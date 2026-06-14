using ElTejido.Application.Conversacion;
using ElTejido.Application.WhatsApp;
using ElTejido.Infrastructure.WhatsApp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ElTejido.Infrastructure.Configuracion;

/// <summary>
/// Registra el WhatsApp Gateway y el procesamiento asincrono in-process (05 §2, 02 §5). Las piezas
/// sin dependencia de persistencia (gateway, colas, almacen de jobs, orquestador provisional y los
/// trabajadores) se registran siempre; los procesadores y el servicio de envios dependen de los
/// repositorios Cosmos (Fase 1) y se gatillan con la presencia de <c>Cosmos:AccountEndpoint</c>,
/// igual que el resto de orquestadores (registro guardado).
/// </summary>
public static class ServiciosWhatsApp
{
    public static IServiceCollection AgregarWhatsApp(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.Configure<OpcionesWhatsApp>(configuration.GetSection(OpcionesWhatsApp.Seccion));

        services.AddHttpClient<IWhatsAppGateway, WhatsAppGateway>();

        services.AddSingleton<ColaWebhookCanal>();
        services.AddSingleton<IColaWebhook>(sp => sp.GetRequiredService<ColaWebhookCanal>());
        services.AddSingleton<ColaEnviosCanal>();
        services.AddSingleton<IColaEnvios>(sp => sp.GetRequiredService<ColaEnviosCanal>());
        services.AddSingleton<IAlmacenJobs, AlmacenJobsMemoria>();

        services.AddHostedService<TrabajadorWebhook>();
        services.AddHostedService<TrabajadorEnvios>();

        if (OpcionesPersistencia.HayAlmacen(configuration))
        {
            // El orquestador real (05 §4) consume los repos (Cosmos o Memoria), evaluador (08) y
            // compilador (09); se gatilla con la presencia de un almacen, igual que el resto.
            services.AddScoped<IOrquestadorConversacion, OrquestadorConversacion>();
            services.AddScoped<ProcesadorWebhookEntrante>();
            services.AddScoped<ProcesadorEnvio>();
            services.AddScoped<IServicioEnvios, ServicioEnvios>();
        }

        return services;
    }
}
