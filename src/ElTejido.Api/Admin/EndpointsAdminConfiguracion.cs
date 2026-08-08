using System.Text.Json;
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
        usuarios.MapGet("/plantilla-carga", DescargarPlantillaCargaAsync);
        usuarios.MapGet("/{id}", ObtenerUsuarioAsync);
        usuarios.MapPut("/{id}", ActualizarUsuarioAsync);
        usuarios.MapPost("/{id}/reasignar-numero", ReasignarNumeroUsuarioAsync);
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
            busqueda: query["q"].ToString(),
            empresaId: query["empresaId"].ToString(),
            sede: query["sede"].ToString(),
            idioma: query["idioma"].ToString());

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
                // area y empresa dejaron de ser obligatorios con la plantilla oficial (I-08 §3.1.h).
                request.Area,
                request.Empresa,
                request.Tags,
                request.PropiedadesDinamicas,
                request.Email,
                request.EmpresaId,
                request.Sede,
                request.Cargo,
                request.AntiguedadAnios,
                request.Idioma,
                request.UsuarioWhatsapp),
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
        var formatoAdmitido = string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".csv", StringComparison.OrdinalIgnoreCase);
        if (!formatoAdmitido)
        {
            throw new ErrorValidacion(
                "Solo se admiten archivos .xlsx y .csv.",
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
        var modo = ResolverModo(contexto, formulario);
        var resoluciones = ResolverReasignaciones(formulario);

        await using var contenido = archivo.OpenReadStream();
        var reporte = await contexto.RequestServices
            .GetRequiredService<IServicioCargaMasiva>()
            .CargarAsync(archivo.FileName, contenido, campaniaId, modo, resoluciones, cancellationToken);

        return Results.Ok(MapearReporteCargaMasiva(reporte));
    }

    private static string ResolverModo(HttpContext contexto, IFormCollection formulario)
    {
        var desdeQuery = contexto.Request.Query["modo"].ToString();
        if (!string.IsNullOrWhiteSpace(desdeQuery))
        {
            return desdeQuery.Trim();
        }

        var desdeForm = formulario["modo"].ToString();
        return string.IsNullOrWhiteSpace(desdeForm) ? ModoCargaMasiva.Upsert : desdeForm.Trim();
    }

    /// <summary>
    /// Decisiones del admin sobre los conflictos de titular, en la segunda pasada del mismo archivo
    /// (04 §5.1). Viajan como campo del formulario con un arreglo JSON <c>[{fila, accion}]</c>.
    /// </summary>
    private static IReadOnlyCollection<ResolucionConflictoTitular> ResolverReasignaciones(
        IFormCollection formulario)
    {
        var crudo = formulario["reasignaciones"].ToString();
        if (string.IsNullOrWhiteSpace(crudo))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<ResolucionConflictoTitular[]>(
                crudo,
                new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? [];
        }
        catch (JsonException)
        {
            throw new ErrorValidacion(
                "El campo 'reasignaciones' debe ser un arreglo JSON de {fila, accion}.",
                new[] { new DetalleError("reasignaciones", "invalido") });
        }
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
                request.PropiedadesDinamicas,
                request.Email,
                request.EmpresaId,
                request.Sede,
                request.Cargo,
                request.AntiguedadAnios,
                request.Idioma,
                request.UsuarioWhatsapp),
            cancellationToken);

        return Results.Ok(MapearUsuario(usuario));
    }

    /// <summary>
    /// Reasignacion manual del numero (04 §5.1): inactiva al titular y crea uno nuevo con el mismo
    /// numero. Responde <c>201</c> con el usuario nuevo y la identidad del anterior.
    /// </summary>
    private static async Task<IResult> ReasignarNumeroUsuarioAsync(
        string id,
        ReasignarNumeroRequest request,
        HttpContext contexto,
        CancellationToken cancellationToken)
    {
        var resultado = await ResolverServicio(contexto).ReasignarNumeroAsync(
            id,
            new SolicitudReasignarNumero(
                RequerirTexto(request.Nombre, "nombre"),
                request.Email,
                request.EmpresaId,
                request.Sede,
                request.Cargo,
                request.AntiguedadAnios,
                request.Idioma,
                request.UsuarioWhatsapp),
            cancellationToken);

        return Results.Created(
            $"/api/admin/usuarios/{resultado.Nuevo.Id}",
            new
            {
                usuario = MapearUsuario(resultado.Nuevo),
                usuarioIdAnterior = resultado.UsuarioIdAnterior,
                codigoUsuarioAnterior = resultado.CodigoUsuarioAnterior,
            });
    }

    /// <summary>Descarga la plantilla vacia con la cabecera oficial (04 §5.1, I-08 §4.5).</summary>
    private static IResult DescargarPlantillaCargaAsync(HttpContext contexto)
    {
        var generador = contexto.RequestServices.GetRequiredService<IGeneradorPlantillaParticipantes>();
        return Results.File(generador.Generar(), generador.TipoContenido, generador.NombreArchivo);
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
            usuario.CodigoUsuario,
            usuario.CodigoUsuarioLegible,
            usuario.Nombre,
            usuario.WhatsappNormalizado.Valor,
            usuario.UsuarioWhatsapp,
            usuario.Rol.ToString().ToLowerInvariant(),
            usuario.Estado.ToString().ToLowerInvariant(),
            usuario.Area,
            usuario.Empresa,
            usuario.EmpresaId,
            usuario.Sede,
            usuario.Cargo,
            usuario.Email,
            usuario.AntiguedadAnios,
            usuario.Idioma,
            usuario.Tags,
            usuario.PropiedadesDinamicas,
            usuario.CreadoEn,
            usuario.ActualizadoEn);

    private static ReporteCargaMasivaDto MapearReporteCargaMasiva(ReporteCargaMasiva reporte)
        => new(
            reporte.TotalFilas,
            reporte.Creados,
            reporte.Actualizados,
            reporte.Reasignados,
            reporte.Rechazados,
            reporte.Asociados,
            reporte.Filas
                .Select(f => new ResultadoFilaCargaDto(
                    f.Fila,
                    f.Resultado,
                    f.UsuarioId,
                    f.Motivo,
                    f.CodigoUsuario,
                    f.UsuarioIdAnterior,
                    f.CodigoUsuarioAnterior,
                    f.NombreActual,
                    f.NombrePropuesto))
                .ToArray());

    private static TagAdminDto MapearTag(Tag tag)
        => new(
            tag.Id,
            tag.Nombre,
            tag.TipoTag,
            tag.Descripcion,
            tag.Estado.ToString().ToLowerInvariant(),
            tag.CreadoEn);

    /// <summary>
    /// Alta de usuario (04 §5.1). Obligatorios: <c>nombre</c> y <c>numero</c>. <c>codigoUsuario</c> no
    /// se acepta: es de solo lectura y lo asigna el servidor (03 §3.1.1).
    /// </summary>
    private sealed record GuardarUsuarioRequest(
        string? Nombre,
        string? Numero,
        string? Rol,
        string? Estado,
        string? Area,
        string? Empresa,
        IReadOnlyCollection<string>? Tags,
        IReadOnlyDictionary<string, object?>? PropiedadesDinamicas,
        string? Email,
        string? EmpresaId,
        string? Sede,
        string? Cargo,
        decimal? AntiguedadAnios,
        string? Idioma,
        string? UsuarioWhatsapp);

    private sealed record ActualizarUsuarioRequest(
        string? Nombre,
        string? Numero,
        string? Rol,
        string? Estado,
        string? Area,
        string? Empresa,
        IReadOnlyCollection<string>? Tags,
        IReadOnlyDictionary<string, object?>? PropiedadesDinamicas,
        string? Email,
        string? EmpresaId,
        string? Sede,
        string? Cargo,
        decimal? AntiguedadAnios,
        string? Idioma,
        string? UsuarioWhatsapp);

    /// <summary>Reasignacion manual del numero a otra persona (04 §5.1, I-08 §4.4).</summary>
    private sealed record ReasignarNumeroRequest(
        string? Nombre,
        string? Email,
        string? EmpresaId,
        string? Sede,
        string? Cargo,
        decimal? AntiguedadAnios,
        string? Idioma,
        string? UsuarioWhatsapp);

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

    /// <summary>
    /// DTO de usuario de 04 §5.1. Los campos del maestro oficial (<c>codigoUsuario</c>, <c>email</c>,
    /// <c>empresaId</c>, <c>sede</c>, <c>cargo</c>, <c>antiguedadAnios</c>, <c>idioma</c>,
    /// <c>usuarioWhatsapp</c>) se agregan de forma <b>aditiva</b>: un cliente que los ignore sigue
    /// funcionando (I-08 §3.1).
    /// </summary>
    private sealed record UsuarioAdminDto(
        string Id,
        int CodigoUsuario,
        string CodigoUsuarioLegible,
        string Nombre,
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
        int Reasignados,
        int Rechazados,
        int Asociados,
        IReadOnlyCollection<ResultadoFilaCargaDto> Filas);

    /// <summary>
    /// Fila del reporte (04 §5.1). Los campos del titular anterior y los nombres solo vienen en un
    /// <c>conflicto_titular</c> o una reasignacion, para que el portal muestre <i>actual vs. propuesto</i>.
    /// </summary>
    private sealed record ResultadoFilaCargaDto(
        int Fila,
        string Resultado,
        string? UsuarioId,
        string? Motivo,
        int? CodigoUsuario,
        string? UsuarioIdAnterior,
        int? CodigoUsuarioAnterior,
        string? NombreActual,
        string? NombrePropuesto);

    private sealed record RespuestaPaginada<T>(
        IReadOnlyCollection<T> Items,
        int Page,
        int PageSize,
        int Total);
}
