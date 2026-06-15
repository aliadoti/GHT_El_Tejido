using System.Security.Cryptography;
using System.Text;
using ElTejido.Api.Seguridad;
using ElTejido.Application.Diagnostico;

namespace ElTejido.Api.Diagnostico;

/// <summary>
/// Endpoint de preparacion (readiness) <c>GET /health/ready</c> (guia de Azure §11, 13 §7).
/// A diferencia de <c>/health</c> (liveness), verifica que las dependencias externas (Key Vault,
/// Cosmos, Blob, configuracion de WhatsApp) esten configuradas y accesibles. Reporta solo
/// presencia/alcanzabilidad, nunca valores de secretos. Protegido por una clave de diagnostico
/// (header <c>X-Diag-Key</c>): si no hay clave configurada, responde 404 como si no existiera.
/// </summary>
internal static class EndpointsPreparacion
{
    private const string HeaderClave = "X-Diag-Key";

    public static IEndpointRouteBuilder MapearEndpointsPreparacion(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health/ready", VerificarPreparacionAsync)
            .WithName("Readiness")
            .WithSummary("Readiness probe: verifica dependencias externas (protegido por X-Diag-Key).")
            .RequireRateLimiting(PoliticasRateLimiting.Publico);

        return app;
    }

    private static async Task<IResult> VerificarPreparacionAsync(
        HttpContext contexto,
        IProveedorClaveDiagnostico claves,
        IServicioPreparacion preparacion,
        CancellationToken cancellationToken)
    {
        var esperada = await claves.ObtenerClaveEsperadaAsync(cancellationToken);
        var recibida = contexto.Request.Headers[HeaderClave].ToString();

        // Sin clave configurada o sin coincidencia: 404 indistinguible (no revela la postura).
        if (string.IsNullOrEmpty(esperada) || !ClaveCoincide(recibida, esperada))
        {
            return Results.NotFound();
        }

        var reporte = await preparacion.GenerarReporteAsync(cancellationToken);
        var cuerpo = new RespuestaPreparacion(
            Estado(reporte.Estado),
            reporte.Componentes
                .Select(c => new ComponentePreparacion(c.Componente, Estado(c.Estado), c.Detalle))
                .ToArray());

        // 200 si todo OK; 503 si falta o falla alguna dependencia (para monitoreo/alertas).
        return reporte.Estado == EstadoPreparacion.Ok
            ? Results.Ok(cuerpo)
            : Results.Json(cuerpo, statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    private static bool ClaveCoincide(string recibida, string esperada)
    {
        var bytesRecibida = Encoding.UTF8.GetBytes(recibida);
        var bytesEsperada = Encoding.UTF8.GetBytes(esperada);
        return CryptographicOperations.FixedTimeEquals(bytesRecibida, bytesEsperada);
    }

    private static string Estado(EstadoPreparacion estado) => estado switch
    {
        EstadoPreparacion.Ok => "ok",
        EstadoPreparacion.Faltante => "faltante",
        EstadoPreparacion.Error => "error",
        EstadoPreparacion.NoAplica => "no_aplica",
        _ => "desconocido",
    };

    private sealed record RespuestaPreparacion(string Estado, IReadOnlyList<ComponentePreparacion> Componentes);

    private sealed record ComponentePreparacion(string Componente, string Estado, string Detalle);
}
