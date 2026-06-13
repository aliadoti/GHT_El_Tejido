using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using ElTejido.Application.Seguridad;

namespace ElTejido.Infrastructure.Seguridad;

/// <summary>
/// Proveedor de secretos contra Azure Key Vault por Managed Identity (10 §4, ARQ §10.3).
/// Usa <see cref="DefaultAzureCredential"/> (Managed Identity en Azure; credenciales del
/// desarrollador en local). No guarda credenciales en codigo ni variables de entorno en claro.
/// </summary>
public sealed class KeyVaultSecretProvider : ISecretProvider
{
    private readonly SecretClient _client;

    /// <summary>Construye el cliente real apuntando al Key Vault indicado por su URI.</summary>
    public KeyVaultSecretProvider(Uri keyVaultUri)
        : this(new SecretClient(keyVaultUri, new DefaultAzureCredential()))
    {
    }

    /// <summary>Ctor interno para inyectar un <see cref="SecretClient"/> de prueba.</summary>
    internal KeyVaultSecretProvider(SecretClient client)
    {
        _client = client;
    }

    public async Task<string> ObtenerSecretoAsync(string nombre, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);

        var respuesta = await _client.GetSecretAsync(nombre, cancellationToken: cancellationToken);
        return respuesta.Value.Value;
    }
}
