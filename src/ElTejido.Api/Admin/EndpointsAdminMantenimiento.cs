using ElTejido.Application.Common;
using ElTejido.Application.Mantenimiento;

namespace ElTejido.Api.Admin;

/// <summary>
/// P-15 — purga total de datos de campañas para arrancar pruebas en frío. Expone una sola mutacion
/// destructiva bajo <c>/api/admin/mantenimiento</c>, gateada por el guard admin + CSRF (compartido),
/// el flag operativo <c>Seguridad:PermitirReinicioDatos</c> y una palabra de confirmacion explicita.
/// Borra campañas y todo lo asociado y elimina los usuarios no administrativos; conserva Admin/Visor,
/// Config LLM, Rúbricas, Prompts y Tags. Devuelve el reporte de conteos.
/// </summary>
internal static class EndpointsAdminMantenimiento
{
    private const string PalabraConfirmacion = "ELIMINAR";

    public static IEndpointRouteBuilder MapearEndpointsAdminMantenimiento(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/admin/mantenimiento")
            .AddEndpointFilter<AutorizacionAdminEndpointFilter>();

        grupo.MapPost("/purgar-campanias", PurgarCampaniasAsync);

        return app;
    }

    private static async Task<IResult> PurgarCampaniasAsync(
        PurgarCampaniasRequest? request,
        HttpContext contexto,
        CancellationToken ct)
    {
        if (!PermitirReinicioDatos(contexto))
        {
            throw new ErrorConflicto("La purga de datos esta deshabilitada (Seguridad:PermitirReinicioDatos).");
        }

        var confirmacion = request?.Confirmacion?.Trim();
        if (!string.Equals(confirmacion, PalabraConfirmacion, StringComparison.Ordinal))
        {
            throw new ErrorValidacion(
                $"Debes escribir '{PalabraConfirmacion}' para confirmar la purga total.",
                new[] { new DetalleError("confirmacion", "confirmacion_requerida") });
        }

        var servicio = contexto.RequestServices.GetRequiredService<IServicioPurgaCampanias>();
        var reporte = await servicio.PurgarTodoAsync(ct);
        return Results.Ok(MapearReporte(reporte));
    }

    private static bool PermitirReinicioDatos(HttpContext contexto)
        => contexto.RequestServices.GetRequiredService<IConfiguration>().GetValue("Seguridad:PermitirReinicioDatos", true);

    private static object MapearReporte(ReportePurgaCampanias reporte)
        => new
        {
            reporte.Campanias,
            reporte.Conversaciones,
            reporte.Mensajes,
            reporte.Respuestas,
            reporte.Evaluaciones,
            reporte.Artefactos,
            reporte.BlobsBorrados,
            reporte.BlobsFallidos,
            reporte.Participantes,
            reporte.UsuariosBorrados,
        };

    private sealed record PurgarCampaniasRequest(string? Confirmacion);
}
