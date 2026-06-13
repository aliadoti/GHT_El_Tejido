using ElTejido.Infrastructure.Seguridad;
using FluentAssertions;

namespace ElTejido.UnitTests.Seguridad;

public sealed class HasherOtpBcryptTests
{
    private readonly HasherOtpBcrypt _hasher = new();

    [Fact]
    public void Hashear_NoDevuelveElCodigoEnClaro()
    {
        var hash = _hasher.Hashear("482913", "pepper-global");

        hash.Should().NotBeNullOrWhiteSpace();
        hash.Should().NotContain("482913");
    }

    [Fact]
    public void Verificar_CodigoYPepperCorrectos_DevuelveTrue()
    {
        var hash = _hasher.Hashear("482913", "pepper-global");

        _hasher.Verificar("482913", "pepper-global", hash).Should().BeTrue();
    }

    [Fact]
    public void Verificar_CodigoIncorrecto_DevuelveFalse()
    {
        var hash = _hasher.Hashear("482913", "pepper-global");

        _hasher.Verificar("000000", "pepper-global", hash).Should().BeFalse();
    }

    [Fact]
    public void Verificar_PepperDistinto_DevuelveFalse()
    {
        var hash = _hasher.Hashear("482913", "pepper-global");

        _hasher.Verificar("482913", "otro-pepper", hash).Should().BeFalse();
    }

    [Fact]
    public void Verificar_HashCorrupto_DevuelveFalseSinLanzar()
    {
        _hasher.Verificar("482913", "pepper-global", "no-es-un-hash").Should().BeFalse();
    }
}
