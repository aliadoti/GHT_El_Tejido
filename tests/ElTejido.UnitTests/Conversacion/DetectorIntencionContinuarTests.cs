using ElTejido.Application.Conversacion;
using FluentAssertions;

namespace ElTejido.UnitTests.Conversacion;

public sealed class DetectorIntencionContinuarTests
{
    private readonly DetectorIntencionContinuar _detector =
        new(DetectorIntencionContinuar.FrasesPorDefecto, maxCaracteres: 40);

    [Theory]
    [InlineData("sigamos")]
    [InlineData("Sigamos")]
    [InlineData("listo!")]
    [InlineData("Así está bien")] // con acentos y mayuscula
    [InlineData("ok, así está bien, sigamos")]
    [InlineData("ya estoy conforme, gracias")]
    public void DeseaContinuar_FrasesDeContinuar_DetectaIntencion(string texto)
    {
        _detector.DeseaContinuar(texto).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("El agua de la vereda esta bien gestionada por la comunidad desde 2019.")] // larga: no aplica contencion
    [InlineData("Creo que el siguiente paso del proceso es socializar el acuerdo con los lideres.")]
    [InlineData("Mi respuesta es que debemos continuar fortaleciendo el tejido social del barrio entero.")]
    public void DeseaContinuar_RespuestasReales_NoFalsoPositivo(string texto)
    {
        _detector.DeseaContinuar(texto).Should().BeFalse();
    }

    [Fact]
    public void DeseaContinuar_SinFrasesConfiguradas_SiempreFalso()
    {
        var vacio = new DetectorIntencionContinuar(frases: Array.Empty<string>(), maxCaracteres: 40);

        vacio.DeseaContinuar("sigamos").Should().BeFalse();
    }

    [Fact]
    public void DeseaContinuar_FraseExacta_DetectaAunSiSuperaElLargo()
    {
        // maxCaracteres muy bajo: la contencion no aplica, pero la igualdad exacta si.
        var detector = new DetectorIntencionContinuar(new[] { "no quiero mejorar" }, maxCaracteres: 1);

        detector.DeseaContinuar("No quiero mejorar").Should().BeTrue();
        detector.DeseaContinuar("la verdad no quiero mejorar mi respuesta").Should().BeFalse();
    }
}
