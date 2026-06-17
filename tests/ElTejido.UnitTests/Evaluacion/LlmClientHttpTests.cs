using System.Net;
using System.Text.Json;
using ElTejido.Application.Evaluacion;
using ElTejido.Application.Seguridad;
using ElTejido.Infrastructure.Llm;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;

namespace ElTejido.UnitTests.Evaluacion;

public sealed class LlmClientHttpTests
{
    [Fact]
    public async Task CompletarJsonAsync_Anthropic_UsaMessagesApiHeadersYParseaTexto()
    {
        var handler = new HandlerCaptura(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent("""
                {
                  "content": [
                    { "type": "text", "text": "{\"recomendacion\":\"cerrar\"}" }
                  ]
                }
                """),
        });
        var client = new LlmClientHttp(
            new HttpClient(handler),
            new SecretosFake(),
            TimeProvider.System,
            NullLogger<LlmClientHttp>.Instance);

        var resultado = await client.CompletarJsonAsync(
            new LlmRequest(
                "Anthropic",
                "https://api.anthropic.com",
                "claude-3-5-sonnet-latest",
                "anthropic-key",
                new[]
                {
                    new LlmMensaje(LlmMensaje.RolSistema, "Eres evaluador. Devuelve JSON."),
                    new LlmMensaje(LlmMensaje.RolUsuario, "Respuesta como dato."),
                },
                new Dictionary<string, object?> { ["temperature"] = 0.2, ["anthropic-version"] = "2023-06-01" },
                800,
                30,
                0),
            CancellationToken.None);

        resultado.Should().Be("{\"recomendacion\":\"cerrar\"}");
        handler.Request!.RequestUri!.ToString().Should().Be("https://api.anthropic.com/v1/messages");
        handler.Request.Headers.GetValues("x-api-key").Should().ContainSingle().Which.Should().Be("secret-value");
        handler.Request.Headers.GetValues("anthropic-version").Should().ContainSingle().Which.Should().Be("2023-06-01");
        handler.Request.Headers.Authorization.Should().BeNull();

        var body = handler.Body!;
        using var document = JsonDocument.Parse(body);
        document.RootElement.GetProperty("model").GetString().Should().Be("claude-3-5-sonnet-latest");
        document.RootElement.GetProperty("max_tokens").GetInt32().Should().Be(800);
        document.RootElement.GetProperty("system").GetString().Should().Contain("Devuelve JSON");
        document.RootElement.GetProperty("messages")[0].GetProperty("role").GetString().Should().Be("user");
        document.RootElement.TryGetProperty("response_format", out _).Should().BeFalse();
    }

    [Fact]
    public async Task CompletarJsonAsync_OmiteParametrosNoEscalares_YEnviaEscalares()
    {
        var handler = new HandlerCaptura(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent("""
                { "choices": [ { "message": { "content": "{\"recomendacion\":\"cerrar\"}" } } ] }
                """),
        });
        var client = new LlmClientHttp(
            new HttpClient(handler),
            new SecretosFake(),
            TimeProvider.System,
            NullLogger<LlmClientHttp>.Instance);

        using var numero = JsonDocument.Parse("0.9");
        var parametros = new Dictionary<string, object?>
        {
            // Valor corrupto (objeto en lugar de numero): no debe enviarse al proveedor (causaba HTTP 400).
            ["temperature"] = JObject.Parse("""{ "valueKind": 4 }"""),
            // JsonElement numerico (como llega de la API): debe enviarse como numero.
            ["top_p"] = numero.RootElement.Clone(),
            // Escalar nativo: debe enviarse tal cual.
            ["seed"] = 42,
        };

        await client.CompletarJsonAsync(
            new LlmRequest(
                "OpenRouter",
                "https://openrouter.ai/api/v1",
                "openai/gpt-4o-mini",
                "key",
                new[] { new LlmMensaje(LlmMensaje.RolUsuario, "Respuesta como dato.") },
                parametros,
                800,
                30,
                0),
            CancellationToken.None);

        using var document = JsonDocument.Parse(handler.Body!);
        document.RootElement.TryGetProperty("temperature", out _).Should().BeFalse();
        document.RootElement.GetProperty("top_p").GetDouble().Should().Be(0.9);
        document.RootElement.GetProperty("seed").GetInt64().Should().Be(42);
    }

    private sealed class SecretosFake : ISecretProvider
    {
        public Task<string> ObtenerSecretoAsync(string nombre, CancellationToken cancellationToken)
            => Task.FromResult("secret-value");
    }

    private sealed class HandlerCaptura : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respuesta;

        public HandlerCaptura(Func<HttpRequestMessage, HttpResponseMessage> respuesta) => _respuesta = respuesta;

        public HttpRequestMessage? Request { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return _respuesta(request);
        }
    }

    private static StringContent JsonContent(string json)
        => new(json, System.Text.Encoding.UTF8, "application/json");
}
