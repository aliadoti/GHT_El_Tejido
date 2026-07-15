using ElTejido.Application.Common;
using ElTejido.Application.Configuracion;
using ElTejido.Application.Usuarios;
using ElTejido.Application.Usuarios.CargaMasiva;
using ElTejido.Domain.Common;
using ElTejido.Domain.Usuarios;
using Microsoft.Extensions.Primitives;

namespace ElTejido.Api.Admin;

internal static class EndpointsAdminConfiguracion
{
    private const int PaginaPorDefecto = 1;
    private const int TamanoPaginaPorDefecto = 25;
    private const int TamanoPaginaMaximo = 100;
    private const long TamanoCargaMasivaPorDefecto = 2 * 1024 * 1024; // 2 MB (I-08 §3.1).

    public static IEndpointRouteBuilder MapearEndpointsAdminConfiguracion(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/admin")
            .AddEndpointFilter<AutorizacionAdminEndpointFilter>();

        var usuarios = grupo.MapGroup("/usuarios");
        usuarios.MapGet("", ListarUsuariosAsync);
        usuarios.MapPost("", CrearUsuarioAsync);
        // I-08: carga masiva desde archivo (multipart). El CSRF lo exige el filtro admin (04 §5.1);
        // se desactiva la antiforgery automatica de forms de minimal API porque el MVP usa el header
        // X-CSRF-Token propio, no el token antiforgery.
        usuarios.MapPost("/carga-masiva", CargaMasivaUsuariosAsync).DisableAntiforgery();
        usuarios.MapGet("/{id}", ObtenerUsuarioAsync);
        usuarios.MapPut("/{id}", ActualizarUsuarioAsync);
        usuarios.MapPatch("/{id}/estado", CambiarEstadoUsuarioDesdeRequestAsync);
        usuarios.MapDelete("/{id}", InactivarUsuarioAsync);

        var tags = grupo.MapGroup("/tags");
        tags.MapGet("", ListarTagsAsync);
        tags.MapPost("", CrearTagAsync);
        tags.MapGet("/{id}", ObtenerTagAsync);
        tags.MapPut("/{id}", ActualizarTagAsync);
        tags.MapPatch("/{id}/estado", CambiarEstadoTagDesdeRequestAsync);
        tags.MapDelete("/{id}", InactivarTagAsync);

        return app;
    }

    private static async Task<IResult> ListarUsuariosAsync(HttpContext contexto, CancellationToken cancellationToken)
    {
        var query = contexto.Request.Query;
        var filtro = new FiltroUsuarios(
            rol: ParsearRolOpcional(query["rol"]),
            estado: ParsearEstadoOpcional(query["estado"], "estado"),
            area: query["area"].ToString(),
            empresa: query["empresa"].ToString(),
            tags: ParsearTags(query["tag"], query["tags"]),
            busqueda: query["q"].ToString());

        var servicio = ResolverServicio(contexto);
        var usuarios = await servicio.BuscarUsuariosAsync(filtro, cancellationToken);
        var pagina = Paginar(
            usuarios.Select(MapearUsuario).ToArray(),
            query["page"],
            query["pageSize"]);

        return Results.Ok(pagina);
    }

    private static async Task<IResult> CrearUsuarioAsync(
        GuardarUsuarioRequest request,
        HttpContext contexto,
        CancellationToken cancellationToken)
    {
        var servicio = ResolverServicio(contexto);
        var usuario = await servicio.CrearUsuarioAsync(
            new SolicitudCrearUsuario(
                RequerirTexto(request.Nombre, "nombre"),
                RequerirTexto(request.Numero, "numero"),
                ParsearRolRequerido(request.Rol),
                ParsearEstadoOpcional(request.Estado, "estado") ?? EstadoRegistro.Activo,
                RequerirTexto(request.Area, "area"),
                RequerirTexto(request.Empresa, "empresa"),
                request.Tags,
                request.PropiedadesDinamicas),
            cancellationToken);

        return Results.Created($"/api/admin/usuarios/{usuario.Id}", MapearUsuario(usuario));
    }

    // I-08 (04 §5.1): sube un archivo (CSV en Sprint 1a) y hace upsert por numero normalizado. El
    // parseo/validacion por fila viven en el servicio de aplicacion; aqui solo se valida el transporte
    // (archivo presente, extension .csv, tamano <= limite) y se traduce el reporte a JSON.
    private static async Task<IResult> CargaMasivaUsuariosAsync(
        HttpContext contexto,
        CancellationToken cancellationToken)
    {
        var formulario = await contexto.Request.ReadFormAsync(cancellationToken);
        var archivo = formulario.Files.GetFile("archivo") ?? formulario.Files.FirstOrDefault();
        if (archivo is null || archivo.Length == 0)
        {
            throw new ErrorValidacion(
                "Debe adjuntar un archivo en el campo 'archivo'.",
                new[] { new DetalleError("archivo", "obligatorio") });
        }

        var extension = Path.GetExtension(archivo.FileName);
        if (!string.Equals(extension, ".csv", StringComparison.OrdinalIgnoreCase))
        {
            throw new ErrorValidacion(
                "Solo se admite archivo .csv.",
                new[] { new DetalleError("archivo", "formato_no_soportado") });
        }

        var limite = contexto.RequestServices.GetRequiredService<IConfiguration>()
            .GetValue<long?>("Seguridad:CargaMasivaMaxBytes") ?? TamanoCargaMasivaPorDefecto;
        if (archivo.Length > limite)
        {
            throw new ErrorValidacion(
                $"El archivo excede el limite de {limite} bytes.",
                new[] { new DetalleError("archivo", "archivo_excede_limite") });
        }

        var campaniaId = ResolverCampaniaId(contexto, formulario);

        await using var contenido = archivo.OpenReadStream();
        var reporte = await contexto.RequestServices
            .GetRequiredService<IServicioCargaMasiva>()
            .CargarAsync(archivo.FileName, contenido, campaniaId, cancellationToken);

        return Results.Ok(MapearReporteCargaMasiva(reporte));
    }

    private static string? ResolverCampaniaId(HttpContext contexto, IFormCollection formulario)
    {
        var desdeQuery = contexto.Request.Query["campaniaId"].ToString();
        if (!string.IsNullOrWhiteSpace(desdeQuery))
        {
            return desdeQuery.Trim();
        }

        var desdeForm = formulario["campaniaId"].ToString();
        return string.IsNullOrWhiteSpace(desdeForm) ? null : desdeForm.Trim();
    }

    private static async Task<IResult> ObtenerUsuarioAsync(
        string id,
        HttpContext contexto,
        CancellationToken cancellationToken)
    {
        var usuario = await ResolverServicio(contexto).ObtenerUsuarioAsync(id, cancellationToken);
        return Results.Ok(MapearUsuario(usuario));
    }

    private static async Task<IResult> ActualizarUsuarioAsync(
        string id,
        ActualizarUsuarioRequest request,
        HttpContext contexto,
        CancellationToken cancellationToken)
    {
        var usuario = await ResolverServicio(contexto).ActualizarUsuarioAsync(
            id,
            new SolicitudActualizarUsuario(
                request.Nombre,
                request.Numero,
                ParsearRolOpcional(request.Rol),
                ParsearEstadoOpcional(request.Estado, "estado"),
                request.Area,
                request.Empresa,
                request.Tags,
                request.PropiedadesDinamicas),
            cancellationToken);

        return Results.Ok(MapearUsuario(usuario));
    }

    private static Task<IResult> CambiarEstadoUsuarioDesdeRequestAsync(
        string id,
        CambiarEstadoRequest request,
        HttpContext contexto,
        CancellationToken cancellationToken)
        => AplicarEstadoUsuarioAsync(
            id,
            ParsearEstadoRequerido(request.Estado, "estado"),
            contexto,
            cancellationToken);

    private static async Task<IResult> AplicarEstadoUsuarioAsync(
        string id,
        EstadoRegistro estado,
        HttpContext contexto,
        CancellationToken cancellationToken)
    {
        var usuario = await ResolverServicio(contexto).CambiarEstadoUsuarioAsync(id, estado, cancellationToken);
        return Results.Ok(MapearUsuario(usuario));
    }

    private static Task<IResult> InactivarUsuarioAsync(
        string id,
        HttpContext contexto,
        CancellationToken cancellationToken)
        => AplicarEstadoUsuarioAsync(id, EstadoRegistro.Inactivo, contexto, cancellationToken);

    private static async Task<IResult> ListarTagsAsync(HttpContext contexto, CancellationToken cancellationToken)
    {
        var query = contexto.Request.Query;
        var filtro = new FiltroTags(
            query["tipoTag"].ToString(),
            ParsearEstadoOpcional(query["estado"], "estado"));

        var tags = await ResolverServicio(contexto).BuscarTagsAsync(filtro, cancellationToken);
        var pagina = Paginar(tags.Select(MapearTag).ToArray(), query["page"], query["pageSize"]);
        return Results.Ok(pagina);
    }

    private static async Task<IResult> CrearTagAsync(
        GuardarTagRequest request,
        HttpContext contexto,
        CancellationToken cancellationToken)
    {
        var tag = await ResolverServicio(contexto).CrearTagAsync(
            new SolicitudCrearTag(
                RequerirTexto(request.Nombre, "nombre"),
                RequerirTexto(request.TipoTag, "tipoTag"),
                request.Descripcion,
                ParsearEstadoOpcional(request.Estado, "estado") ?? EstadoRegistro.Activo),
            cancellationToken);

        return Results.Created($"/api/admin/tags/{tag.Id}", MapearTag(tag));
    }

    private static async Task<IResult> ObtenerTagAsync(
        string id,
        HttpContext contexto,
        CancellationToken cancellationToken)
    {
        var tag = await ResolverServicio(contexto).ObtenerTagAsync(id, cancellationToken);
        return Results.Ok(MapearTag(tag));
    }

    private static async Task<IResult> ActualizarTagAsync(
        string id,
        ActualizarTagRequest request,
        HttpContext contexto,
        CancellationToken cancellationToken)
    {
        var tag = await ResolverServicio(contexto).ActualizarTagAsync(
            id,
            new SolicitudActualizarTag(
                request.Nombre,
                request.TipoTag,
                request.Descripcion,
                ParsearEstadoOpcional(request.Estado, "estado")),
            cancellationToken);

        return Results.Ok(MapearTag(tag));
    }

    private static Task<IResult> CambiarEstadoTagDesdeRequestAsync(
        string id,
        CambiarEstadoRequest request,
        HttpContext contexto,
        CancellationToken cancellationToken)
        => AplicarEstadoTagAsync(
            id,
            ParsearEstadoRequerido(request.Estado, "estado"),
            contexto,
            cancellationToken);

    private static async Task<IResult> AplicarEstadoTagAsync(
        string id,
        EstadoRegistro estado,
        HttpContext contexto,
        CancellationToken cancellationToken)
    {
        var tag = await ResolverServicio(contexto).CambiarEstadoTagAsync(id, estado, cancellationToken);
        return Results.Ok(MapearTag(tag));
    }

    private static Task<IResult> InactivarTagAsync(
        string id,
        HttpContext contexto,
        CancellationToken cancellationToken)
        => AplicarEstadoTagAsync(id, EstadoRegistro.Inactivo, contexto, cancellationToken);

    private static IServicioGestionUsuarios ResolverServicio(HttpContext contexto)
        => contexto.RequestServices.GetRequiredService<IServicioGestionUsuarios>();

    private static string RequerirTexto(string? valor, string campo)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            throw new ErrorValidacion(
                $"El campo {campo} es obligatorio.",
                new[] { new DetalleError(campo, "obligatorio") });
        }

        return valor;
    }

    private static RolUsuario ParsearRolRequerido(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            throw new ErrorValidacion(
                "El rol es obligatorio.",
                new[] { new DetalleError("rol", "obligatorio") });
        }

        return ParsearRol(valor);
    }

    private static RolUsuario? ParsearRolOpcional(StringValues valor)
        => ParsearRolOpcional(valor.ToString());

    private static RolUsuario? ParsearRolOpcional(string? valor)
        => string.IsNullOrWhiteSpace(valor) ? null : ParsearRol(valor);

    private static RolUsuario ParsearRol(string valor)
    {
        if (Enum.TryParse<RolUsuario>(valor, ignoreCase: true, out var rol))
        {
            return rol;
        }

        throw new ErrorValidacion(
            "El rol no es valido.",
            new[] { new DetalleError("rol", "valor_invalido") });
    }

    private static EstadoRegistro ParsearEstadoRequerido(string? valor, string campo)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            throw new ErrorValidacion(
                $"El campo {campo} es obligatorio.",
                new[] { new DetalleError(campo, "obligatorio") });
        }

        return ParsearEstado(valor, campo);
    }

    private static EstadoRegistro? ParsearEstadoOpcional(StringValues valor, string campo)
        => ParsearEstadoOpcional(valor.ToString(), campo);

    private static EstadoRegistro? ParsearEstadoOpcional(string? valor, string campo)
        => string.IsNullOrWhiteSpace(valor) ? null : ParsearEstado(valor, campo);

    private static EstadoRegistro ParsearEstado(string valor, string campo)
    {
        if (Enum.TryParse<EstadoRegistro>(valor, ignoreCase: true, out var estado))
        {
            return estado;
        }

        throw new ErrorValidacion(
            $"El campo {campo} no es valido.",
            new[] { new DetalleError(campo, "valor_invalido") });
    }

    private static IReadOnlyCollection<string> ParsearTags(StringValues tag, StringValues tags)
    {
        var resultado = new HashSet<string>(StringComparer.Ordinal);
        foreach (var valor in tag.Concat(tags))
        {
            if (valor is null)
            {
                continue;
            }

            foreach (var item in valor.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (item.Length > 0)
                {
                    resultado.Add(item);
                }
            }
        }

        return resultado.ToArray();
    }

    private static RespuestaPaginada<T> Paginar<T>(IReadOnlyCollection<T> items, StringValues page, StringValues pageSize)
    {
        var numeroPagina = ParsearEnteroPositivo(page, "page", PaginaPorDefecto);
        var tamanoPagina = Math.Min(
            ParsearEnteroPositivo(pageSize, "pageSize", TamanoPaginaPorDefecto),
            TamanoPaginaMaximo);

        return new RespuestaPaginada<T>(
            items.Skip((numeroPagina - 1) * tamanoPagina).Take(tamanoPagina).ToArray(),
            numeroPagina,
            tamanoPagina,
            items.Count);
    }

    private static int ParsearEnteroPositivo(StringValues valor, string campo, int porDefecto)
    {
        var texto = valor.ToString();
        if (string.IsNullOrWhiteSpace(texto))
        {
            return porDefecto;
        }

        if (int.TryParse(texto, out var numero) && numero > 0)
        {
            return numero;
        }

        throw new ErrorValidacion(
            $"El campo {campo} debe ser un entero positivo.",
            new[] { new DetalleError(campo, "entero_positivo") });
    }

    private static UsuarioAdminDto MapearUsuario(Usuario usuario)
        => new(
            usuario.Id,
            usuario.Nombre,
            usuario.WhatsappNormalizado.Valor,
            usuario.Rol.ToString().ToLowerInvariant(),
            usuario.Estado.ToString().ToLowerInvariant(),
            usuario.Area,
            usuario.Empresa,
            usuario.Tags,
            usuario.PropiedadesDinamicas,
            usuario.CreadoEn,
            usuario.ActualizadoEn);

    private static ReporteCargaMasivaDto MapearReporteCargaMasiva(ReporteCargaMasiva reporte)
        => new(
            reporte.TotalFilas,
            reporte.Creados,
            reporte.Actualizados,
            reporte.Rechazados,
            reporte.Asociados,
            reporte.Filas
                .Select(f => new ResultadoFilaCargaDto(f.Fila, f.Resultado, f.UsuarioId, f.Motivo))
                .ToArray());

    private static TagAdminDto MapearTag(Tag tag)
        => new(
            tag.Id,
            tag.Nombre,
            tag.TipoTag,
            tag.Descripcion,
            tag.Estado.ToString().ToLowerInvariant(),
            tag.CreadoEn);

    private sealed record GuardarUsuarioRequest(
        string? Nombre,
        string? Numero,
        string? Rol,
        string? Estado,
        string? Area,
        string? Empresa,
        IReadOnlyCollection<string>? Tags,
        IReadOnlyDictionary<string, object?>? PropiedadesDinamicas);

    private sealed record ActualizarUsuarioRequest(
        string? Nombre,
        string? Numero,
        string? Rol,
        string? Estado,
        string? Area,
        string? Empresa,
        IReadOnlyCollection<string>? Tags,
        IReadOnlyDictionary<string, object?>? PropiedadesDinamicas);

    private sealed record GuardarTagRequest(
        string? Nombre,
        string? TipoTag,
        string? Descripcion,
        string? Estado);

    private sealed record ActualizarTagRequest(
        string? Nombre,
        string? TipoTag,
        string? Descripcion,
        string? Estado);

    private sealed record CambiarEstadoRequest(string? Estado);

    private sealed record UsuarioAdminDto(
        string Id,
        string Nombre,
        string WhatsappNormalizado,
        string Rol,
        string Estado,
        string Area,
        string Empresa,
        IReadOnlyCollection<string> Tags,
        IReadOnlyDictionary<string, object?> PropiedadesDinamicas,
        DateTimeOffset CreadoEn,
        DateTimeOffset ActualizadoEn);

    private sealed record TagAdminDto(
        string Id,
        string Nombre,
        string TipoTag,
        string? Descripcion,
        string Estado,
        DateTimeOffset CreadoEn);

    private sealed record ReporteCargaMasivaDto(
        int TotalFilas,
        int Creados,
        int Actualizados,
        int Rechazados,
        int Asociados,
        IReadOnlyCollection<ResultadoFilaCargaDto> Filas);

    private sealed record ResultadoFilaCargaDto(
        int Fila,
        string Resultado,
        string? UsuarioId,
        string? Motivo);

    private sealed record RespuestaPaginada<T>(
        IReadOnlyCollection<T> Items,
        int Page,
        int PageSize,
        int Total);
}
