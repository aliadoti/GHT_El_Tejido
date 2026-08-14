using System.Net;
using System.Net.Http.Json;
using ElTejido.Application.Auth;
using ElTejido.Application.Configuracion;
using ElTejido.Domain.Usuarios;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ElTejido.IntegrationTests;

public sealed class AdminCatalogosTextosIntegrationTests
{
    [Fact]
    public async Task Catalogo_AdminCreaActivaYConsultaVersionEfectiva()
    {
        using var fabrica = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("Persistencia:Modo", "Memoria");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IServicioSesion>();
                services.AddSingleton<IServicioSesion, SesionesFake>();
            });
        });
        using var client = fabrica.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", "eltejido_sesion=token-admin");
        var contenido = ContenidoValido();

        using var crear = new HttpRequestMessage(HttpMethod.Post, "/api/admin/catalogos-textos")
        {
            Content = JsonContent.Create(new
            {
                familiaId = "conversacion-global",
                idioma = "es",
                contenido.Mensajes,
                contenido.Frases,
            }),
        };
        crear.Headers.Add("X-CSRF-Token", "csrf-admin");
        using var creado = await client.SendAsync(crear);

        creado.StatusCode.Should().Be(HttpStatusCode.Created);
        creado.Headers.ETag.Should().NotBeNull();

        using var activar = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/admin/catalogos-textos/conversacion-global/es/versiones/1/activar");
        activar.Headers.Add("X-CSRF-Token", "csrf-admin");
        activar.Headers.TryAddWithoutValidation("If-Match", creado.Headers.ETag!.Tag);
        using var activado = await client.SendAsync(activar);
        activado.StatusCode.Should().Be(HttpStatusCode.OK);

        using var efectivo = await client.GetAsync("/api/admin/catalogos-textos/efectivo?idioma=es");
        efectivo.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await efectivo.Content.ReadAsStringAsync();
        json.Should().Contain("\"origen\":\"catalogo\"");
        json.Should().Contain("\"estado\":\"activo\"");
        json.Should().Contain("\"idioma\":\"es\"");
    }

    [Fact]
    public async Task Semilla_AdminLaCreaComoBorradorSinActivarla()
    {
        using var fabrica = ConstruirFabrica();
        using var client = fabrica.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", "eltejido_sesion=token-admin");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/catalogos-textos/semillas/en");
        request.Headers.Add("X-CSRF-Token", "csrf-admin");

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("\"idioma\":\"en\"");
        json.Should().Contain("\"estado\":\"borrador\"");
        using var efectivo = await client.GetAsync("/api/admin/catalogos-textos/efectivo?idioma=en");
        (await efectivo.Content.ReadAsStringAsync()).Should().Contain("\"origen\":\"emergencia\"");
    }

    // --- DT-P32-02 corte 1/3: semilla base vs. fotografia legacy ---

    [Fact]
    public async Task SemillaBase_CreaBorradorAunqueElLegacySupereElLimite()
    {
        using var fabrica = ConstruirFabrica(maxFrasesPorGrupo: 30, conLegacyExcedido: true);
        using var client = ClienteAdmin(fabrica);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/catalogos-textos/semillas/es/base");
        request.Headers.Add("X-CSRF-Token", "csrf-admin");
        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("\"idioma\":\"es\"").And.Contain("\"estado\":\"borrador\"");
        json.Should().NotContain("frase legacy");
        (await ListarVersionesAsync(client)).Should().HaveCount(1);
    }

    [Fact]
    public async Task PrevalidarLegacy_ReportaElGrupoExcedidoYNoPersisteNada()
    {
        using var fabrica = ConstruirFabrica(maxFrasesPorGrupo: 30, conLegacyExcedido: true);
        using var client = ClienteAdmin(fabrica);

        using var response = await client.GetAsync(
            "/api/admin/catalogos-textos/semillas/es/legacy/preview");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("\"valido\":false");
        json.Should().Contain("\"field\":\"frases.despertarProactivo\"");
        json.Should().Contain("\"issue\":\"debe_tener_entre_1_y_30_elementos\"");
        json.Should().Contain("\"gruposFrases\":16");
        json.Should().NotContain("frase legacy");
        (await ListarVersionesAsync(client)).Should().BeEmpty();
    }

    [Fact]
    public async Task ExportarLegacy_ConservaTodasLasEntradasAunqueSeaInvalido()
    {
        using var fabrica = ConstruirFabrica(maxFrasesPorGrupo: 30, conLegacyExcedido: true);
        using var client = ClienteAdmin(fabrica);

        using var response = await client.GetAsync(
            "/api/admin/catalogos-textos/semillas/es/legacy/exportar");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentDisposition!.FileNameStar
            .Should().Be("catalogo-catalogo_conversacion-es-legacy-editable.json");
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("\"formato\": \"catalogo-textos/v1\"");
        // Ninguna entrada se recorta: estan las 31, incluida la que rompe el limite.
        json.Should().Contain("frase legacy 0").And.Contain("frase legacy 30");
        (await ListarVersionesAsync(client)).Should().BeEmpty();
    }

    [Fact]
    public async Task ImportarLegacy_PorEncimaDelLimite_DevuelveValidacionYNoCreaVersion()
    {
        using var fabrica = ConstruirFabrica(maxFrasesPorGrupo: 30, conLegacyExcedido: true);
        using var client = ClienteAdmin(fabrica);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/catalogos-textos/semillas/es/legacy");
        request.Headers.Add("X-CSRF-Token", "csrf-admin");
        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("VALIDATION_ERROR").And.Contain("frases.despertarProactivo");
        (await ListarVersionesAsync(client)).Should().BeEmpty();
    }

    [Fact]
    public async Task ImportarLegacy_ConLimiteOperativoAmpliado_CreaBorradorSinRecompilar()
    {
        using var fabrica = ConstruirFabrica(maxFrasesPorGrupo: 100, conLegacyExcedido: true);
        using var client = ClienteAdmin(fabrica);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/catalogos-textos/semillas/es/legacy");
        request.Headers.Add("X-CSRF-Token", "csrf-admin");
        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await response.Content.ReadAsStringAsync()).Should().Contain("\"estado\":\"borrador\"");
        using var efectivo = await client.GetAsync("/api/admin/catalogos-textos/efectivo?idioma=es");
        // Sigue sin activarse: el efectivo cae al respaldo compilado.
        (await efectivo.Content.ReadAsStringAsync()).Should().Contain("\"origen\":\"emergencia\"");
    }

    [Fact]
    public async Task SemillasNuevas_VisorPrevalidaPeroNoPuedeCrear()
    {
        using var fabrica = ConstruirFabrica(maxFrasesPorGrupo: 30, conLegacyExcedido: true);
        using var client = fabrica.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", "eltejido_sesion=token-visor");

        using var preview = await client.GetAsync("/api/admin/catalogos-textos/semillas/es/legacy/preview");
        using var crear = new HttpRequestMessage(HttpMethod.Post, "/api/admin/catalogos-textos/semillas/es/base");
        crear.Headers.Add("X-CSRF-Token", "csrf-visor");
        using var creado = await client.SendAsync(crear);

        preview.StatusCode.Should().Be(HttpStatusCode.OK);
        creado.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SemillaBase_IdiomaInvalido_DevuelveValidacion()
    {
        using var fabrica = ConstruirFabrica();
        using var client = ClienteAdmin(fabrica);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/catalogos-textos/semillas/fr/base");
        request.Headers.Add("X-CSRF-Token", "csrf-admin");
        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("\"field\":\"idioma\"");
    }

    private static HttpClient ClienteAdmin(WebApplicationFactory<Program> fabrica)
    {
        var client = fabrica.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", "eltejido_sesion=token-admin");
        return client;
    }

    private static async Task<IReadOnlyCollection<object>> ListarVersionesAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/admin/catalogos-textos?idioma=es");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<IReadOnlyCollection<object>>()
            ?? Array.Empty<object>();
    }

    private static WebApplicationFactory<Program> ConstruirFabrica(
        int? maxFrasesPorGrupo = null,
        bool conLegacyExcedido = false)
        => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("Persistencia:Modo", "Memoria");
            if (maxFrasesPorGrupo is not null)
            {
                builder.UseSetting(
                    "Conversacion:CatalogoTextos:MaxFrasesPorGrupo",
                    maxFrasesPorGrupo.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }

            if (conLegacyExcedido)
            {
                // Reproduce la corrida del 2026-08-13: 31 frases heredadas en un solo grupo.
                for (var indice = 0; indice < 31; indice++)
                {
                    builder.UseSetting(
                        $"Conversacion:FrasesDespertarProactivo:{indice}",
                        $"frase legacy {indice}");
                }
            }

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IServicioSesion>();
                services.AddSingleton<IServicioSesion, SesionesFake>();
            });
        });

    private static SolicitudContenidoCatalogoTextos ContenidoValido()
    {
        var mensajes = ValidadorCatalogoTextosConversacion.ClavesMensajes
            .ToDictionary(x => x, x => $"{x} {{{{nombre}}}}", StringComparer.Ordinal);
        var frases = ValidadorCatalogoTextosConversacion.ClavesFrases
            .ToDictionary(
                x => x,
                x => (IReadOnlyCollection<string>)new[] { $"{x} opcion" },
                StringComparer.Ordinal);
        return new SolicitudContenidoCatalogoTextos(mensajes, frases);
    }

    private sealed class SesionesFake : IServicioSesion
    {
        public Task<SesionEmitida> EmitirAsync(ElTejido.Domain.Usuarios.Usuario usuario, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<PrincipalSesion?> ValidarAsync(string token, CancellationToken cancellationToken)
            => Task.FromResult<PrincipalSesion?>(token switch
            {
                "token-admin" => new PrincipalSesion(
                    "u_admin",
                    "Admin",
                    RolUsuario.Admin,
                    "csrf-admin",
                    DateTimeOffset.UtcNow.AddMinutes(30)),
                "token-visor" => new PrincipalSesion(
                    "u_visor",
                    "Visor",
                    RolUsuario.Visor,
                    "csrf-visor",
                    DateTimeOffset.UtcNow.AddMinutes(30)),
                _ => null,
            });
    }
}
