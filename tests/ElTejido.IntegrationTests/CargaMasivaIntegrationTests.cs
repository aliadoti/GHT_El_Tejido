using System.Net;
using System.Net.Http.Json;
using System.Text;
using ElTejido.Application.Auth;
using ElTejido.Application.Common;
using ElTejido.Application.Configuracion;
using ElTejido.Application.Seguridad;
using ElTejido.Application.Usuarios;
using ElTejido.Application.Usuarios.CargaMasiva;
using ElTejido.Domain.Common;
using ElTejido.Domain.Identidad;
using ElTejido.Domain.Seguridad;
using ElTejido.Domain.Usuarios;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace ElTejido.IntegrationTests;

/// <summary>
/// I-08 — endpoint <c>POST /api/admin/usuarios/carga-masiva</c>: exige sesion admin + CSRF, hace
/// upsert desde el CSV con reporte por fila, y audita sin PII (04 §5.1).
/// </summary>
public sealed class CargaMasivaIntegrationTests
{
    private const string CookieSesion = "eltejido_sesion";
    private const string CsrfAdmin = "csrf-admin";

    [Fact]
    public async Task CargaMasiva_Admin_CreaUsuariosYReportaPorFila()
    {
        var usuarios = new RepositorioUsuariosMemoria();
        var log = new RepositorioLogSeguridadEspia();
        using var fabrica = Construir(usuarios, log);
        using var client = CrearClienteConSesion(fabrica, SesionesFake.TokenAdmin);

        var csv =
            "Nombre,WhatsApp,Area,Empresa,Tags\n" +
            "Ana,573001112233,Ops,GHT,t_area\n" +
            "Mala,no-es-numero,Ops,GHT,\n";

        using var respuesta = await SubirCsvAsync(client, csv, CsrfAdmin);

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        var reporte = await respuesta.Content.ReadFromJsonAsync<ReporteDto>();
        reporte!.TotalFilas.Should().Be(2);
        reporte.Creados.Should().Be(1);
        reporte.Rechazados.Should().Be(1);
        reporte.Filas.Should().Contain(f => f.Resultado == "creado" && f.UsuarioId != null);
        reporte.Filas.Should().Contain(f => f.Resultado == "rechazado" && f.Motivo == "numero_invalido");

        // Auditoria sin PII: registra conteos, no numeros.
        log.Registrados.Should().ContainSingle(l => l.Resultado == "carga_masiva");
        log.Registrados.Should().OnlyContain(l => l.Numero == null);
        log.Registrados.Single().Detalle.Should().NotContain("573001112233");
    }

    [Fact]
    public async Task CargaMasiva_SinCsrf_Responde403()
    {
        var usuarios = new RepositorioUsuariosMemoria();
        using var fabrica = Construir(usuarios, new RepositorioLogSeguridadEspia());
        using var client = CrearClienteConSesion(fabrica, SesionesFake.TokenAdmin);

        using var respuesta = await SubirCsvAsync(client, "Nombre,WhatsApp,Area,Empresa,Tags\n", csrf: null);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CargaMasiva_SinSesion_Responde401()
    {
        var usuarios = new RepositorioUsuariosMemoria();
        using var fabrica = Construir(usuarios, new RepositorioLogSeguridadEspia());
        using var client = fabrica.CreateClient();

        using var respuesta = await SubirCsvAsync(client, "Nombre,WhatsApp,Area,Empresa,Tags\n", CsrfAdmin);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static Task<HttpResponseMessage> SubirCsvAsync(HttpClient client, string csv, string? csrf)
    {
        var contenido = new MultipartFormDataContent();
        var archivo = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        archivo.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        contenido.Add(archivo, "archivo", "roster.csv");

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/usuarios/carga-masiva")
        {
            Content = contenido,
        };
        if (csrf is not null)
        {
            request.Headers.Add("X-CSRF-Token", csrf);
        }

        return client.SendAsync(request);
    }

    private static WebApplicationFactory<Program> Construir(
        IRepositorioUsuarios usuarios,
        IRepositorioLogSeguridad log)
        => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(usuarios);
                services.AddSingleton(log);
                services.AddSingleton<IServicioSesion, SesionesFake>();
                services.AddSingleton<IProveedorCorrelacion, CorrelacionFake>();
                services.AddScoped<IServicioGestionUsuarios, ServicioGestionUsuarios>();
                // La asociacion a campania no se ejercita aqui (campaniaId nulo); un stub basta.
                services.AddSingleton(Substitute.For<IServicioGestionCampanias>());
                services.AddSingleton<ILectorArchivoParticipantes, LectorCsvParticipantes>();
                services.AddScoped<IServicioCargaMasiva, ServicioCargaMasiva>();
            });
        });

    private static HttpClient CrearClienteConSesion(WebApplicationFactory<Program> fabrica, string token)
    {
        var client = fabrica.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", $"{CookieSesion}={token}");
        return client;
    }

    private sealed record ReporteDto(
        int TotalFilas,
        int Creados,
        int Actualizados,
        int Rechazados,
        int Asociados,
        IReadOnlyList<FilaDto> Filas);

    private sealed record FilaDto(int Fila, string Resultado, string? UsuarioId, string? Motivo);

    private sealed class CorrelacionFake : IProveedorCorrelacion
    {
        public string? CorrelationIdActual => "corr_test";
    }

    private sealed class RepositorioLogSeguridadEspia : IRepositorioLogSeguridad
    {
        public List<LogSeguridad> Registrados { get; } = new();

        public Task RegistrarAsync(LogSeguridad log, CancellationToken cancellationToken)
        {
            Registrados.Add(log);
            return Task.CompletedTask;
        }
    }

    private sealed class SesionesFake : IServicioSesion
    {
        public const string TokenAdmin = "token-admin";

        public Task<SesionEmitida> EmitirAsync(Usuario usuario, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<PrincipalSesion?> ValidarAsync(string token, CancellationToken cancellationToken)
        {
            PrincipalSesion? principal = token == TokenAdmin
                ? new PrincipalSesion("u_admin", "Admin", RolUsuario.Admin, CsrfAdmin, DateTimeOffset.UtcNow.AddMinutes(30))
                : null;
            return Task.FromResult(principal);
        }
    }

    private sealed class RepositorioUsuariosMemoria : IRepositorioUsuarios
    {
        private readonly Dictionary<string, Usuario> _usuarios = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Tag> _tags = new(StringComparer.Ordinal);

        public Task GuardarUsuarioAsync(Usuario usuario, CancellationToken cancellationToken)
        {
            _usuarios[usuario.Id] = usuario;
            return Task.CompletedTask;
        }

        public Task<Usuario?> ObtenerUsuarioPorIdAsync(string id, CancellationToken cancellationToken)
            => Task.FromResult(_usuarios.GetValueOrDefault(id));

        public Task<Usuario?> ObtenerUsuarioPorNumeroAsync(NumeroWhatsApp numero, CancellationToken cancellationToken)
            => Task.FromResult(_usuarios.Values.FirstOrDefault(u => u.WhatsappNormalizado.Valor == numero.Valor));

        public Task<IReadOnlyCollection<Usuario>> BuscarUsuariosAsync(FiltroUsuarios filtro, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<Usuario>>(_usuarios.Values.ToArray());

        public Task GuardarTagAsync(Tag tag, CancellationToken cancellationToken)
        {
            _tags[tag.Id] = tag;
            return Task.CompletedTask;
        }

        public Task<Tag?> ObtenerTagPorIdAsync(string id, CancellationToken cancellationToken)
            => Task.FromResult(_tags.GetValueOrDefault(id));

        public Task<IReadOnlyCollection<Tag>> BuscarTagsAsync(FiltroTags filtro, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<Tag>>(_tags.Values.ToArray());
    }
}
