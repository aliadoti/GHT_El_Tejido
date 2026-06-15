using ElTejido.Application.Diagnostico;
using ElTejido.Infrastructure.Diagnostico;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ElTejido.Infrastructure.Configuracion;

/// <summary>
/// Registra la verificacion de preparacion (readiness) del despliegue: el agregador
/// <see cref="IServicioPreparacion"/>, el resolutor de la clave de acceso y las comprobaciones por
/// dependencia (secretos, Cosmos, Blob, configuracion de WhatsApp). Alimenta el endpoint
/// <c>/health/ready</c> (guia de Azure §11, 13 §7). No crea recursos: solo verifica los existentes.
/// </summary>
public static class ServiciosDiagnostico
{
    public static IServiceCollection AgregarDiagnostico(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<OpcionesDiagnostico>(configuration.GetSection(OpcionesDiagnostico.Seccion));
        services.AddSingleton<IProveedorClaveDiagnostico, ProveedorClaveDiagnostico>();
        services.AddSingleton<IServicioPreparacion, ServicioPreparacion>();

        // Secretos (Key Vault / config local) y configuracion no secreta de WhatsApp.
        services.AddSingleton<IComprobacionPreparacion, ComprobacionSecretos>();
        services.AddSingleton<IComprobacionPreparacion, ComprobacionWhatsApp>();

        // Cosmos: el cliente solo existe en modo Cosmos; en memoria se reporta NoAplica.
        var database = configuration["Cosmos:DatabaseName"] ?? "eltejido";
        services.AddSingleton<IComprobacionPreparacion>(sp =>
            new ComprobacionCosmos(sp.GetService<CosmosClient>(), database));

        // Blob: si no hay AccountUrl se usa el almacen en memoria (NoAplica).
        var accountUrl = configuration["Blob:AccountUrl"];
        var contenedor = configuration["Blob:ContainerName"] ?? "markdown";
        services.AddSingleton<IComprobacionPreparacion>(_ => new ComprobacionBlob(accountUrl, contenedor));

        return services;
    }
}
