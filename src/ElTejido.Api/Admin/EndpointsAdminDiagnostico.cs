namespace ElTejido.Api.Admin;

/// <summary>
/// Endpoints minimos solo para pruebas locales/integracion del guard de <c>/api/admin/*</c>.
/// No implementan CRUD de Fase 4 y no se exponen fuera de Development.
/// </summary>
internal static class EndpointsAdminDiagnostico
{
    public static IEndpointRouteBuilder MapearEndpointsAdminDiagnostico(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/admin/diagnostico")
            .AddEndpointFilter<AutorizacionAdminEndpointFilter>();

        grupo.MapGet("/lectura", () => Results.Ok(new RespuestaAdminDiagnostico("ok")));
        grupo.MapPost("/mutacion", () => Results.Ok(new RespuestaAdminDiagnostico("ok")));

        return app;
    }

    private sealed record RespuestaAdminDiagnostico(string Status);
}
