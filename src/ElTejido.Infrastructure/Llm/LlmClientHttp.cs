using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ElTejido.Application.Evaluacion;
using ElTejido.Application.Seguridad;
using Microsoft.Extensions.Logging;

namespace ElTejido.Infrastructure.Llm;

/// <summary>
/// Cliente HTTP de LLM agnostico del proveedor (08 §2-§3): adapta la peticion a la API de chat
/// completions de <c>AzureOpenAI</c> u <c>OpenAI</c>/<c>Otro</c> (compatible) segun
/// <see cref="LlmRequest.Proveedor"/>. La API key se resuelve por <see cref="ISecretProvider"/> desde
/// Key Vault (Managed Identity) y <b>nunca</b> se loguea. Aplica timeout y reintentos transitorios;
/// devuelve el texto crudo del modelo (se espera JSON, 08 §4).
/// </summary>
public sealed class LlmClientHttp : ILlmClient
{
    private const string ApiVersionAzure = "2024-06-01";
    private const string VersionAnthropic = "2023-06-01";

    private readonly HttpClient _httpClient;
    private readonly ISecretProvider _secretos;
    private readonly TimeProvider _tiempo;
    private readonly ILogger<LlmClientHttp> _logger;

    public LlmClientHttp(
        HttpClient httpClient,
        ISecretProvider secretos,
        TimeProvider tiempo,
        ILogger<LlmClientHttp> logger)
    {
        _httpClient = httpClient;
        _secretos = secretos;
        _tiempo = tiempo;
        _logger = logger;
    }

    public async Task<string> CompletarJsonAsync(LlmRequest request, CancellationToken cancellationToken)
    {
        var apiKey = await _secretos.ObtenerSecretoAsync(request.ApiKeyRef, cancellationToken);
        var proveedor = ResolverProveedor(request.Proveedor);
        var cuerpo = ConstruirCuerpo(request, proveedor);
        var ruta = ResolverRuta(request, proveedor);

        for (var intento = 0; ; intento++)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(request.TimeoutSegundos));

            try
            {
                using var solicitud = new HttpRequestMessage(HttpMethod.Post, ruta)
                {
                    Content = JsonContent.Create(cuerpo),
                };
                AplicarAutenticacion(solicitud, proveedor, apiKey, request.Parametros);

                using var respuesta = await _httpClient.SendAsync(solicitud, timeoutCts.Token);
                if (respuesta.IsSuccessStatusCode)
                {
                    return await LeerContenidoAsync(respuesta, proveedor, timeoutCts.Token);
                }

                if (!EsTransitorio(respuesta.StatusCode) || intento >= request.MaxReintentos)
                {
                    // El cuerpo de error del proveedor (JSON con el motivo) es interno y no contiene
                    // la API key (esta viaja en headers, no se refleja en la respuesta); se loguea
                    // truncado para diagnosticar 4xx de configuracion (modelo/params invalidos).
                    var detalle = await LeerErrorSeguroAsync(respuesta, timeoutCts.Token);
                    _logger.LogWarning(
                        "LLM {Proveedor} respondio HTTP {Codigo} tras {Intentos} intento(s). Detalle: {Detalle}",
                        request.Proveedor,
                        (int)respuesta.StatusCode,
                        intento + 1,
                        detalle);
                    throw new HttpRequestException($"LLM respondio HTTP {(int)respuesta.StatusCode}.");
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException) when (intento >= request.MaxReintentos)
            {
                // Timeout agotados los reintentos: se propaga para que el evaluador haga fallback (08 §6).
                throw new TimeoutException("Timeout del proveedor LLM.");
            }
            catch (OperationCanceledException)
            {
                // Timeout de este intento; se reintenta.
            }
            catch (HttpRequestException) when (intento < request.MaxReintentos)
            {
                // Error transitorio de red; se reintenta.
            }

            await Task.Delay(CalcularBackoff(intento), _tiempo, cancellationToken);
        }
    }

    private static Dictionary<string, object?> ConstruirCuerpo(LlmRequest request, TipoProveedorLlm proveedor)
        => proveedor == TipoProveedorLlm.Anthropic
            ? ConstruirCuerpoAnthropic(request)
            : ConstruirCuerpoOpenAi(request);

    private static Dictionary<string, object?> ConstruirCuerpoOpenAi(LlmRequest request)
    {
        var cuerpo = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["model"] = request.Modelo,
            ["max_tokens"] = request.MaxCompletionTokens,
            ["response_format"] = new { type = "json_object" },
            ["messages"] = request.Mensajes
                .Select(m => new { role = m.Rol, content = m.Contenido })
                .ToArray(),
        };

        // Parametros configurables (temperature, top_p, ...) sin sobreescribir los campos base.
        foreach (var parametro in request.Parametros)
        {
            if (!cuerpo.ContainsKey(parametro.Key))
            {
                cuerpo[parametro.Key] = parametro.Value;
            }
        }

        return cuerpo;
    }

    private static Dictionary<string, object?> ConstruirCuerpoAnthropic(LlmRequest request)
    {
        var cuerpo = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["model"] = request.Modelo,
            ["max_tokens"] = request.MaxCompletionTokens,
            ["messages"] = request.Mensajes
                .Where(m => !string.Equals(m.Rol, LlmMensaje.RolSistema, StringComparison.Ordinal))
                .Select(m => new { role = MapearRolAnthropic(m.Rol), content = m.Contenido })
                .ToArray(),
        };

        var system = string.Join(
            "\n\n",
            request.Mensajes
                .Where(m => string.Equals(m.Rol, LlmMensaje.RolSistema, StringComparison.Ordinal))
                .Select(m => m.Contenido));
        if (!string.IsNullOrWhiteSpace(system))
        {
            cuerpo["system"] = system;
        }

        foreach (var parametro in request.Parametros)
        {
            if (!EsParametroReservadoAnthropic(parametro.Key) && !cuerpo.ContainsKey(parametro.Key))
            {
                cuerpo[parametro.Key] = parametro.Value;
            }
        }

        return cuerpo;
    }

    private static string ResolverRuta(LlmRequest request, TipoProveedorLlm proveedor)
    {
        var baseUrl = request.Endpoint.TrimEnd('/');
        return proveedor switch
        {
            TipoProveedorLlm.AzureOpenAi => $"{baseUrl}/openai/deployments/{request.Modelo}/chat/completions?api-version={ApiVersionAzure}",
            TipoProveedorLlm.Anthropic => $"{baseUrl}/v1/messages",
            _ => $"{baseUrl}/chat/completions",
        };
    }

    private static void AplicarAutenticacion(
        HttpRequestMessage solicitud,
        TipoProveedorLlm proveedor,
        string apiKey,
        IReadOnlyDictionary<string, object?> parametros)
    {
        if (proveedor == TipoProveedorLlm.AzureOpenAi)
        {
            solicitud.Headers.Add("api-key", apiKey);
        }
        else if (proveedor == TipoProveedorLlm.Anthropic)
        {
            solicitud.Headers.Add("x-api-key", apiKey);
            solicitud.Headers.Add("anthropic-version", ResolverVersionAnthropic(parametros));
        }
        else
        {
            solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }
    }

    private static async Task<string> LeerContenidoAsync(
        HttpResponseMessage respuesta,
        TipoProveedorLlm proveedor,
        CancellationToken cancellationToken)
    {
        await using var stream = await respuesta.Content.ReadAsStreamAsync(cancellationToken);
        using var documento = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (proveedor == TipoProveedorLlm.Anthropic
            && documento.RootElement.TryGetProperty("content", out var contenidoAnthropic)
            && contenidoAnthropic.ValueKind == JsonValueKind.Array
            && contenidoAnthropic.GetArrayLength() > 0
            && contenidoAnthropic[0].TryGetProperty("text", out var textoAnthropic)
            && textoAnthropic.GetString() is { } textoAnthropicValue)
        {
            return textoAnthropicValue;
        }

        if (documento.RootElement.TryGetProperty("choices", out var choices)
            && choices.ValueKind == JsonValueKind.Array
            && choices.GetArrayLength() > 0
            && choices[0].TryGetProperty("message", out var message)
            && message.TryGetProperty("content", out var content)
            && content.GetString() is { } texto)
        {
            return texto;
        }

        throw new InvalidOperationException("La respuesta del LLM no contiene contenido utilizable.");
    }

    private static async Task<string> LeerErrorSeguroAsync(HttpResponseMessage respuesta, CancellationToken cancellationToken)
    {
        try
        {
            var cuerpo = await respuesta.Content.ReadAsStringAsync(cancellationToken);
            cuerpo = cuerpo.Replace('\r', ' ').Replace('\n', ' ').Trim();
            return cuerpo.Length switch
            {
                0 => "(sin cuerpo)",
                > 500 => cuerpo[..500],
                _ => cuerpo,
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return "(no se pudo leer el cuerpo)";
        }
    }

    private static TimeSpan CalcularBackoff(int intento)
        => TimeSpan.FromMilliseconds(200 * Math.Pow(2, intento));

    private static bool EsTransitorio(HttpStatusCode codigo)
        => codigo == HttpStatusCode.TooManyRequests || (int)codigo >= 500;

    private static TipoProveedorLlm ResolverProveedor(string proveedor)
    {
        if (string.Equals(proveedor, "AzureOpenAI", StringComparison.OrdinalIgnoreCase))
        {
            return TipoProveedorLlm.AzureOpenAi;
        }

        return string.Equals(proveedor, "Anthropic", StringComparison.OrdinalIgnoreCase)
            ? TipoProveedorLlm.Anthropic
            : TipoProveedorLlm.OpenAiCompatible;
    }

    private static string MapearRolAnthropic(string rol)
        => string.Equals(rol, "assistant", StringComparison.OrdinalIgnoreCase) ? "assistant" : "user";

    private static bool EsParametroReservadoAnthropic(string parametro)
        => string.Equals(parametro, "model", StringComparison.Ordinal)
            || string.Equals(parametro, "max_tokens", StringComparison.Ordinal)
            || string.Equals(parametro, "messages", StringComparison.Ordinal)
            || string.Equals(parametro, "system", StringComparison.Ordinal)
            || string.Equals(parametro, "anthropic-version", StringComparison.OrdinalIgnoreCase);

    private static string ResolverVersionAnthropic(IReadOnlyDictionary<string, object?> parametros)
        => parametros.TryGetValue("anthropic-version", out var version) && version is not null
            ? version.ToString() ?? VersionAnthropic
            : VersionAnthropic;

    private enum TipoProveedorLlm
    {
        AzureOpenAi,
        OpenAiCompatible,
        Anthropic,
    }
}
