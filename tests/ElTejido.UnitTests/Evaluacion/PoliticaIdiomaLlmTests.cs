using ElTejido.Application.Evaluacion;
using ElTejido.Domain.Localizacion;
using FluentAssertions;

namespace ElTejido.UnitTests.Evaluacion;

public sealed class PoliticaIdiomaLlmTests
{
    private readonly PoliticaIdiomaLlm _politica = new();

    [Theory]
    [InlineData("en", TipoDirectivaIdiomaLlm.Orientativo, "IDIOMA_ORIENTATIVO: en")]
    [InlineData(" ES ", TipoDirectivaIdiomaLlm.Salida, "IDIOMA_DE_SALIDA: es")]
    [InlineData("EN", TipoDirectivaIdiomaLlm.SalidaObligatoria, "IDIOMA_DE_SALIDA_OBLIGATORIO: en")]
    public void Resolver_IdiomaSoportadoProduceLaDirectivaSinTraducirPrompt(
        string idioma,
        TipoDirectivaIdiomaLlm tipo,
        string directiva)
    {
        var resultado = _politica.Resolver(idioma, tipo);

        resultado.Should().BeOfType<ResultadoDirectivaIdiomaLlm.Disponible>()
            .Which.Should().BeEquivalentTo(new
            {
                Idioma = IdiomaConversacion.Crear(idioma),
                Directiva = directiva,
            });
    }

    [Fact]
    public void Resolver_IdiomaNoSoportadoFallaTipificadoSinFallbackEspanol()
    {
        var resultado = _politica.Resolver("fr", TipoDirectivaIdiomaLlm.SalidaObligatoria);

        resultado.Should().BeOfType<ResultadoDirectivaIdiomaLlm.NoDisponible>()
            .Which.Codigo.Should().Be(PoliticaIdiomaLlm.CodigoIdiomaNoSoportado);
    }
}
