using System.Net;
using System.Net.Http.Json;
using ElTejido.Application.Auth;
using ElTejido.Application.Configuracion;
using ElTejido.Application.Usuarios;
using ElTejido.Application.Usuarios.CargaMasiva;
using ElTejido.Domain.Common;
using ElTejido.Domain.Identidad;
using ElTejido.Domain.Usuarios;
using ElTejido.Infrastructure.Usuarios;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace ElTejido.IntegrationTests;

public sealed class AdminConfiguracionEndpointsIntegrationTests
{
    private const string CookieSesion = "eltejido_sesion";
    private const string CsrfAdmin = "csrf-admin";
    private const string CsrfVisor = "csrf-visor";

    [Fact]
    public async Task Usuarios_AdminCreaListaYObtieneUsuario()
    {
        var repositorio = new RepositorioUsuariosMemoria();
        using var fabrica = Construir(repositorio);
        using var client = CrearClienteConSesion(fabrica, SesionesFake.TokenAdmin);

        using var creacion = await EnviarJsonAsync(
            client,
            HttpMethod.Post,
            "/api/admin/usuarios",
            new
            {
                nombre = "ARENAS CHAVES JUAN PABLO",
                numero = "+57 300 111 2233",
                rol = "participante",
                area = "Operaciones",
                empresa = "GHT",
                tags = new[] { "t_area_oper", "t_area_oper" },
                propiedadesDinamicas = new Dictionary<string, object?> { ["cargo"] = "Coordinadora" },
            });

        creacion.StatusCode.Should().Be(HttpStatusCode.Created);
        var creado = await creacion.Content.ReadFromJsonAsync<UsuarioDto>();
        creado!.Id.Should().StartWith("u_");
        creado.Nombre.Should().Be("ARENAS CHAVES JUAN PABLO");
        creado.NombreSaludo.Should().Be("Juan Pablo");
        creado.WhatsappNormalizado.Should().Be("573001112233");
        creado.Rol.Should().Be("participante");
        creado.Estado.Should().Be("activo");
        creado.Tags.Should().Equal("t_area_oper");

        using var listado = await client.GetAsync("/api/admin/usuarios?rol=participante&page=1&pageSize=10");
        listado.StatusCode.Should().Be(HttpStatusCode.OK);
        var pagina = await listado.Content.ReadFromJsonAsync<PaginaUsuariosDto>();
        pagina!.Total.Should().Be(1);
        pagina.Items.Should().ContainSingle(u => u.Id == creado.Id);

        using var detalle = await client.GetAsync($"/api/admin/usuarios/{creado.Id}");
        detalle.StatusCode.Should().Be(HttpStatusCode.OK);
        var obtenido = await detalle.Content.ReadFromJsonAsync<UsuarioDto>();
        obtenido!.Id.Should().Be(creado.Id);
    }

    [Fact]
    public async Task Usuarios_NumeroDuplicado_Responde409()
    {
        var repositorio = new RepositorioUsuariosMemoria(
            CrearUsuario("u_existente", "573001112233"));
        using var fabrica = Construir(repositorio);
        using var client = CrearClienteConSesion(fabrica, SesionesFake.TokenAdmin);

        using var respuesta = await EnviarJsonAsync(
            client,
            HttpMethod.Post,
            "/api/admin/usuarios",
            new
            {
                nombre = "Ana",
                numero = "573001112233",
                rol = "participante",
                area = "Operaciones",
                empresa = "GHT",
            });

        respuesta.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<CuerpoErrorDto>();
        cuerpo!.Error.Code.Should().Be("CONFLICT");
    }

    // --- I-08 v2 (04 §5.1): campos del maestro, reasignacion manual y plantilla ---

    [Fact]
    public async Task Usuarios_AltaConCamposDeLaPlantilla_LosDevuelveEnElDto()
    {
        var repositorio = new RepositorioUsuariosMemoria();
        using var fabrica = Construir(repositorio);
        using var client = CrearClienteConSesion(fabrica, SesionesFake.TokenAdmin);

        using var creacion = await EnviarJsonAsync(
            client,
            HttpMethod.Post,
            "/api/admin/usuarios",
            new
            {
                nombre = "ANA PEREZ",
                numero = "573001112233",
                rol = "participante",
                email = "ana@ght.com",
                empresaId = "AL",
                sede = "FF - ADM",
                cargo = "Coordinadora",
                antiguedadAnios = 16.391666m,
                idioma = "en",
                usuarioWhatsapp = "ana.perez",
            });

        creacion.StatusCode.Should().Be(HttpStatusCode.Created);
        var creado = await creacion.Content.ReadFromJsonAsync<UsuarioDto>();
        // area y empresa ya no son obligatorios: el alta pasa sin ellos (I-08 §3.1.h).
        creado!.CodigoUsuario.Should().Be(1);
        creado.CodigoUsuarioLegible.Should().Be("U-000001");
        creado.Email.Should().Be("ana@ght.com");
        creado.EmpresaId.Should().Be("AL");
        creado.Sede.Should().Be("FF - ADM");
        creado.Cargo.Should().Be("Coordinadora");
        creado.AntiguedadAnios.Should().Be(16.391666m);
        creado.Idioma.Should().Be("en");
        creado.UsuarioWhatsapp.Should().Be("ana.perez");

        using var porEmpresa = await client.GetAsync("/api/admin/usuarios?empresaId=AL&idioma=en");
        var pagina = await porEmpresa.Content.ReadFromJsonAsync<PaginaUsuariosDto>();
        pagina!.Total.Should().Be(1);
    }

    [Fact]
    public async Task Usuarios_BackfillNombreSaludo_PrevisualizaYCompletaDeFormaIdempotente()
    {
        var repositorio = new RepositorioUsuariosMemoria { NombresSaludoFaltantes = 3 };
        using var fabrica = Construir(repositorio);
        using var client = CrearClienteConSesion(fabrica, SesionesFake.TokenAdmin);

        using var previsualizacion = await client.GetAsync("/api/admin/usuarios/nombres-saludo/pendientes");
        var pendientes = await previsualizacion.Content.ReadFromJsonAsync<ConteoPendientesDto>();
        using var ejecucion = await EnviarJsonAsync(
            client,
            HttpMethod.Post,
            "/api/admin/usuarios/nombres-saludo/completar",
            new { });
        var completados = await ejecucion.Content.ReadFromJsonAsync<ConteoCompletadosDto>();
        using var repeticion = await EnviarJsonAsync(
            client,
            HttpMethod.Post,
            "/api/admin/usuarios/nombres-saludo/completar",
            new { });
        var repetidos = await repeticion.Content.ReadFromJsonAsync<ConteoCompletadosDto>();

        previsualizacion.StatusCode.Should().Be(HttpStatusCode.OK);
        pendientes!.Pendientes.Should().Be(3);
        ejecucion.StatusCode.Should().Be(HttpStatusCode.OK);
        completados!.Completados.Should().Be(3);
        repetidos!.Completados.Should().Be(0);
    }

    [Fact]
    public async Task Usuarios_ReasignarNumero_InactivaAlTitularYCreaAlNuevo()
    {
        var repositorio = new RepositorioUsuariosMemoria(CrearUsuario("u_1", "573001112233"));
        using var fabrica = Construir(repositorio);
        using var client = CrearClienteConSesion(fabrica, SesionesFake.TokenAdmin);

        using var respuesta = await EnviarJsonAsync(
            client,
            HttpMethod.Post,
            "/api/admin/usuarios/u_1/reasignar-numero",
            new { nombre = "CARLOS RODRIGUEZ", empresaId = "AL" });

        respuesta.StatusCode.Should().Be(HttpStatusCode.Created);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<ReasignacionDto>();
        cuerpo!.UsuarioIdAnterior.Should().Be("u_1");
        cuerpo.Usuario.Id.Should().NotBe("u_1");
        cuerpo.Usuario.Nombre.Should().Be("CARLOS RODRIGUEZ");
        cuerpo.Usuario.WhatsappNormalizado.Should().Be("573001112233");
        cuerpo.Usuario.Estado.Should().Be("activo");

        // El anterior conserva su numero e historial, inactivo.
        using var detalleAnterior = await client.GetAsync("/api/admin/usuarios/u_1");
        var anterior = await detalleAnterior.Content.ReadFromJsonAsync<UsuarioDto>();
        anterior!.Estado.Should().Be("inactivo");
        anterior.WhatsappNormalizado.Should().Be("573001112233");
    }

    [Fact]
    public async Task Usuarios_DescargaPlantillaVacia()
    {
        var repositorio = new RepositorioUsuariosMemoria();
        using var fabrica = Construir(repositorio);
        using var client = CrearClienteConSesion(fabrica, SesionesFake.TokenAdmin);

        using var respuesta = await client.GetAsync("/api/admin/usuarios/plantilla-carga");

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        respuesta.Content.Headers.ContentType!.MediaType.Should()
            .Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

        // Lo que se descarga tiene que poder volver a entrar por el lector.
        var bytes = await respuesta.Content.ReadAsByteArrayAsync();
        using var contenido = new MemoryStream(bytes);
        var filas = await new LectorXlsxParticipantes().LeerAsync(contenido, CancellationToken.None);
        filas.Should().BeEmpty(); // Cabecera valida y sin datos.
    }

    [Fact]
    public async Task Tags_AdminCreaEInactivaTag()
    {
        var repositorio = new RepositorioUsuariosMemoria();
        using var fabrica = Construir(repositorio);
        using var client = CrearClienteConSesion(fabrica, SesionesFake.TokenAdmin);

        using var creacion = await EnviarJsonAsync(
            client,
            HttpMethod.Post,
            "/api/admin/tags",
            new
            {
                nombre = "Operaciones",
                tipoTag = "area",
                descripcion = "Equipo de operaciones",
            });

        creacion.StatusCode.Should().Be(HttpStatusCode.Created);
        var creado = await creacion.Content.ReadFromJsonAsync<TagDto>();
        creado!.Id.Should().StartWith("t_");
        creado.Estado.Should().Be("activo");

        using var baja = await EnviarJsonAsync<object?>(
            client,
            HttpMethod.Delete,
            $"/api/admin/tags/{creado.Id}",
            body: null);

        baja.StatusCode.Should().Be(HttpStatusCode.OK);
        var inactivo = await baja.Content.ReadFromJsonAsync<TagDto>();
        inactivo!.Id.Should().Be(creado.Id);
        inactivo.Estado.Should().Be("inactivo");
    }

    [Fact]
    public async Task VisorPuedeListarPeroNoCrear()
    {
        var repositorio = new RepositorioUsuariosMemoria(CrearUsuario("u_1", "573001112233"));
        using var fabrica = Construir(repositorio);
        using var client = CrearClienteConSesion(fabrica, SesionesFake.TokenVisor);

        using var listado = await client.GetAsync("/api/admin/usuarios");
        listado.StatusCode.Should().Be(HttpStatusCode.OK);

        using var creacion = await EnviarJsonAsync(
            client,
            HttpMethod.Post,
            "/api/admin/tags",
            new { nombre = "Operaciones", tipoTag = "area" },
            CsrfVisor);

        creacion.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static WebApplicationFactory<Program> Construir(IRepositorioUsuarios repositorio)
        => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(repositorio);
                services.AddSingleton<IServicioSesion, SesionesFake>();
                services.AddScoped<IServicioGestionUsuarios, ServicioGestionUsuarios>();
                services.AddSingleton<IGeneradorPlantillaParticipantes, GeneradorPlantillaParticipantesXlsx>();
            });
        });

    private static HttpClient CrearClienteConSesion(WebApplicationFactory<Program> fabrica, string token)
    {
        var client = fabrica.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", $"{CookieSesion}={token}");
        return client;
    }

    private static Task<HttpResponseMessage> EnviarJsonAsync<T>(
        HttpClient client,
        HttpMethod method,
        string path,
        T body,
        string csrf = CsrfAdmin)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-CSRF-Token", csrf);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return client.SendAsync(request);
    }

    private static Usuario CrearUsuario(string id, string numero)
        => Usuario.Crear(
            id,
            1,
            "Usuario",
            NumeroWhatsApp.FromNormalized(numero),
            RolUsuario.Participante,
            EstadoRegistro.Activo,
            "Operaciones",
            "GHT",
            null,
            null,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch);

    private sealed record PaginaUsuariosDto(IReadOnlyCollection<UsuarioDto> Items, int Page, int PageSize, int Total);

    private sealed record UsuarioDto(
        string Id,
        int CodigoUsuario,
        string CodigoUsuarioLegible,
        string Nombre,
        string NombreSaludo,
        string WhatsappNormalizado,
        string? UsuarioWhatsapp,
        string Rol,
        string Estado,
        string? Area,
        string? Empresa,
        string? EmpresaId,
        string? Sede,
        string? Cargo,
        string? Email,
        decimal? AntiguedadAnios,
        string Idioma,
        IReadOnlyCollection<string> Tags,
        DateTimeOffset CreadoEn,
        DateTimeOffset ActualizadoEn);

    private sealed record ReasignacionDto(
        UsuarioDto Usuario,
        string UsuarioIdAnterior,
        int CodigoUsuarioAnterior);

    private sealed record TagDto(
        string Id,
        string Nombre,
        string TipoTag,
        string? Descripcion,
        string Estado,
        DateTimeOffset CreadoEn);

    private sealed record CuerpoErrorDto(ErrorDto Error);

    private sealed record ConteoPendientesDto(int Pendientes);

    private sealed record ConteoCompletadosDto(int Completados);

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

    private sealed class RepositorioUsuariosMemoria : IRepositorioUsuarios
    {
        private readonly Dictionary<string, Usuario> _usuarios = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Tag> _tags = new(StringComparer.Ordinal);
        private int _ultimoCodigoUsuario;

        public int NombresSaludoFaltantes { get; set; }

        public RepositorioUsuariosMemoria(params Usuario[] usuarios)
        {
            foreach (var usuario in usuarios)
            {
                _usuarios[usuario.Id] = usuario;
            }
        }

        public Task GuardarUsuarioAsync(Usuario usuario, CancellationToken cancellationToken)
        {
            _usuarios[usuario.Id] = usuario;
            return Task.CompletedTask;
        }

        public Task<Usuario?> ObtenerUsuarioPorIdAsync(string id, CancellationToken cancellationToken)
            => Task.FromResult(_usuarios.GetValueOrDefault(id));

        public Task<Usuario?> ObtenerUsuarioPorNumeroAsync(
            NumeroWhatsApp numero,
            CancellationToken cancellationToken)
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

        public Task<IReadOnlyCollection<Usuario>> BuscarUsuariosAsync(
            FiltroUsuarios filtro,
            CancellationToken cancellationToken)
        {
            var consulta = _usuarios.Values.AsEnumerable();
            if (filtro.Rol is not null)
            {
                consulta = consulta.Where(u => u.Rol == filtro.Rol);
            }

            if (filtro.Estado is not null)
            {
                consulta = consulta.Where(u => u.Estado == filtro.Estado);
            }

            if (!string.IsNullOrWhiteSpace(filtro.Area))
            {
                consulta = consulta.Where(u => u.Area == filtro.Area);
            }

            if (!string.IsNullOrWhiteSpace(filtro.Empresa))
            {
                consulta = consulta.Where(u => u.Empresa == filtro.Empresa);
            }

            if (filtro.Tags.Count > 0)
            {
                consulta = consulta.Where(u => filtro.Tags.All(t => u.Tags.Contains(t, StringComparer.Ordinal)));
            }

            if (!string.IsNullOrWhiteSpace(filtro.Busqueda))
            {
                consulta = consulta.Where(u =>
                    u.Nombre.Contains(filtro.Busqueda, StringComparison.OrdinalIgnoreCase)
                    || u.WhatsappNormalizado.Valor.Contains(filtro.Busqueda, StringComparison.Ordinal));
            }

            return Task.FromResult<IReadOnlyCollection<Usuario>>(consulta.ToArray());
        }

        public Task<int> ContarNombresSaludoFaltantesAsync(CancellationToken cancellationToken)
            => Task.FromResult(NombresSaludoFaltantes);

        public Task<int> CompletarNombresSaludoFaltantesAsync(CancellationToken cancellationToken)
        {
            var completados = NombresSaludoFaltantes;
            NombresSaludoFaltantes = 0;
            return Task.FromResult(completados);
        }

        public Task GuardarTagAsync(Tag tag, CancellationToken cancellationToken)
        {
            _tags[tag.Id] = tag;
            return Task.CompletedTask;
        }

        public Task<Tag?> ObtenerTagPorIdAsync(string id, CancellationToken cancellationToken)
            => Task.FromResult(_tags.GetValueOrDefault(id));

        public Task<IReadOnlyCollection<Tag>> BuscarTagsAsync(
            FiltroTags filtro,
            CancellationToken cancellationToken)
        {
            var consulta = _tags.Values.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(filtro.TipoTag))
            {
                consulta = consulta.Where(t => t.TipoTag == filtro.TipoTag);
            }

            if (filtro.Estado is not null)
            {
                consulta = consulta.Where(t => t.Estado == filtro.Estado);
            }

            return Task.FromResult<IReadOnlyCollection<Tag>>(consulta.ToArray());
        }

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
