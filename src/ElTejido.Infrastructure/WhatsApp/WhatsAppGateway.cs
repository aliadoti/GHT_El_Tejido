using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ElTejido.Application.Seguridad;
using ElTejido.Application.WhatsApp;
using ElTejido.Domain.Campanas;
using ElTejido.Domain.Participantes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ElTejido.Infrastructure.WhatsApp;

/// <summary>
/// Adaptador de WhatsApp Cloud API (05 §2): verificacion de firma HMAC, parseo del webhook y envio
/// (plantilla/texto) por Graph API con reintentos y backoff. El token de acceso y el app secret se
/// resuelven por <see cref="ISecretProvider"/> desde Key Vault (Managed Identity); nunca se loguean.
/// </summary>
public sealed class WhatsAppGateway : IWhatsAppGateway
{
    private const string FirmaPrefijo = "sha256=";

    private readonly HttpClient _httpClient;
    private readonly ISecretProvider _secretos;
    private readonly OpcionesWhatsApp _opciones;
    private readonly TimeProvider _tiempo;
    private readonly ILogger<WhatsAppGateway> _logger;

    public WhatsAppGateway(
        HttpClient httpClient,
        ISecretProvider secretos,
        IOptions<OpcionesWhatsApp> opciones,
        TimeProvider tiempo,
        ILogger<WhatsAppGateway> logger)
    {
        _httpClient = httpClient;
        _secretos = secretos;
        _opciones = opciones.Value;
        _tiempo = tiempo;
        _logger = logger;
    }

    public bool VerificarFirma(ReadOnlySpan<byte> cuerpoCrudo, string? firmaHeader, string appSecret)
    {
        if (string.IsNullOrWhiteSpace(firmaHeader) || string.IsNullOrWhiteSpace(appSecret))
        {
            return false;
        }

        var recibido = firmaHeader.Trim();
        if (recibido.StartsWith(FirmaPrefijo, StringComparison.OrdinalIgnoreCase))
        {
            recibido = recibido[FirmaPrefijo.Length..];
        }

        Span<byte> hash = stackalloc byte[32];
        if (!HMACSHA256.TryHashData(Encoding.UTF8.GetBytes(appSecret), cuerpoCrudo, hash, out _))
        {
            return false;
        }

        var esperado = Convert.ToHexString(hash).ToLowerInvariant();

        // Comparacion en tiempo constante para no filtrar informacion por temporizacion (10 §3).
        var recibidoBytes = Encoding.ASCII.GetBytes(recibido);
        var esperadoBytes = Encoding.ASCII.GetBytes(esperado);
        return CryptographicOperations.FixedTimeEquals(recibidoBytes, esperadoBytes);
    }

    public MensajeEntrante? ParsearWebhook(WhatsAppWebhookPayload payload)
    {
        var mensaje = payload.Entry?
            .SelectMany(entry => entry.Changes ?? Array.Empty<WhatsAppChange>())
            .Select(change => change.Value)
            .Where(value => value is not null)
            .SelectMany(value => value!.Messages ?? Array.Empty<WhatsAppMessage>())
            .FirstOrDefault(EsTextoProcesable);

        if (mensaje is null)
        {
            // Payloads de estado/no-mensaje se ignoran (05 §2.4 paso a).
            return null;
        }

        return new MensajeEntrante(
            mensaje.From!,
            mensaje.Text!.Body!,
            mensaje.Id!,
            ParsearTimestamp(mensaje.Timestamp));
    }

    public Task<EnvioResultado> EnviarPlantillaAsync(
        string numeroE164,
        PlantillaWhatsApp plantilla,
        IReadOnlyDictionary<string, string> variables,
        TipoEnvioMensaje tipo,
        CancellationToken cancellationToken)
    {
        var parametros = plantilla.Componentes
            .Select(componente => new { type = "text", text = variables.GetValueOrDefault(componente, string.Empty) })
            .ToArray();

        object plantillaPayload = parametros.Length == 0
            ? new { name = plantilla.Nombre, language = new { code = plantilla.Idioma } }
            : new
            {
                name = plantilla.Nombre,
                language = new { code = plantilla.Idioma },
                components = new[] { new { type = "body", parameters = parametros } },
            };

        var cuerpo = new
        {
            messaging_product = "whatsapp",
            to = numeroE164,
            type = "template",
            template = plantillaPayload,
        };

        return EnviarAsync(cuerpo, tipo, cancellationToken);
    }

    public Task<EnvioResultado> EnviarTextoAsync(
        string numeroE164,
        string texto,
        TipoEnvioMensaje tipo,
        CancellationToken cancellationToken)
    {
        var cuerpo = new
        {
            messaging_product = "whatsapp",
            to = numeroE164,
            type = "text",
            text = new { body = texto },
        };

        return EnviarAsync(cuerpo, tipo, cancellationToken);
    }

    private async Task<EnvioResultado> EnviarAsync(object cuerpo, TipoEnvioMensaje tipo, CancellationToken cancellationToken)
    {
        string token;
        try
        {
            token = await _secretos.ObtenerSecretoAsync(_opciones.AccessTokenSecretName, cancellationToken);
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException)
        {
            _logger.LogError("No se pudo resolver el token de acceso de WhatsApp para el envio {Tipo}.", tipo);
            return EnvioResultado.Fallo("token_no_disponible");
        }

        var ruta = $"{_opciones.GraphApiBaseUrl.TrimEnd('/')}/{_opciones.PhoneNumberId}/messages";

        for (var intento = 0; ; intento++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, ruta)
                {
                    Content = JsonContent.Create(cuerpo),
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                using var respuesta = await _httpClient.SendAsync(request, cancellationToken);
                if (respuesta.IsSuccessStatusCode)
                {
                    var messageId = await LeerMessageIdAsync(respuesta, cancellationToken);
                    return EnvioResultado.Ok(messageId);
                }

                if (!EsTransitorio(respuesta.StatusCode) || intento >= _opciones.MaxReintentos)
                {
                    _logger.LogWarning(
                        "Envio {Tipo} fallido tras {Intentos} intento(s): HTTP {Codigo}.",
                        tipo,
                        intento + 1,
                        (int)respuesta.StatusCode);
                    return EnvioResultado.Fallo($"http_{(int)respuesta.StatusCode}");
                }
            }
            catch (HttpRequestException ex) when (intento < _opciones.MaxReintentos)
            {
                _logger.LogWarning(ex, "Error transitorio enviando {Tipo}; se reintenta.", tipo);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Error de red enviando {Tipo}; agotados los reintentos.", tipo);
                return EnvioResultado.Fallo("error_red");
            }

            await Task.Delay(CalcularBackoff(intento), _tiempo, cancellationToken);
        }
    }

    private async Task<string?> LeerMessageIdAsync(HttpResponseMessage respuesta, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await respuesta.Content.ReadAsStreamAsync(cancellationToken);
            using var documento = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (documento.RootElement.TryGetProperty("messages", out var mensajes)
                && mensajes.ValueKind == JsonValueKind.Array
                && mensajes.GetArrayLength() > 0
                && mensajes[0].TryGetProperty("id", out var id))
            {
                return id.GetString();
            }
        }
        catch (JsonException)
        {
            // Respuesta sin id utilizable; el envio se considera exitoso por el 2xx.
        }

        return null;
    }

    private TimeSpan CalcularBackoff(int intento)
        => TimeSpan.FromMilliseconds(_opciones.BackoffBaseMs * Math.Pow(2, intento));

    private static bool EsTransitorio(HttpStatusCode codigo)
        => codigo == HttpStatusCode.TooManyRequests || (int)codigo >= 500;

    private static bool EsTextoProcesable(WhatsAppMessage mensaje)
        => string.Equals(mensaje.Type, "text", StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(mensaje.From)
            && !string.IsNullOrWhiteSpace(mensaje.Id)
            && !string.IsNullOrWhiteSpace(mensaje.Text?.Body);

    private DateTimeOffset ParsearTimestamp(string? timestamp)
        => long.TryParse(timestamp, NumberStyles.Integer, CultureInfo.InvariantCulture, out var segundos)
            ? DateTimeOffset.FromUnixTimeSeconds(segundos)
            : _tiempo.GetUtcNow();
}
