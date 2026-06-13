using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ElTejido.IntegrationTests;

/// <summary>
/// Verifica el modelo de errores uniforme (04 §3), la propagacion del correlationId (04 §8, 10 §6.2)
/// y el rate limiting con respuesta 429 + Retry-After (10 §2, §3) sobre la app real en Development.
/// </summary>
public sealed class ModeloErroresIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ModeloErroresIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
    }

    [Fact]
    public async Task Health_SigueDevolviendo200EnDevelopment()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ErrorNoControlado_DevuelveModeloUniforme500SinFiltrarDetalles()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/diagnostico/error");
        var cuerpo = await response.Content.ReadFromJsonAsync<CuerpoError>();

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        cuerpo.Should().NotBeNull();
        cuerpo!.Error.Code.Should().Be("INTERNAL_ERROR");
        cuerpo.Error.Message.Should().NotContain("simulado");
        cuerpo.Error.CorrelationId.Should().StartWith("corr_");
    }

    [Fact]
    public async Task ErrorDeValidacion_DevuelveCodigoYDetallesDelContrato()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/diagnostico/validacion");
        var cuerpo = await response.Content.ReadFromJsonAsync<CuerpoError>();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        cuerpo!.Error.Code.Should().Be("VALIDATION_ERROR");
        cuerpo.Error.Details.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new DetalleError("numero", "formato"));
        cuerpo.Error.CorrelationId.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CorrelationIdEntrante_SeReflejaEnCabeceraYCuerpo()
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/diagnostico/validacion");
        request.Headers.Add("X-Correlation-Id", "corr_pruebaentrante");

        using var response = await client.SendAsync(request);
        var cuerpo = await response.Content.ReadFromJsonAsync<CuerpoError>();

        response.Headers.GetValues("X-Correlation-Id").Should().ContainSingle()
            .Which.Should().Be("corr_pruebaentrante");
        cuerpo!.Error.CorrelationId.Should().Be("corr_pruebaentrante");
    }

    [Fact]
    public async Task RateLimit_AlExcederDevuelve429ConRetryAfterYModeloUniforme()
    {
        using var client = _factory.CreateClient();

        using var primera = await client.GetAsync("/diagnostico/limitado");
        using var segunda = await client.GetAsync("/diagnostico/limitado");

        primera.StatusCode.Should().Be(HttpStatusCode.OK);
        segunda.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        segunda.Headers.RetryAfter.Should().NotBeNull();

        var cuerpo = await segunda.Content.ReadFromJsonAsync<CuerpoError>();
        cuerpo!.Error.Code.Should().Be("RATE_LIMITED");
        cuerpo.Error.CorrelationId.Should().StartWith("corr_");
    }

    private sealed record CuerpoError(ErrorDto Error);

    private sealed record ErrorDto(
        string Code,
        string Message,
        IReadOnlyList<DetalleError>? Details,
        string CorrelationId);

    private sealed record DetalleError(string? Field, string Issue);
}
