using System.Net;
using System.Net.Http.Json;
using ElTejido.Application.Auth;
using ElTejido.Application.Configuracion;
using ElTejido.Application.Usuarios;
using ElTejido.Domain.Common;
using ElTejido.Domain.Identidad;
using ElTejido.Domain.Usuarios;
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
                nombre = "Ana Perez",
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
        string Nombre,
        string WhatsappNormalizado,
        string Rol,
        string Estado,
        string Area,
        string Empresa,
        IReadOnlyCollection<string> Tags,
        DateTimeOffset CreadoEn,
        DateTimeOffset ActualizadoEn);

    private sealed record TagDto(
        string Id,
        string Nombre,
        string TipoTag,
        string? Descripcion,
        string Estado,
        DateTimeOffset CreadoEn);

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

    private sealed class RepositorioUsuariosMemoria : IRepositorioUsuarios
    {
        private readonly Dictionary<string, Usuario> _usuarios = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Tag> _tags = new(StringComparer.Ordinal);

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
            => Task.FromResult(_usuarios.Values.FirstOrDefault(u => u.WhatsappNormalizado.Valor == numero.Valor));

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
