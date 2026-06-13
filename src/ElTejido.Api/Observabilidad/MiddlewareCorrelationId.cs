namespace ElTejido.Api.Observabilidad;

/// <summary>
/// Lee <c>X-Correlation-Id</c> entrante o genera <c>corr_&lt;guid&gt;</c>; lo guarda en
/// <c>HttpContext.Items</c>, lo agrega a un scope de logging y lo devuelve en la cabecera de
/// respuesta para que se propague en toda la cadena (04 §8, 10 §6.2).
/// </summary>
internal sealed class MiddlewareCorrelationId
{
    private readonly RequestDelegate _next;
    private readonly ILogger<MiddlewareCorrelationId> _logger;

    public MiddlewareCorrelationId(RequestDelegate next, ILogger<MiddlewareCorrelationId> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolverEntrante(context) ?? AccesorCorrelationId.Generar();

        context.Items[CorrelacionConstantes.ClaveItems] = correlationId;
        context.Response.Headers[CorrelacionConstantes.Header] = correlationId;

        using (_logger.BeginScope(new Dictionary<string, object> { [CorrelacionConstantes.ClaveItems] = correlationId }))
        {
            await _next(context);
        }
    }

    private static string? ResolverEntrante(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(CorrelacionConstantes.Header, out var valor))
        {
            var entrante = valor.ToString();
            if (!string.IsNullOrWhiteSpace(entrante))
            {
                return entrante.Trim();
            }
        }

        return null;
    }
}
