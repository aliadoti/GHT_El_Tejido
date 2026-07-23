using System.Net;
using System.Net.Http.Json;
using ElTejido.Application.Usuarios;
using ElTejido.Domain.Identidad;
using ElTejido.Domain.Usuarios;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ElTejido.IntegrationTests;

/// <summary>
/// Verifica el gating de los endpoints de simulacion (<c>/diagnostico/simulacion/*</c>) fuera de
/// Development (guia de prueba simulada §7): no se mapean sin <c>Simulacion:Habilitada</c>, y cuando
/// se habilitan exigen la clave de diagnostico (X-Diag-Key). En Development siguen disponibles sin
/// clave (cubierto por el flujo local). Estos endpoints crean admin/emiten OTP, por eso van cerrados.
/// </summary>
public sealed class SimulacionGatingIntegrationTests
{
    private const string Clave = "clave-de-diagnostico-de-pruebas";
    private const string NumeroAdmin = "573001119999";

    [Fact]
    public async Task Produccion_SinHabilitar_NoMapeaSimulacion()
    {
        using var fabrica = Construir(habilitada: false, conClave: true);
        using var client = fabrica.CreateClient();
        client.DefaultRequestHeaders.Add("X-Diag-Key", Clave);

        using var respuesta = await client.PostAsJsonAsync(
            "/diagnostico/simulacion/admin-inicial",
            new { numero = NumeroAdmin, nombre = "Admin" });

        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Produccion_HabilitadaSinClaveCorrecta_Responde404()
    {
        using var fabrica = Construir(habilitada: true, conClave: true);
        using var client = fabrica.CreateClient();
        client.DefaultRequestHeaders.Add("X-Diag-Key", "clave-equivocada");

        using var respuesta = await client.PostAsJsonAsync(
            "/diagnostico/simulacion/admin-inicial",
            new { numero = NumeroAdmin, nombre = "Admin" });

        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Produccion_HabilitadaConClaveCorrecta_CreaAdmin()
    {
        using var fabrica = Construir(habilitada: true, conClave: true);
        using var client = fabrica.CreateClient();
        client.DefaultRequestHeaders.Add("X-Diag-Key", Clave);

        using var respuesta = await client.PostAsJsonAsync(
            "/diagnostico/simulacion/admin-inicial",
            new { numero = NumeroAdmin, nombre = "Admin" });

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<AdminDto>();
        cuerpo!.WhatsappNormalizado.Should().Be(NumeroAdmin);
        cuerpo.Rol.Should().Be("admin");
    }

    private static WebApplicationFactory<Program> Construir(bool habilitada, bool conClave)
        => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                var valores = new Dictionary<string, string?>
                {
                    ["Simulacion:Habilitada"] = habilitada ? "true" : "false",
                };

                if (conClave)
                {
                    valores["Diagnostico:Clave"] = Clave;
                }

                config.AddInMemoryCollection(valores);
            });
            builder.ConfigureTestServices(services =>
            {
                // La simulacion resuelve los repos desde RequestServices; los proveemos en memoria
                // sin depender del modo de persistencia (evita el timing de config del factory).
                services.AddSingleton<IRepositorioUsuarios, UsuariosEnMemoria>();
            });
        });

    private sealed record AdminDto(string Id, string Nombre, string WhatsappNormalizado, string Rol, string Estado);

    private sealed class UsuariosEnMemoria : IRepositorioUsuarios
    {
        private readonly Dictionary<string, Usuario> _porNumero = new();

        public Task<Usuario?> ObtenerUsuarioPorNumeroAsync(NumeroWhatsApp numero, CancellationToken cancellationToken)
            => Task.FromResult(_porNumero.GetValueOrDefault(numero.Valor));

        public Task GuardarUsuarioAsync(Usuario usuario, CancellationToken cancellationToken)
        {
            _porNumero[usuario.WhatsappNormalizado.Valor] = usuario;
            return Task.CompletedTask;
        }

        public Task<Usuario?> ObtenerUsuarioPorIdAsync(string id, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<IReadOnlyCollection<Usuario>> BuscarUsuariosAsync(FiltroUsuarios filtro, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task GuardarTagAsync(Tag tag, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<Tag?> ObtenerTagPorIdAsync(string id, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<IReadOnlyCollection<Tag>> BuscarTagsAsync(FiltroTags filtro, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<int> EliminarUsuariosNoAdministrativosAsync(CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }
}
