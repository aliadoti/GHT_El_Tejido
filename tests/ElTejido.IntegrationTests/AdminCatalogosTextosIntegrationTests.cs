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

    private static WebApplicationFactory<Program> ConstruirFabrica()
        => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("Persistencia:Modo", "Memoria");
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
            => Task.FromResult<PrincipalSesion?>(token == "token-admin"
                ? new PrincipalSesion(
                    "u_admin",
                    "Admin",
                    RolUsuario.Admin,
                    "csrf-admin",
                    DateTimeOffset.UtcNow.AddMinutes(30))
                : null);
    }
}
