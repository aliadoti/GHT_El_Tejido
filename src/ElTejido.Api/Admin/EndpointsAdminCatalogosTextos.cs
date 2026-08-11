using System.Text;
using System.Text.Json;
using ElTejido.Application.Common;
using ElTejido.Application.Configuracion;
using ElTejido.Application.Conversacion;
using ElTejido.Domain.Configuracion;

namespace ElTejido.Api.Admin;

internal static class EndpointsAdminCatalogosTextos
{
    public static IEndpointRouteBuilder MapearEndpointsAdminCatalogosTextos(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/admin/catalogos-textos")
            .AddEndpointFilter<AutorizacionAdminEndpointFilter>();

        grupo.MapGet("", BuscarAsync);
        grupo.MapPost("", CrearAsync);
        grupo.MapPost("/importar", ImportarAsync);
        grupo.MapPost("/semillas/{idioma}", CrearDesdeSemillaAsync);
        grupo.MapGet("/efectivo", ObtenerEfectivoAsync);
        grupo.MapGet("/{familiaId}/{idioma}/versiones", ListarVersionesAsync);
        grupo.MapPost("/{familiaId}/{idioma}/versiones", CrearVersionAsync);
        grupo.MapPut("/{familiaId}/{idioma}/versiones/{version:int}", ActualizarAsync);
        grupo.MapPost("/{familiaId}/{idioma}/versiones/{version:int}/activar", ActivarAsync);
        grupo.MapGet("/{familiaId}/{idioma}/versiones/{version:int}/exportar", ExportarAsync);

        return app;
    }

    private static async Task<IResult> BuscarAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var idioma = context.Request.Query["idioma"].ToString();
        var estado = ParsearEstado(context.Request.Query["estado"].ToString());
        var items = await Servicio(context).BuscarAsync(idioma, estado, cancellationToken);
        return Results.Ok(items.Select(Mapear).ToArray());
    }

    private static async Task<IResult> ListarVersionesAsync(
        string familiaId,
        string idioma,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var items = await Servicio(context).ListarVersionesAsync(familiaId, idioma, cancellationToken);
        return Results.Ok(items.Select(Mapear).ToArray());
    }

    private static async Task<IResult> CrearAsync(
        GuardarCatalogoRequest request,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var creado = await Servicio(context).CrearAsync(
            new SolicitudGuardarCatalogoTextos(
                request.FamiliaId ?? string.Empty,
                request.Idioma ?? string.Empty,
                request.Mensajes ?? new Dictionary<string, string>(),
                request.Frases ?? new Dictionary<string, IReadOnlyCollection<string>>()),
            Actor(context),
            cancellationToken);
        context.Response.Headers.ETag = creado.Etag;
        return Results.Created(
            $"/api/admin/catalogos-textos/{creado.Catalogo.FamiliaId}/{creado.Catalogo.Idioma}/versiones/{creado.Catalogo.Version}",
            Mapear(creado));
    }

    private static async Task<IResult> ImportarAsync(
        GuardarCatalogoRequest request,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var creado = await Servicio(context).ImportarAsync(
            new SolicitudGuardarCatalogoTextos(
                request.FamiliaId ?? string.Empty,
                request.Idioma ?? string.Empty,
                request.Mensajes ?? new Dictionary<string, string>(),
                request.Frases ?? new Dictionary<string, IReadOnlyCollection<string>>()),
            Actor(context),
            cancellationToken);
        context.Response.Headers.ETag = creado.Etag;
        return Results.Created(
            $"/api/admin/catalogos-textos/{creado.Catalogo.FamiliaId}/{creado.Catalogo.Idioma}/versiones/{creado.Catalogo.Version}",
            Mapear(creado));
    }

    private static async Task<IResult> CrearVersionAsync(
        string familiaId,
        string idioma,
        ContenidoCatalogoRequest? request,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        SolicitudContenidoCatalogoTextos? contenido = request is null
            ? null
            : new SolicitudContenidoCatalogoTextos(
                request.Mensajes ?? new Dictionary<string, string>(),
                request.Frases ?? new Dictionary<string, IReadOnlyCollection<string>>());
        var creado = await Servicio(context).CrearVersionAsync(
            familiaId,
            idioma,
            contenido,
            Actor(context),
            cancellationToken);
        context.Response.Headers.ETag = creado.Etag;
        return Results.Created(
            $"/api/admin/catalogos-textos/{familiaId}/{idioma}/versiones/{creado.Catalogo.Version}",
            Mapear(creado));
    }

    private static async Task<IResult> ActualizarAsync(
        string familiaId,
        string idioma,
        int version,
        ContenidoCatalogoRequest request,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var actualizado = await Servicio(context).ActualizarBorradorAsync(
            familiaId,
            idioma,
            version,
            new SolicitudContenidoCatalogoTextos(
                request.Mensajes ?? new Dictionary<string, string>(),
                request.Frases ?? new Dictionary<string, IReadOnlyCollection<string>>()),
            RequerirIfMatch(context),
            Actor(context),
            cancellationToken);
        context.Response.Headers.ETag = actualizado.Etag;
        return Results.Ok(Mapear(actualizado));
    }

    private static async Task<IResult> ActivarAsync(
        string familiaId,
        string idioma,
        int version,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var activo = await Servicio(context).ActivarAsync(
            familiaId,
            idioma,
            version,
            RequerirIfMatch(context),
            Actor(context),
            cancellationToken);
        context.Response.Headers.ETag = activo.Etag;
        return Results.Ok(Mapear(activo));
    }

    private static async Task<IResult> ObtenerEfectivoAsync(
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var idioma = context.Request.Query["idioma"].ToString();
        if (string.IsNullOrWhiteSpace(idioma))
        {
            throw new ErrorValidacion(
                "El idioma es obligatorio.",
                new[] { new DetalleError("idioma", "obligatorio") });
        }

        var efectivo = await context.RequestServices.GetRequiredService<IProveedorTextosConversacion>()
            .PrevisualizarAsync(idioma, cancellationToken);
        return Results.Ok(new
        {
            origen = MapearOrigen(efectivo.Origen),
            catalogo = efectivo.Version is null ? null : Mapear(efectivo.Version),
        });
    }

    private static async Task<IResult> CrearDesdeSemillaAsync(
        string idioma,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        SolicitudGuardarCatalogoTextos solicitud;
        try
        {
            solicitud = CatalogosTextosSemilla.CrearSolicitud(
                idioma,
                context.RequestServices.GetRequiredService<OpcionesConversacion>());
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new ErrorValidacion(
                "El idioma debe ser 'es' o 'en'.",
                new[] { new DetalleError("idioma", "valor_invalido") });
        }

        // Importar crea v1 o una version nueva, siempre borrador. Nunca activa la semilla.
        var creado = await Servicio(context).ImportarAsync(
            solicitud,
            Actor(context),
            cancellationToken);
        context.Response.Headers.ETag = creado.Etag;
        return Results.Created(
            $"/api/admin/catalogos-textos/{creado.Catalogo.FamiliaId}/{creado.Catalogo.Idioma}/versiones/{creado.Catalogo.Version}",
            Mapear(creado));
    }

    private static async Task<IResult> ExportarAsync(
        string familiaId,
        string idioma,
        int version,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var catalogo = await Servicio(context).ObtenerAsync(familiaId, idioma, version, cancellationToken);
        var json = JsonSerializer.Serialize(Mapear(catalogo), new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
        });
        return Results.File(
            Encoding.UTF8.GetBytes(json),
            "application/json; charset=utf-8",
            $"catalogo-{familiaId}-{idioma}-v{version}.json");
    }

    private static object Mapear(VersionCatalogoTextos version)
        => new
        {
            familiaId = version.Catalogo.FamiliaId,
            idioma = version.Catalogo.Idioma,
            version = version.Catalogo.Version,
            estado = version.Catalogo.Estado.ToString().ToLowerInvariant(),
            mensajes = version.Catalogo.Mensajes,
            frases = version.Catalogo.Frases,
            creadoPor = version.Catalogo.CreadoPor,
            aprobadoPor = version.Catalogo.AprobadoPor,
            creadoEn = version.Catalogo.CreadoEn,
            actualizadoEn = version.Catalogo.ActualizadoEn,
            activadoEn = version.Catalogo.ActivadoEn,
            huella = version.Catalogo.Huella,
            etag = version.Etag,
        };

    private static string MapearOrigen(OrigenTextosConversacion origen)
        => origen switch
        {
            OrigenTextosConversacion.Legacy => "legacy",
            OrigenTextosConversacion.Catalogo => "catalogo",
            OrigenTextosConversacion.Cache => "cache",
            OrigenTextosConversacion.UltimaVersionValida => "ultimaVersionValida",
            OrigenTextosConversacion.Emergencia => "emergencia",
            _ => throw new InvalidOperationException($"Origen de textos no soportado: {origen}."),
        };

    private static EstadoCatalogoTextos? ParsearEstado(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        if (Enum.TryParse<EstadoCatalogoTextos>(valor, true, out var estado))
        {
            return estado;
        }

        throw new ErrorValidacion(
            "El estado debe ser borrador, activo o inactivo.",
            new[] { new DetalleError("estado", "valor_invalido") });
    }

    private static string RequerirIfMatch(HttpContext context)
    {
        var etag = context.Request.Headers.IfMatch.ToString();
        if (string.IsNullOrWhiteSpace(etag))
        {
            throw new ErrorValidacion(
                "El header If-Match es obligatorio.",
                new[] { new DetalleError("If-Match", "obligatorio") });
        }

        return etag.Trim();
    }

    private static string Actor(HttpContext context)
        => AutorizacionAdminEndpointFilter.ObtenerPrincipal(context).UsuarioId;

    private static IServicioGestionCatalogosTextos Servicio(HttpContext context)
        => context.RequestServices.GetRequiredService<IServicioGestionCatalogosTextos>();

    private sealed record GuardarCatalogoRequest(
        string? FamiliaId,
        string? Idioma,
        IReadOnlyDictionary<string, string>? Mensajes,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>>? Frases);

    private sealed record ContenidoCatalogoRequest(
        IReadOnlyDictionary<string, string>? Mensajes,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>>? Frases);
}
