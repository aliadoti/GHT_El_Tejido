using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using ElTejido.Application.Seguridad;

namespace ElTejido.Infrastructure.Seguridad;

public sealed class KeyVaultSecretWriter : ISecretWriter
{
    private readonly SecretClient _client;

    public KeyVaultSecretWriter(Uri keyVaultUri)
        : this(new SecretClient(keyVaultUri, new DefaultAzureCredential()))
    {
    }

    internal KeyVaultSecretWriter(SecretClient client)
    {
        _client = client;
    }

    public async Task GuardarSecretoAsync(string nombre, string valor, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        ArgumentException.ThrowIfNullOrWhiteSpace(valor);

        await _client.SetSecretAsync(nombre, valor, cancellationToken);
    }
}
