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
    [InlineData("Creo que ya está bien")]
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

    [Fact]
    public void RechazoGuardado_NoConfundeUnaSalidaQueEmpiezaPorNo()
    {
        var detector = new DetectorIntencionContinuar(
            DetectorIntencionContinuar.FrasesRechazoGuardadoPorDefecto,
            maxCaracteres: 40);

        detector.Coincide("no").Should().BeTrue();
        detector.Coincide("no más").Should().BeFalse();
        detector.Coincide("no quiero continuar").Should().BeFalse();
    }

    [Theory]
    [InlineData("Vamos a mejorarla")]
    [InlineData("ayúdame a mejorar!")]
    [InlineData("ME GUSTARÍA MEJORAR")]
    public void P24_SolicitarMejora_FraseBreveNormalizada_DetectaIntencion(string texto)
    {
        var detector = new DetectorIntencionContinuar(
            DetectorIntencionContinuar.FrasesSolicitarMejoraPorDefecto,
            maxCaracteres: 40);

        detector.Coincide(texto).Should().BeTrue();
    }

    [Fact]
    public void P24_SolicitarMejora_MensajeLargoConContenido_NoLoConfundeConLaIntencion()
    {
        var detector = new DetectorIntencionContinuar(
            DetectorIntencionContinuar.FrasesSolicitarMejoraPorDefecto,
            maxCaracteres: 40);

        detector.Coincide(
            "Vamos a mejorarla agregando responsables, presupuesto, fechas y una prueba piloto para cada área.")
            .Should().BeFalse();
    }
}
