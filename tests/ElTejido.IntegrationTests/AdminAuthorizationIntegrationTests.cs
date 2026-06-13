using System.Net;
using System.Net.Http.Json;
using ElTejido.Application.Auth;
using ElTejido.Domain.Usuarios;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace ElTejido.IntegrationTests;

/// <summary>
/// Verifica el enforcement comun de <c>/api/admin/*</c> (04 §1/§5, 06 §4.4):
/// sesion requerida, GET para admin/visor, mutaciones solo admin + CSRF.
/// </summary>
public sealed class AdminAuthorizationIntegrationTests
{
    private const string CookieSesion = "eltejido_sesion";
    private const string CsrfAdmin = "csrf-admin";
    private const string CsrfVisor = "csrf-visor";

    [Fact]
    public async Task GetAdmin_SinCookie_Responde401()
    {
        using var fabrica = Construir();
        using var client = fabrica.CreateClient();

        using var respuesta = await client.GetAsync("/api/admin/diagnostico/lectura");

        respuesta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<CuerpoErrorDto>();
        cuerpo!.Error.Code.Should().Be("UNAUTHENTICATED");
    }

    [Fact]
    public async Task GetAdmin_TokenInvalido_Responde401()
    {
        using var fabrica = Construir();
        using var client = CrearClienteConSesion(fabrica, "token-invalido");

        using var respuesta = await client.GetAsync("/api/admin/diagnostico/lectura");

        respuesta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<CuerpoErrorDto>();
        cuerpo!.Error.Code.Should().Be("UNAUTHENTICATED");
    }

    [Fact]
    public async Task GetAdmin_Visor_Responde200()
    {
        using var fabrica = Construir();
        using var client = CrearClienteConSesion(fabrica, SesionesFake.TokenVisor);

        using var respuesta = await client.GetAsync("/api/admin/diagnostico/lectura");

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PostAdmin_VisorConCsrf_Responde403()
    {
        using var fabrica = Construir();
        using var client = CrearClienteConSesion(fabrica, SesionesFake.TokenVisor);
        using var peticion = new HttpRequestMessage(HttpMethod.Post, "/api/admin/diagnostico/mutacion");
        peticion.Headers.Add("X-CSRF-Token", CsrfVisor);

        using var respuesta = await client.SendAsync(peticion);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<CuerpoErrorDto>();
        cuerpo!.Error.Code.Should().Be("FORBIDDEN");
    }

    [Fact]
    public async Task PostAdmin_AdminSinCsrf_Responde403()
    {
        using var fabrica = Construir();
        using var client = CrearClienteConSesion(fabrica, SesionesFake.TokenAdmin);

        using var respuesta = await client.PostAsync("/api/admin/diagnostico/mutacion", content: null);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<CuerpoErrorDto>();
        cuerpo!.Error.Code.Should().Be("FORBIDDEN");
    }

    [Fact]
    public async Task PostAdmin_AdminConCsrfValido_Responde200()
    {
        using var fabrica = Construir();
        using var client = CrearClienteConSesion(fabrica, SesionesFake.TokenAdmin);
        using var peticion = new HttpRequestMessage(HttpMethod.Post, "/api/admin/diagnostico/mutacion");
        peticion.Headers.Add("X-CSRF-Token", CsrfAdmin);

        using var respuesta = await client.SendAsync(peticion);

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static WebApplicationFactory<Program> Construir()
        => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IServicioSesion, SesionesFake>();
            });
        });

    private static HttpClient CrearClienteConSesion(WebApplicationFactory<Program> fabrica, string token)
    {
        var client = fabrica.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", $"{CookieSesion}={token}");
        return client;
    }

    private sealed record CuerpoErrorDto(ErrorDto Error);

    private sealed record ErrorDto(string Code, string Message);

    private sealed class SesionesFake : IServicioSesion
    {
        public const string TokenAdmin = "token-admin";
        public const string TokenVisor = "token-visor";

        public Task<SesionEmitida> EmitirAsync(Usuario usuario, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<PrincipalSesion?> ValidarAsync(string token, CancellationToken cancellationToken)
        {
            PrincipalSesion? principal = token switch
            {
                TokenAdmin => new PrincipalSesion(
                    "u_admin",
                    "Admin",
                    RolUsuario.Admin,
                    CsrfAdmin,
                    DateTimeOffset.UtcNow.AddMinutes(30)),
                TokenVisor => new PrincipalSesion(
                    "u_visor",
                    "Visor",
                    RolUsuario.Visor,
                    CsrfVisor,
                    DateTimeOffset.UtcNow.AddMinutes(30)),
                _ => null,
            };

            return Task.FromResult(principal);
        }
    }
}
