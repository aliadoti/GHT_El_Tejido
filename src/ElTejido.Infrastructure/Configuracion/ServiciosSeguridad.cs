using ElTejido.Application.Seguridad;
using ElTejido.Infrastructure.Seguridad;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ElTejido.Infrastructure.Configuracion;

/// <summary>
/// Registro del acceso a secretos (10 §4). Selecciona el proveedor por configuracion:
/// si existe <c>KeyVault:Uri</c> usa Key Vault (Managed Identity); en caso contrario lee de
/// configuracion local (user-secrets). Siempre se envuelve en la cache corta en memoria.
/// </summary>
public static class ServiciosSeguridad
{
    public static IServiceCollection AgregarSeguridad(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMemoryCache();
        services.Configure<OpcionesCacheSecretos>(
            configuration.GetSection(OpcionesCacheSecretos.Seccion));

        var keyVaultUri = configuration["KeyVault:Uri"];

        services.AddSingleton<ISecretProvider>(sp =>
        {
            ISecretProvider interno = string.IsNullOrWhiteSpace(keyVaultUri)
                ? new ConfiguracionSecretProvider(configuration)
                : new KeyVaultSecretProvider(new Uri(keyVaultUri));

            return new SecretProviderConCache(
                interno,
                sp.GetRequiredService<IMemoryCache>(),
                sp.GetRequiredService<IOptions<OpcionesCacheSecretos>>());
        });

        services.AddSingleton<ISecretWriter>(_ => string.IsNullOrWhiteSpace(keyVaultUri)
            ? new SecretWriterMemoria()
            : new KeyVaultSecretWriter(new Uri(keyVaultUri)));

        return services;
    }
}
