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
using ElTejido.Infrastructure.Usuarios;
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

    private const string Cabecera =
        "Empresa,ID Empresa,Sede,Nombre,Cargo,Email,Antigüedad en la empresa en años,Idioma,Telefono\n";

    [Fact]
    public async Task CargaMasiva_Admin_CreaUsuariosYReportaPorFila()
    {
        var usuarios = new RepositorioUsuariosMemoria();
        var log = new RepositorioLogSeguridadEspia();
        using var fabrica = Construir(usuarios, log);
        using var client = CrearClienteConSesion(fabrica, SesionesFake.TokenAdmin);

        var csv = Cabecera +
            "Flores El Aljibe,AL,AL,ANA PEREZ,Coordinadora,ana@ght.com,16.391666,es,573001112233\n" +
            ",,,MALA,,,,,no-es-numero\n";

        using var respuesta = await SubirCsvAsync(client, csv, CsrfAdmin);

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        var reporte = await respuesta.Content.ReadFromJsonAsync<ReporteDto>();
        reporte!.TotalFilas.Should().Be(2);
        reporte.Creados.Should().Be(1);
        reporte.Rechazados.Should().Be(1);
        reporte.Filas.Should().Contain(f =>
            f.Resultado == "creado" && f.UsuarioId != null && f.CodigoUsuario == 1);
        reporte.Filas.Should().Contain(f => f.Resultado == "rechazado" && f.Motivo == "numero_invalido");

        // Auditoria sin PII: registra conteos, no numeros.
        log.Registrados.Should().ContainSingle(l => l.Resultado == "carga_masiva");
        log.Registrados.Should().OnlyContain(l => l.Numero == null);
        log.Registrados.Single().Detalle.Should().NotContain("573001112233");
    }

    [Fact]
    public async Task CargaMasiva_Admin_AceptaXlsxConLaPlantillaOficial()
    {
        var usuarios = new RepositorioUsuariosMemoria();
        using var fabrica = Construir(usuarios, new RepositorioLogSeguridadEspia());
        using var client = CrearClienteConSesion(fabrica, SesionesFake.TokenAdmin);
        var xlsx = ConstruirXlsx();

        using var respuesta = await SubirArchivoAsync(
            client,
            xlsx,
            "roster.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            CsrfAdmin);

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        var reporte = await respuesta.Content.ReadFromJsonAsync<ReporteDto>();
        reporte!.Creados.Should().Be(1);
        reporte.Filas.Should().ContainSingle().Which.CodigoUsuario.Should().Be(1);
    }

    [Fact]
    public async Task CargaMasiva_SoloActualizar_ConTelefonoInexistente_NoCreaNada()
    {
        var usuarios = new RepositorioUsuariosMemoria();
        using var fabrica = Construir(usuarios, new RepositorioLogSeguridadEspia());
        using var client = CrearClienteConSesion(fabrica, SesionesFake.TokenAdmin);
        var csv = Cabecera + ",,,ANA PEREZ,,,,,573001112233\n";

        using var respuesta = await SubirCsvAsync(client, csv, CsrfAdmin, modo: "solo_actualizar");

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        var reporte = await respuesta.Content.ReadFromJsonAsync<ReporteDto>();
        reporte!.Creados.Should().Be(0);
        reporte.Filas.Should().ContainSingle().Which.Motivo.Should().Be("no_encontrado");
    }

    [Fact]
    public async Task CargaMasiva_FormatoNoSoportado_Responde400()
    {
        var usuarios = new RepositorioUsuariosMemoria();
        using var fabrica = Construir(usuarios, new RepositorioLogSeguridadEspia());
        using var client = CrearClienteConSesion(fabrica, SesionesFake.TokenAdmin);

        using var respuesta = await SubirArchivoAsync(
            client,
            Encoding.UTF8.GetBytes("cualquier cosa"),
            "roster.txt",
            "text/plain",
            CsrfAdmin);

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CargaMasiva_SinCsrf_Responde403()
    {
        var usuarios = new RepositorioUsuariosMemoria();
        using var fabrica = Construir(usuarios, new RepositorioLogSeguridadEspia());
        using var client = CrearClienteConSesion(fabrica, SesionesFake.TokenAdmin);

        using var respuesta = await SubirCsvAsync(client, Cabecera, csrf: null);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CargaMasiva_SinSesion_Responde401()
    {
        var usuarios = new RepositorioUsuariosMemoria();
        using var fabrica = Construir(usuarios, new RepositorioLogSeguridadEspia());
        using var client = fabrica.CreateClient();

        using var respuesta = await SubirCsvAsync(client, Cabecera, CsrfAdmin);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static Task<HttpResponseMessage> SubirCsvAsync(
        HttpClient client,
        string csv,
        string? csrf,
        string? modo = null)
        => SubirArchivoAsync(client, Encoding.UTF8.GetBytes(csv), "roster.csv", "text/csv", csrf, modo);

    private static Task<HttpResponseMessage> SubirArchivoAsync(
        HttpClient client,
        byte[] bytes,
        string nombreArchivo,
        string tipoContenido,
        string? csrf,
        string? modo = null)
    {
        var contenido = new MultipartFormDataContent();
        var archivo = new ByteArrayContent(bytes);
        archivo.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(tipoContenido);
        contenido.Add(archivo, "archivo", nombreArchivo);
        if (modo is not null)
        {
            contenido.Add(new StringContent(modo), "modo");
        }

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

    /// <summary>Libro minimo con la cabecera oficial y una fila valida, para ejercitar el lector real.</summary>
    private static byte[] ConstruirXlsx()
    {
        using var memoria = new MemoryStream();
        using (var libro = new ClosedXML.Excel.XLWorkbook())
        {
            var hoja = libro.Worksheets.Add("Participantes");
            for (var columna = 0; columna < PlantillaParticipantes.Cabecera.Count; columna++)
            {
                hoja.Cell(1, columna + 1).Value = PlantillaParticipantes.Cabecera[columna];
            }

            hoja.Cell(2, 4).Value = "ANA PEREZ";
            hoja.Cell(2, 9).Value = "573001112233";
            libro.SaveAs(memoria);
        }

        return memoria.ToArray();
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
                // Los dos lectores reales: el servicio elige por extension (I-08 §4.2).
                services.AddSingleton<ILectorArchivoParticipantes, LectorXlsxParticipantes>();
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
        int Reasignados,
        int Rechazados,
        int Asociados,
        IReadOnlyList<FilaDto> Filas);

    private sealed record FilaDto(
        int Fila,
        string Resultado,
        string? UsuarioId,
        string? Motivo,
        int? CodigoUsuario);

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
        private int _ultimoCodigoUsuario;

        public Task GuardarUsuarioAsync(Usuario usuario, CancellationToken cancellationToken)
        {
            _usuarios[usuario.Id] = usuario;
            return Task.CompletedTask;
        }

        public Task<Usuario?> ObtenerUsuarioPorIdAsync(string id, CancellationToken cancellationToken)
            => Task.FromResult(_usuarios.GetValueOrDefault(id));

        public Task<Usuario?> ObtenerUsuarioPorNumeroAsync(NumeroWhatsApp numero, CancellationToken cancellationToken)
            => Task.FromResult(_usuarios.Values.FirstOrDefault(u =>
                u.WhatsappNormalizado.Valor == numero.Valor && u.Estado == EstadoRegistro.Activo));

        public Task<IReadOnlyCollection<Usuario>> ListarUsuariosPorNumeroAsync(
            NumeroWhatsApp numero,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<Usuario>>(_usuarios.Values
                .Where(u => u.WhatsappNormalizado.Valor == numero.Valor)
                .OrderBy(u => u.CreadoEn)
                .ToArray());

        public Task<int> ReservarCodigosUsuarioAsync(int cantidad, CancellationToken cancellationToken)
        {
            var primero = _ultimoCodigoUsuario + 1;
            _ultimoCodigoUsuario += cantidad;
            return Task.FromResult(primero);
        }

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

        public Task<int> EliminarUsuariosNoAdministrativosAsync(CancellationToken cancellationToken)
        {
            var aBorrar = _usuarios.Values.Where(u => !u.EsAdministrativo).ToArray();
            foreach (var usuario in aBorrar)
            {
                _usuarios.Remove(usuario.Id);
            }

            return Task.FromResult(aBorrar.Length);
        }
    }
}
