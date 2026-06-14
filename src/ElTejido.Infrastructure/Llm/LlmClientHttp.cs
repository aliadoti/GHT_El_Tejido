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
        var cuerpo = ConstruirCuerpo(request);
        var (ruta, esAzure) = ResolverRuta(request);

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
                AplicarAutenticacion(solicitud, esAzure, apiKey);

                using var respuesta = await _httpClient.SendAsync(solicitud, timeoutCts.Token);
                if (respuesta.IsSuccessStatusCode)
                {
                    return await LeerContenidoAsync(respuesta, timeoutCts.Token);
                }

                if (!EsTransitorio(respuesta.StatusCode) || intento >= request.MaxReintentos)
                {
                    _logger.LogWarning(
                        "LLM {Proveedor} respondio HTTP {Codigo} tras {Intentos} intento(s).",
                        request.Proveedor,
                        (int)respuesta.StatusCode,
                        intento + 1);
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

    private static Dictionary<string, object?> ConstruirCuerpo(LlmRequest request)
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

    private static (string Ruta, bool EsAzure) ResolverRuta(LlmRequest request)
    {
        var baseUrl = request.Endpoint.TrimEnd('/');
        var esAzure = string.Equals(request.Proveedor, "AzureOpenAI", StringComparison.OrdinalIgnoreCase);
        var ruta = esAzure
            ? $"{baseUrl}/openai/deployments/{request.Modelo}/chat/completions?api-version={ApiVersionAzure}"
            : $"{baseUrl}/chat/completions";
        return (ruta, esAzure);
    }

    private static void AplicarAutenticacion(HttpRequestMessage solicitud, bool esAzure, string apiKey)
    {
        if (esAzure)
        {
            solicitud.Headers.Add("api-key", apiKey);
        }
        else
        {
            solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }
    }

    private static async Task<string> LeerContenidoAsync(HttpResponseMessage respuesta, CancellationToken cancellationToken)
    {
        await using var stream = await respuesta.Content.ReadAsStreamAsync(cancellationToken);
        using var documento = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

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

    private static TimeSpan CalcularBackoff(int intento)
        => TimeSpan.FromMilliseconds(200 * Math.Pow(2, intento));

    private static bool EsTransitorio(HttpStatusCode codigo)
        => codigo == HttpStatusCode.TooManyRequests || (int)codigo >= 500;
}
