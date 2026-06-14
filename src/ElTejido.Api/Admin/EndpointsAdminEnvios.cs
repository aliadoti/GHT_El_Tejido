using ElTejido.Application.WhatsApp;

namespace ElTejido.Api.Admin;

/// <summary>
/// Endpoints administrativos de envio masivo (04 §5.4) y consulta de jobs (04 §5.9). Reutilizan el
/// guard comun <see cref="AutorizacionAdminEndpointFilter"/> (sesion + rol + CSRF). El disparo
/// responde <c>202 Accepted</c> con el <c>jobId</c> y el procesamiento ocurre en cola (05 §2.5).
/// </summary>
internal static class EndpointsAdminEnvios
{
    public static IEndpointRouteBuilder MapearEndpointsAdminEnvios(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/admin")
            .AddEndpointFilter<AutorizacionAdminEndpointFilter>();

        var envios = grupo.MapGroup("/campanias/{id}/envios");
        envios.MapPost("", EncolarAsync);
        envios.MapPost("/reenviar", ReenviarAsync);
        envios.MapPost("/reintentar", ReintentarAsync);
        envios.MapGet("", ConsultarEstadoAsync);

        grupo.MapGet("/jobs/{jobId}", ObtenerJobAsync);

        return app;
    }

    private static async Task<IResult> EncolarAsync(
        string id,
        EnviosRequest? request,
        HttpContext contexto,
        CancellationToken ct)
    {
        var resultado = await Servicio(contexto).EncolarInicialesAsync(
            id,
            request?.Participantes,
            request?.MensajeInicialId,
            ct);
        return Aceptado(resultado);
    }

    private static async Task<IResult> ReenviarAsync(
        string id,
        ReenvioRequest? request,
        HttpContext contexto,
        CancellationToken ct)
    {
        var resultado = await Servicio(contexto).ReenviarSinRespuestaAsync(id, request?.MensajeInicialId, ct);
        return Aceptado(resultado);
    }

    private static async Task<IResult> ReintentarAsync(
        string id,
        ReenvioRequest? request,
        HttpContext contexto,
        CancellationToken ct)
    {
        var resultado = await Servicio(contexto).ReintentarErroresAsync(id, request?.MensajeInicialId, ct);
        return Aceptado(resultado);
    }

    private static async Task<IResult> ConsultarEstadoAsync(string id, HttpContext contexto, CancellationToken ct)
    {
        var estados = await Servicio(contexto).ConsultarEstadoAsync(id, ct);
        return Results.Ok(estados.Select(estado => new
        {
            estado.UsuarioId,
            numero = estado.Numero,
            estadoEnvio = estado.EstadoEnvio,
            estadoRespuesta = estado.EstadoRespuesta,
            estado.Error,
        }));
    }

    private static IResult ObtenerJobAsync(string jobId, HttpContext contexto)
    {
        var almacen = contexto.RequestServices.GetRequiredService<IAlmacenJobs>();
        var job = almacen.ObtenerJob(jobId);
        if (job is null)
        {
            return Results.NotFound(new { error = new { code = "NOT_FOUND", message = "El job no existe." } });
        }

        return Results.Ok(new
        {
            jobId = job.Id,
            job.CampaniaId,
            job.Encolados,
            job.Enviados,
            job.Errores,
            estado = job.Estado == EstadoJob.Completado ? "completado" : "enProceso",
            job.CreadoEn,
        });
    }

    private static IResult Aceptado(ResultadoEncolarEnvio resultado)
        => Results.Json(
            new { jobId = resultado.JobId, encolados = resultado.Encolados, estado = resultado.Estado },
            statusCode: StatusCodes.Status202Accepted);

    private static IServicioEnvios Servicio(HttpContext contexto)
        => contexto.RequestServices.GetRequiredService<IServicioEnvios>();

    private sealed record EnviosRequest(IReadOnlyCollection<string>? Participantes, string? MensajeInicialId);

    private sealed record ReenvioRequest(string? MensajeInicialId);
}
