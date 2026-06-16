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

    public Task<EnvioResultado> EnviarPlantillaAutenticacionAsync(
        string numeroE164,
        PlantillaWhatsApp plantilla,
        string codigo,
        TipoEnvioMensaje tipo,
        CancellationToken cancellationToken)
    {
        // Meta exige el codigo en el body y en el boton (sub_type=url, index=0) para las plantillas
        // de categoria Authentication con boton copy-code/one-tap.
        var parametroCodigo = new[] { new { type = "text", text = codigo } };

        var cuerpo = new
        {
            messaging_product = "whatsapp",
            to = numeroE164,
            type = "template",
            template = new
            {
                name = plantilla.Nombre,
                language = new { code = plantilla.Idioma },
                components = new object[]
                {
                    new { type = "body", parameters = parametroCodigo },
                    new
                    {
                        type = "button",
                        sub_type = "url",
                        index = "0",
                        parameters = parametroCodigo,
                    },
                },
            },
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
            _logger.LogError(
                "Envio {Tipo} no realizado: falta el secreto '{Secreto}' en el almacen de secretos (Key Vault). "
                    + "Configuralo para habilitar el envio por WhatsApp.",
                tipo,
                _opciones.AccessTokenSecretName);
            return EnvioResultado.Fallo(
                $"WhatsApp no configurado: falta el secreto '{_opciones.AccessTokenSecretName}' en Key Vault.");
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
                    // Se registra el detalle de error de Meta (code/subcode/message/fbtrace_id) para
                    // diagnosticar; el cuerpo de error de Graph API no contiene nuestros secretos.
                    var detalle = await LeerDetalleErrorAsync(respuesta, cancellationToken);
                    _logger.LogWarning(
                        "Envio {Tipo} fallido tras {Intentos} intento(s): HTTP {Codigo}. Detalle de Meta: {Detalle}",
                        tipo,
                        intento + 1,
                        (int)respuesta.StatusCode,
                        detalle);
                    return EnvioResultado.Fallo(
                        $"WhatsApp rechazo el envio (HTTP {(int)respuesta.StatusCode}). {detalle}");
                }
            }
            catch (HttpRequestException ex) when (intento < _opciones.MaxReintentos)
            {
                _logger.LogWarning(ex, "Error transitorio enviando {Tipo}; se reintenta.", tipo);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Error de red enviando {Tipo}; agotados los reintentos.", tipo);
                return EnvioResultado.Fallo("Error de red al contactar WhatsApp; se agotaron los reintentos.");
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

    /// <summary>
    /// Extrae un detalle diciente del cuerpo de error de Graph API (objeto <c>error</c> con
    /// <c>code</c>/<c>error_subcode</c>/<c>message</c>/<c>fbtrace_id</c>) para el log y el
    /// <see cref="EnvioResultado"/>. El cuerpo de error de Meta no contiene nuestros secretos.
    /// </summary>
    private static async Task<string> LeerDetalleErrorAsync(HttpResponseMessage respuesta, CancellationToken cancellationToken)
    {
        try
        {
            var json = await respuesta.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(json))
            {
                return "(sin cuerpo de error)";
            }

            using var documento = JsonDocument.Parse(json);
            if (documento.RootElement.TryGetProperty("error", out var error))
            {
                var mensaje = error.TryGetProperty("message", out var m) ? m.GetString() : null;
                var codigo = error.TryGetProperty("code", out var c) ? c.ToString() : "?";
                var subcodigo = error.TryGetProperty("error_subcode", out var s) ? s.ToString() : null;
                var trace = error.TryGetProperty("fbtrace_id", out var t) ? t.GetString() : null;
                var sub = subcodigo is null ? string.Empty : $" subcode={subcodigo}";
                return $"code={codigo}{sub} message=\"{mensaje}\" fbtrace_id={trace}";
            }

            return json.Length > 500 ? json[..500] : json;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or HttpRequestException)
        {
            return "(cuerpo de error no legible)";
        }
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
