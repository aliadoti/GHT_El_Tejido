using System.Net;
using System.Net.Http.Json;
using ElTejido.Application.Auth;
using ElTejido.Application.Seguridad;
using ElTejido.Application.Usuarios;
using ElTejido.Domain.Common;
using ElTejido.Domain.Identidad;
using ElTejido.Domain.Seguridad;
using ElTejido.Domain.Usuarios;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ElTejido.IntegrationTests;

/// <summary>
/// Prueba el contrato de <c>/api/auth/*</c> (04 §4) sobre la app real, con repositorios en memoria
/// y secretos de prueba (otp-salt/jwt-sign). Cubre el flujo OTP completo, las respuestas neutrales
/// y la sesion por cookie (06 §4).
/// </summary>
public sealed class AuthEndpointsIntegrationTests
{
    private const string NumeroAdmin = "573001119999";
    private const string CodigoFijo = "123456";

    [Fact]
    public async Task FlujoCompleto_SolicitaVerificaConsultaYCierraSesion()
    {
        using var fabrica = Construir(CrearAdmin());
        using var client = fabrica.CreateClient();

        using var solicitud = await client.PostAsJsonAsync("/api/auth/request-code", new { numero = NumeroAdmin });
        solicitud.StatusCode.Should().Be(HttpStatusCode.OK);

        using var verificacion = await client.PostAsJsonAsync(
            "/api/auth/verify-code",
            new { numero = NumeroAdmin, codigo = CodigoFijo });
        verificacion.StatusCode.Should().Be(HttpStatusCode.OK);
        var sesion = await verificacion.Content.ReadFromJsonAsync<RespuestaSesionDto>();
        sesion!.Usuario.Rol.Should().Be("admin");
        sesion.CsrfToken.Should().NotBeNullOrWhiteSpace();
        verificacion.Headers.GetValues("Set-Cookie").Should()
            .Contain(cookie => cookie.Contains("eltejido_sesion") && cookie.ToLowerInvariant().Contains("httponly"));

        using var yo = await client.GetAsync("/api/auth/me");
        yo.StatusCode.Should().Be(HttpStatusCode.OK);
        var cuerpoYo = await yo.Content.ReadFromJsonAsync<RespuestaMeDto>();
        cuerpoYo!.Usuario.Id.Should().Be("u_admin1");

        using var cierre = await client.PostAsync("/api/auth/logout", content: null);
        cierre.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var yoTrasCierre = await client.GetAsync("/api/auth/me");
        yoTrasCierre.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RequestCode_NumeroDesconocido_Responde200Neutral()
    {
        using var fabrica = Construir(CrearAdmin());
        using var client = fabrica.CreateClient();

        using var respuesta = await client.PostAsJsonAsync("/api/auth/request-code", new { numero = "573009998888" });

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<RespuestaMensajeDto>();
        cuerpo!.Message.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task VerifyCode_CodigoIncorrecto_Responde401Neutral()
    {
        using var fabrica = Construir(CrearAdmin());
        using var client = fabrica.CreateClient();

        await client.PostAsJsonAsync("/api/auth/request-code", new { numero = NumeroAdmin });
        using var respuesta = await client.PostAsJsonAsync(
            "/api/auth/verify-code",
            new { numero = NumeroAdmin, codigo = "000000" });

        respuesta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<CuerpoErrorDto>();
        cuerpo!.Error.Code.Should().Be("UNAUTHENTICATED");
    }

    [Fact]
    public async Task RequestCode_SinNumero_Responde400()
    {
        using var fabrica = Construir(CrearAdmin());
        using var client = fabrica.CreateClient();

        using var respuesta = await client.PostAsJsonAsync("/api/auth/request-code", new { numero = "" });

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<CuerpoErrorDto>();
        cuerpo!.Error.Code.Should().Be("VALIDATION_ERROR");
    }

    private static Usuario CrearAdmin()
        => Usuario.Crear(
            "u_admin1",
            "Admin",
            NumeroWhatsApp.FromNormalized(NumeroAdmin),
            RolUsuario.Admin,
            EstadoRegistro.Activo,
            "Operaciones",
            "GHT",
            tags: null,
            propiedadesDinamicas: null,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch);

    private static WebApplicationFactory<Program> Construir(Usuario usuario)
        => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Secretos:otp-salt"] = "pepper-de-pruebas",
                    ["Secretos:jwt-sign"] = "clave-de-firma-de-pruebas-con-mas-de-32-bytes",
                });
            });
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IRepositorioUsuarios>(new FakeUsuarios(usuario));
                services.AddSingleton<IRepositorioCodigosAuth, FakeCodigos>();
                services.AddSingleton<IRepositorioLogSeguridad, FakeLogs>();
                services.AddSingleton<IGeneradorCodigoOtp>(new GeneradorFijo(CodigoFijo));

                // El orquestador esta guardado tras la presencia de Cosmos; aqui se registra con los
                // repositorios en memoria para ejercitar los endpoints sin un almacen real.
                services.AddScoped<IAuthAdminService, AuthAdminService>();
            });
        });

    private sealed record RespuestaSesionDto(UsuarioDto Usuario, string CsrfToken, DateTimeOffset ExpiraEn);

    private sealed record RespuestaMeDto(UsuarioDto Usuario);

    private sealed record UsuarioDto(string Id, string Nombre, string Rol);

    private sealed record RespuestaMensajeDto(string Message);

    private sealed record CuerpoErrorDto(ErrorDto Error);

    private sealed record ErrorDto(string Code, string Message);

    private sealed class FakeUsuarios : IRepositorioUsuarios
    {
        private readonly List<Usuario> _usuarios;

        public FakeUsuarios(params Usuario[] usuarios) => _usuarios = usuarios.ToList();

        public Task<Usuario?> ObtenerUsuarioPorNumeroAsync(NumeroWhatsApp numero, CancellationToken cancellationToken)
            => Task.FromResult(_usuarios.FirstOrDefault(u => u.WhatsappNormalizado.Valor == numero.Valor));

        public Task<Usuario?> ObtenerUsuarioPorIdAsync(string id, CancellationToken cancellationToken)
            => Task.FromResult(_usuarios.FirstOrDefault(u => u.Id == id));

        public Task GuardarUsuarioAsync(Usuario usuario, CancellationToken cancellationToken)
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

    private sealed class FakeCodigos : IRepositorioCodigosAuth
    {
        private readonly Dictionary<string, CodigoAuthAdmin> _porId = new();

        public Task GuardarAsync(CodigoAuthAdmin codigo, CancellationToken cancellationToken)
        {
            _porId[codigo.Id] = codigo;
            return Task.CompletedTask;
        }

        public Task<CodigoAuthAdmin?> ObtenerVigenteMasRecienteAsync(NumeroWhatsApp numero, CancellationToken cancellationToken)
            => Task.FromResult(_porId.Values
                .Where(c => c.Numero.Valor == numero.Valor)
                .OrderByDescending(c => c.CreadoEn)
                .FirstOrDefault());
    }

    private sealed class FakeLogs : IRepositorioLogSeguridad
    {
        public Task RegistrarAsync(LogSeguridad log, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class GeneradorFijo : IGeneradorCodigoOtp
    {
        private readonly string _codigo;

        public GeneradorFijo(string codigo) => _codigo = codigo;

        public string Generar(int longitud) => _codigo;
    }
}
