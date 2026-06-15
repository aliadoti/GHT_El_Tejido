using Azure;
using Azure.Security.KeyVault.Secrets;
using ElTejido.Infrastructure.Seguridad;
using FluentAssertions;
using NSubstitute;

namespace ElTejido.UnitTests.Seguridad;

/// <summary>
/// Verifica que el proveedor de Key Vault normalice "secreto inexistente" (HTTP 404) al mismo
/// contrato (<see cref="KeyNotFoundException"/>) que el proveedor local, para que los consumidores
/// (gateway de WhatsApp, webhook) degraden con gracia; y que propague los demas fallos de acceso
/// (403 por RBAC) como errores operativos visibles.
/// </summary>
public sealed class KeyVaultSecretProviderTests
{
    [Fact]
    public async Task ObtenerSecreto_Inexistente404_LanzaKeyNotFound()
    {
        var client = Substitute.For<SecretClient>();
        client.GetSecretAsync("wa-token", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Response<KeyVaultSecret>>(_ => throw new RequestFailedException(404, "no existe"));
        var provider = new KeyVaultSecretProvider(client);

        var acto = () => provider.ObtenerSecretoAsync("wa-token", CancellationToken.None);

        await acto.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task ObtenerSecreto_AccesoDenegado403_PropagaRequestFailed()
    {
        var client = Substitute.For<SecretClient>();
        client.GetSecretAsync("wa-token", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Response<KeyVaultSecret>>(_ => throw new RequestFailedException(403, "sin permiso"));
        var provider = new KeyVaultSecretProvider(client);

        var acto = () => provider.ObtenerSecretoAsync("wa-token", CancellationToken.None);

        await acto.Should().ThrowAsync<RequestFailedException>();
    }

    [Fact]
    public async Task ObtenerSecreto_Existe_DevuelveValor()
    {
        var client = Substitute.For<SecretClient>();
        var secreto = new KeyVaultSecret("wa-token", "valor-secreto");
        client.GetSecretAsync("wa-token", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Response.FromValue(secreto, Substitute.For<Response>()));
        var provider = new KeyVaultSecretProvider(client);

        var valor = await provider.ObtenerSecretoAsync("wa-token", CancellationToken.None);

        valor.Should().Be("valor-secreto");
    }
}
