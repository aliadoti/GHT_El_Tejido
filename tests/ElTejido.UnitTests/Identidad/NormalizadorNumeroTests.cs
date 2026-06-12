using ElTejido.Domain.Common;
using ElTejido.Domain.Identidad;
using FluentAssertions;

namespace ElTejido.UnitTests.Identidad;

public sealed class NormalizadorNumeroTests
{
    private readonly NormalizadorNumero _normalizador = new();

    [Theory]
    [InlineData("+57 300 111 2233", "573001112233")]
    [InlineData("57-300-111-2233", "573001112233")]
    [InlineData("(1) 305 555 1234", "13055551234")]
    [InlineData("573001112233", "573001112233")]
    public void Normalizar_RemovesSymbolsAndKeepsE164Digits(string input, string expected)
    {
        var numero = _normalizador.Normalizar(input);

        numero.Valor.Should().Be(expected);
        numero.ToString().Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1234567")]
    [InlineData("0123456789")]
    [InlineData("+57 300 111 2233 9999")]
    [InlineData("sin numero")]
    public void Normalizar_RejectsNonPlausibleE164Values(string input)
    {
        var act = () => _normalizador.Normalizar(input);

        act.Should()
            .Throw<DomainValidationException>()
            .Where(exception => exception.Code == "NUMERO_E164_INVALIDO");
    }

    [Fact]
    public void TryNormalizar_ReturnsFalseInsteadOfThrowingForInvalidValue()
    {
        var success = _normalizador.TryNormalizar("abc", out var numero);

        success.Should().BeFalse();
        numero.Should().BeNull();
    }
}

