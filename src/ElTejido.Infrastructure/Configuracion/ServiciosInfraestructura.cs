using Azure.Identity;
using ElTejido.Application.Campanas;
using ElTejido.Application.Configuracion;
using ElTejido.Application.Conversacion;
using ElTejido.Application.Participantes;
using ElTejido.Application.Respuestas;
using ElTejido.Application.Seguridad;
using ElTejido.Application.Usuarios;
using ElTejido.Application.WhatsApp;
using ElTejido.Infrastructure.Campanas;
using ElTejido.Infrastructure.Conversaciones;
using ElTejido.Infrastructure.Participantes;
using ElTejido.Infrastructure.Persistencia.Memoria;
using ElTejido.Infrastructure.Respuestas;
using ElTejido.Infrastructure.Seguridad;
using ElTejido.Infrastructure.Usuarios;
using ElTejido.Infrastructure.WhatsApp;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ElTejido.Infrastructure.Configuracion;

/// <summary>
/// Registra la persistencia de la aplicacion. Soporta dos modos seleccionables por
/// <c>Persistencia:Modo</c>: <c>Cosmos</c> (default si hay <c>Cosmos:AccountEndpoint</c>) y
/// <c>Memoria</c> (volatil, para correr el portal localmente, p. ej. la pagina de simulacion).
/// Cuando no hay almacen configurado la app sigue arrancando para <c>/health</c> (02 §6, 04 §7).
/// </summary>
public static class ServiciosInfraestructura
{
    public static IServiceCollection AgregarInfraestructura(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        if (OpcionesPersistencia.EsMemoria(configuration))
        {
            return RegistrarMemoria(services);
        }

        var endpoint = configuration["Cosmos:AccountEndpoint"];
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return services;
        }

        return RegistrarCosmos(services, configuration, endpoint);
    }

    private static IServiceCollection RegistrarMemoria(IServiceCollection services)
    {
        services.AddSingleton<IRepositorioUsuarios, RepositorioUsuariosMemoria>();
        services.AddSingleton<IRepositorioCampanias, RepositorioCampaniasMemoria>();
        services.AddSingleton<IRepositorioParticipantes, RepositorioParticipantesMemoria>();
        services.AddSingleton<IRepositorioConfiguracion, RepositorioConfiguracionMemoria>();
        services.AddSingleton<IRepositorioRespuestas, RepositorioRespuestasMemoria>();
        services.AddSingleton<IRepositorioConversaciones, RepositorioConversacionesMemoria>();
        services.AddSingleton<IRegistroWebhookDedupe, RegistroWebhookDedupeMemoria>();
        services.AddSingleton<IRepositorioCodigosAuth, RepositorioCodigosAuthMemoria>();
        services.AddSingleton<IRepositorioLogSeguridad, RepositorioLogSeguridadMemoria>();
        return services;
    }

    private static IServiceCollection RegistrarCosmos(
        IServiceCollection services,
        IConfiguration configuration,
        string endpoint)
    {
        var database = configuration["Cosmos:DatabaseName"] ?? "eltejido";
        var accountKey = configuration["Cosmos:AccountKey"];

        services.AddSingleton(_ => string.IsNullOrWhiteSpace(accountKey)
            ? new CosmosClient(endpoint, new DefaultAzureCredential())
            : new CosmosClient(endpoint, accountKey));

        Container Contenedor(IServiceProvider sp, string clave, string porDefecto) =>
            sp.GetRequiredService<CosmosClient>()
                .GetContainer(database, configuration[$"Cosmos:Containers:{clave}"] ?? porDefecto);

        services.AddSingleton<IRepositorioUsuarios>(sp =>
            new RepositorioUsuariosCosmos(Contenedor(sp, "Users", "users")));
        services.AddSingleton<IRepositorioCampanias>(sp =>
            new RepositorioCampaniasCosmos(Contenedor(sp, "Campaigns", "campaigns")));
        services.AddSingleton<IRepositorioParticipantes>(sp =>
            new RepositorioParticipantesCosmos(Contenedor(sp, "Participants", "participants")));
        services.AddSingleton<IRepositorioConfiguracion>(sp =>
            new RepositorioConfiguracionCosmos(Contenedor(sp, "Config", "config")));
        services.AddSingleton<IRepositorioRespuestas>(sp =>
            new RepositorioRespuestasCosmos(Contenedor(sp, "Responses", "responses")));
        services.AddSingleton<IRepositorioConversaciones>(sp =>
            new RepositorioConversacionesCosmos(Contenedor(sp, "Conversations", "conversations")));
        services.AddSingleton<IRegistroWebhookDedupe>(sp =>
            new RepositorioWebhookDedupeCosmos(Contenedor(sp, "Leases", "leases")));
        services.AddSingleton<IRepositorioCodigosAuth>(sp =>
            new RepositorioCodigosAuthCosmos(Contenedor(sp, "Security", "security")));
        services.AddSingleton<IRepositorioLogSeguridad>(sp =>
            new RepositorioLogSeguridadCosmos(Contenedor(sp, "Security", "security")));

        return services;
    }
}
