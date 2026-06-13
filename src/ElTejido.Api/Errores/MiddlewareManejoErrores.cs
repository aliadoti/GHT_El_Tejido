using ElTejido.Api.Observabilidad;

namespace ElTejido.Api.Errores;

/// <summary>
/// Captura excepciones no manejadas y responde el modelo de errores uniforme (04 §3) con el
/// estado HTTP correcto y el <c>correlationId</c>. Loguea de forma estructurada (10 §6.3) sin
/// filtrar secretos ni PII (no registra cuerpo ni query string).
/// </summary>
internal sealed class MiddlewareManejoErrores
{
    private readonly RequestDelegate _next;
    private readonly ILogger<MiddlewareManejoErrores> _logger;

    public MiddlewareManejoErrores(RequestDelegate next, ILogger<MiddlewareManejoErrores> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception excepcion)
        {
            var correlationId = AccesorCorrelationId.ObtenerOCrear(context);
            var resultado = MapeadorErrores.Map(excepcion);

            if (resultado.Status >= 500)
            {
                _logger.LogError(
                    excepcion,
                    "Error no controlado {Codigo} {Estado} en {Metodo} {Ruta}",
                    resultado.Code,
                    resultado.Status,
                    context.Request.Method,
                    context.Request.Path);
            }
            else
            {
                _logger.LogWarning(
                    "Error de aplicacion {Codigo} {Estado} en {Metodo} {Ruta}",
                    resultado.Code,
                    resultado.Status,
                    context.Request.Method,
                    context.Request.Path);
            }

            if (context.Response.HasStarted)
            {
                _logger.LogWarning(
                    "La respuesta ya habia comenzado; no se pudo escribir el cuerpo de error {Codigo}.",
                    resultado.Code);
                throw;
            }

            await EscritorRespuestaError.EscribirAsync(context, resultado, correlationId, context.RequestAborted);
        }
    }
}
