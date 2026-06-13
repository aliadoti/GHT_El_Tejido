using System.Globalization;
using System.Threading.RateLimiting;
using ElTejido.Api.Errores;
using ElTejido.Api.Observabilidad;
using Microsoft.AspNetCore.RateLimiting;

namespace ElTejido.Api.Seguridad;

/// <summary>
/// Registro del limitador de tasa con politicas nombradas y configurables (10 §2, §3). El rechazo
/// responde el modelo de errores uniforme (04 §3) con <c>429 RATE_LIMITED</c> y cabecera
/// <c>Retry-After</c>, preservando el <c>correlationId</c>.
/// </summary>
public static class ExtensionesRateLimiting
{
    public static IServiceCollection AgregarLimitadorTasa(
        this IServiceCollection services,
        OpcionesSeguridad opciones)
    {
        services.AddRateLimiter(options =>
        {
            options.AddPolicy(
                PoliticasRateLimiting.Publico,
                context => LimitadorPorIp(context, opciones.RateLimitPublicoPorMinuto));
            options.AddPolicy(
                PoliticasRateLimiting.Webhook,
                context => LimitadorPorIp(context, opciones.RateLimitWebhookPorMinuto));
            options.AddPolicy(
                PoliticasRateLimiting.Demo,
                context => LimitadorPorIp(context, 1));

            options.OnRejected = ManejarRechazoAsync;
        });

        return services;
    }

    private static RateLimitPartition<string> LimitadorPorIp(HttpContext context, int permitidasPorMinuto)
    {
        var clave = context.Connection.RemoteIpAddress?.ToString() ?? "desconocida";

        return RateLimitPartition.GetFixedWindowLimiter(
            clave,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitidasPorMinuto,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            });
    }

    private static async ValueTask ManejarRechazoAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var httpContext = context.HttpContext;
        var correlationId = AccesorCorrelationId.ObtenerOCrear(httpContext);

        var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var valor)
            ? valor
            : TimeSpan.FromMinutes(1);

        if (!httpContext.Response.HasStarted)
        {
            httpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }

        var resultado = new ResultadoMapeoError(
            StatusCodes.Status429TooManyRequests,
            "RATE_LIMITED",
            "Has superado el limite de solicitudes. Intenta de nuevo mas tarde.",
            Array.Empty<CampoErrorRespuesta>());

        await EscritorRespuestaError.EscribirAsync(httpContext, resultado, correlationId, cancellationToken);
    }
}
