using Azure;
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

        try
        {
            var respuesta = await _client.GetSecretAsync(nombre, cancellationToken: cancellationToken);
            return respuesta.Value.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // Normaliza "secreto inexistente" al mismo contrato que ConfiguracionSecretProvider
            // (KeyNotFoundException), para que los consumidores (gateway, webhook) degraden con
            // gracia en vez de propagar un fallo de infraestructura que tumba el flujo. El nombre
            // no es sensible; el valor nunca se incluye. Los demas RequestFailedException (403 por
            // RBAC, red) se propagan a proposito: son errores operativos que deben hacerse visibles.
            throw new KeyNotFoundException($"El secreto '{nombre}' no existe en Key Vault.", ex);
        }
    }
}
