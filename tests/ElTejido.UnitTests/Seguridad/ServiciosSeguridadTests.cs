using ElTejido.Application.Seguridad;
using ElTejido.Infrastructure.Configuracion;
using ElTejido.Infrastructure.Seguridad;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ElTejido.UnitTests.Seguridad;

public sealed class ServiciosSeguridadTests
{
    [Fact]
    public void AgregarSeguridad_ConKeyVaultUri_SeleccionaKeyVaultComoProveedorInterno()
    {
        var proveedor = ResolverProveedor(new Dictionary<string, string?>
        {
            ["KeyVault:Uri"] = "https://eltejido.vault.azure.net/",
        });

        proveedor.Should().BeOfType<SecretProviderConCache>()
            .Which.Inner.Should().BeOfType<KeyVaultSecretProvider>();
    }

    [Fact]
    public void AgregarSeguridad_SinKeyVaultUri_SeleccionaConfiguracionComoProveedorInterno()
    {
        var proveedor = ResolverProveedor(new Dictionary<string, string?>());

        proveedor.Should().BeOfType<SecretProviderConCache>()
            .Which.Inner.Should().BeOfType<ConfiguracionSecretProvider>();
    }

    private static ISecretProvider ResolverProveedor(Dictionary<string, string?> valores)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(valores)
            .Build();

        var services = new ServiceCollection();
        services.AgregarSeguridad(configuration);

        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<ISecretProvider>();
    }
}
