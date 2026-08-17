using ElTejido.Domain.Common;
using ElTejido.Domain.Localizacion;
using FluentAssertions;

namespace ElTejido.UnitTests.Localizacion;

public sealed class IdiomaConversacionTests
{
    [Theory]
    [InlineData("es", "es")]
    [InlineData(" ES ", "es")]
    [InlineData("en", "en")]
    [InlineData(" En ", "en")]
    public void Crear_NormalizaLosCodigosSoportados(string entrada, string esperado)
    {
        var idioma = IdiomaConversacion.Crear(entrada);

        idioma.Codigo.Should().Be(esperado);
        idioma.ToString().Should().Be(esperado);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("fr")]
    [InlineData("es_CO")]
    public void TryCrear_RechazaAusentesNoSoportadosYCodigosMeta(string? entrada)
    {
        IdiomaConversacion.TryCrear(entrada, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void DesdeFronteraHistorica_SoloAplicaDefaultEspanolCuandoElCampoEstaAusente(string? entrada)
    {
        IdiomaConversacion.DesdeFronteraHistorica(entrada).Should().Be(IdiomaConversacion.Espanol);
    }

    [Fact]
    public void Crear_ValorPresenteNoSoportado_LanzaErrorTipificado()
    {
        var acto = () => IdiomaConversacion.Crear("pt");

        acto.Should().Throw<DomainValidationException>()
            .Which.Code.Should().Be("IDIOMA_NO_SOPORTADO");
    }

    [Fact]
    public void Politica_ExponeUnaSolaListaInternaSinCodigosMeta()
    {
        IdiomaConversacion.CodigosSoportados.Should().Equal("es", "en");
        IdiomaConversacion.Espanol.Should().NotBe(IdiomaConversacion.Ingles);
    }
}
