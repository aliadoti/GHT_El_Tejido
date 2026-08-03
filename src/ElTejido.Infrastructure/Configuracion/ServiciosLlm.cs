using ElTejido.Application.Conversacion;
using ElTejido.Application.Evaluacion;
using ElTejido.Infrastructure.Llm;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ElTejido.Infrastructure.Configuracion;

/// <summary>
/// Registra la Evaluacion con LLM (08). El cliente HTTP (<see cref="ILlmClient"/>) se registra
/// siempre; el evaluador depende de <c>IRepositorioLogSeguridad</c> (Fase 1) y se gatilla con la
/// presencia de <c>Cosmos:AccountEndpoint</c> (registro guardado), igual que el resto de
/// orquestadores, para que la app arranque sin almacen.
/// </summary>
public static class ServiciosLlm
{
    public static IServiceCollection AgregarLlm(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddHttpClient<ILlmClient, LlmClientHttp>();
        services.AddScoped<ISegmentadorIdeas, SegmentadorIdeas>();
        services.AddScoped<IConsolidadorIdeas, ConsolidadorIdeas>();
        services.AddScoped<IClasificadorIntencionControl, ClasificadorIntencionControl>();
        // I-20: el redactor solo da voz al acto que el servidor ya decidió; se registra siempre porque
        // su kill-switch y su respaldo viven en el llamador, no en la disponibilidad del servicio.
        services.AddScoped<IRedactorTurnoConversacional, RedactorTurnoConversacional>();

        if (OpcionesPersistencia.HayAlmacen(configuration))
        {
            services.AddScoped<IEvaluadorLlm, EvaluadorLlm>();
        }

        return services;
    }
}
