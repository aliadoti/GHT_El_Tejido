using ElTejido.Infrastructure.Seguridad;
using FluentAssertions;

namespace ElTejido.UnitTests.Seguridad;

public sealed class GeneradorCodigoOtpCsprngTests
{
    private readonly GeneradorCodigoOtpCsprng _generador = new();

    [Theory]
    [InlineData(6)]
    [InlineData(8)]
    public void Generar_DevuelveCodigoDeLaLongitudPedidaSoloConDigitos(int longitud)
    {
        var codigo = _generador.Generar(longitud);

        codigo.Should().HaveLength(longitud);
        codigo.Should().MatchRegex("^[0-9]+$");
    }

    [Fact]
    public void Generar_LongitudInvalida_Lanza()
    {
        var accion = () => _generador.Generar(0);

        accion.Should().Throw<ArgumentOutOfRangeException>();
    }
}
