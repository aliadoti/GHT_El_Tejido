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

        if (OpcionesPersistencia.HayAlmacen(configuration))
        {
            services.AddScoped<IEvaluadorLlm, EvaluadorLlm>();
        }

        return services;
    }
}
