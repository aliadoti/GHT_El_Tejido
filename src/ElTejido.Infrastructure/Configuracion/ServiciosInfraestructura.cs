using Azure.Identity;
using ElTejido.Application.Campanas;
using ElTejido.Application.Configuracion;
using ElTejido.Application.Participantes;
using ElTejido.Application.Seguridad;
using ElTejido.Application.Usuarios;
using ElTejido.Application.WhatsApp;
using ElTejido.Infrastructure.Campanas;
using ElTejido.Infrastructure.Participantes;
using ElTejido.Infrastructure.Seguridad;
using ElTejido.Infrastructure.Usuarios;
using ElTejido.Infrastructure.WhatsApp;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ElTejido.Infrastructure.Configuracion;

/// <summary>
/// Registro de la persistencia Cosmos (Fase 1) de forma <b>guardada</b>: solo cablea
/// <see cref="CosmosClient"/> y los repositorios si hay configuracion Cosmos presente
/// (<c>Cosmos:AccountEndpoint</c>). Asi la app arranca en verde sin emulador y <c>/health</c>
/// sigue funcionando (02 §6, 04 §7).
/// </summary>
public static class ServiciosInfraestructura
{
    public static IServiceCollection AgregarInfraestructura(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var endpoint = configuration["Cosmos:AccountEndpoint"];
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            // Sin configuracion Cosmos no se registra persistencia; la app sigue operativa.
            return services;
        }

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
        services.AddSingleton<IRegistroWebhookDedupe>(sp =>
            new RepositorioWebhookDedupeCosmos(Contenedor(sp, "Leases", "leases")));
        services.AddSingleton<IRepositorioCodigosAuth>(sp =>
            new RepositorioCodigosAuthCosmos(Contenedor(sp, "Security", "security")));
        services.AddSingleton<IRepositorioLogSeguridad>(sp =>
            new RepositorioLogSeguridadCosmos(Contenedor(sp, "Security", "security")));

        return services;
    }
}
