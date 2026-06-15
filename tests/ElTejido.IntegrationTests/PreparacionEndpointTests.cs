using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace ElTejido.IntegrationTests;

/// <summary>
/// Prueba el endpoint de preparacion <c>GET /health/ready</c> (guia de Azure §11): protegido por la
/// clave de diagnostico (404 sin clave o con clave equivocada), reporta 503 cuando falta una
/// dependencia y 200 cuando todo lo requerido esta presente, sin exponer valores de secretos.
/// </summary>
public sealed class PreparacionEndpointTests
{
    private const string Clave = "clave-de-diagnostico-de-pruebas";
    private const string Header = "X-Diag-Key";

    [Fact]
    public async Task SinClaveConfigurada_Responde404AunqueSeEnvieHeader()
    {
        using var fabrica = Construir(claveConfigurada: false);
        using var client = fabrica.CreateClient();
        client.DefaultRequestHeaders.Add(Header, "cualquier-cosa");

        using var respuesta = await client.GetAsync("/health/ready");

        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ClaveIncorrecta_Responde404()
    {
        using var fabrica = Construir(claveConfigurada: true);
        using var client = fabrica.CreateClient();
        client.DefaultRequestHeaders.Add(Header, "clave-equivocada");

        using var respuesta = await client.GetAsync("/health/ready");

        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ClaveCorrectaConSecretosFaltantes_Responde503ConDesglose()
    {
        using var fabrica = Construir(claveConfigurada: true);
        using var client = fabrica.CreateClient();
        client.DefaultRequestHeaders.Add(Header, Clave);

        using var respuesta = await client.GetAsync("/health/ready");

        respuesta.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<ReporteDto>();
        cuerpo!.Estado.Should().Be("faltante");
        cuerpo.Componentes.Should().Contain(c => c.Componente == "secreto:wa-token" && c.Estado == "faltante");
        cuerpo.Componentes.Should().Contain(c => c.Componente == "cosmos" && c.Estado == "no_aplica");
    }

    [Fact]
    public async Task ClaveCorrectaConTodoPresente_Responde200SinFiltrarSecretos()
    {
        var secretos = new Dictionary<string, string?>
        {
            ["Secretos:jwt-sign"] = "clave-de-firma-de-pruebas-con-mas-de-32-bytes",
            ["Secretos:otp-salt"] = "pepper-de-pruebas",
            ["Secretos:wa-verify-token"] = "verify-de-pruebas",
            ["Secretos:wa-token"] = "token-de-pruebas",
            ["Secretos:wa-appsec"] = "appsec-de-pruebas",
            ["Secretos:llm-key"] = "llm-de-pruebas",
            ["WhatsApp:PhoneNumberId"] = "123456789",
        };

        using var fabrica = Construir(claveConfigurada: true, extra: secretos);
        using var client = fabrica.CreateClient();
        client.DefaultRequestHeaders.Add(Header, Clave);

        using var respuesta = await client.GetAsync("/health/ready");

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        var crudo = await respuesta.Content.ReadAsStringAsync();
        crudo.Should().NotContain("token-de-pruebas").And.NotContain("appsec-de-pruebas");
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<ReporteDto>();
        cuerpo!.Estado.Should().Be("ok");
        cuerpo.Componentes.Should().Contain(c => c.Componente == "secreto:wa-token" && c.Estado == "ok");
    }

    private static WebApplicationFactory<Program> Construir(
        bool claveConfigurada,
        IDictionary<string, string?>? extra = null)
        => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                var valores = new Dictionary<string, string?>();
                if (claveConfigurada)
                {
                    valores["Diagnostico:Clave"] = Clave;
                }

                if (extra is not null)
                {
                    foreach (var par in extra)
                    {
                        valores[par.Key] = par.Value;
                    }
                }

                config.AddInMemoryCollection(valores);
            });
        });

    private sealed record ReporteDto(string Estado, IReadOnlyList<ComponenteDto> Componentes);

    private sealed record ComponenteDto(string Componente, string Estado, string Detalle);
}
