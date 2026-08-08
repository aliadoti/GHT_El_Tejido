using ElTejido.Application.Conversacion;
using FluentAssertions;

namespace ElTejido.UnitTests.Conversacion;

public sealed class ResolutorFrasesFinalizacionTests
{
    [Theory]
    [InlineData("   ", "vacio")]
    [InlineData("---", "vacio")]
    public void Resolver_EntradaVaciaTrasNormalizar_DescartaTodaLaLista(string frase, string motivo)
    {
        var resultado = ResolutorFrasesFinalizacion.Resolver(new OpcionesConversacion
        {
            FrasesFinalizarIdea = new List<string> { "cerrar esta idea", frase },
        });

        resultado.FinalizarIdea.FueDescartada.Should().BeTrue();
        resultado.FinalizarIdea.MotivoDescarte.Should().Be(motivo);
        resultado.FinalizarIdea.Frases.Should().Equal(DetectorIntencionContinuar.FrasesFinalizarIdeaPorDefecto);
    }

    [Fact]
    public void Resolver_DuplicadoTrasNormalizar_DescartaTodaLaLista()
    {
        var resultado = ResolutorFrasesFinalizacion.Resolver(new OpcionesConversacion
        {
            FrasesFinalizarParticipacion = new List<string> { "Terminar el ejercicio", "terminar, el ejercicio!" },
        });

        resultado.FinalizarParticipacion.FueDescartada.Should().BeTrue();
        resultado.FinalizarParticipacion.MotivoDescarte.Should().Be("duplicado");
        resultado.FinalizarParticipacion.Frases
            .Should().Equal(DetectorIntencionContinuar.FrasesFinalizarParticipacionPorDefecto);
    }

    [Fact]
    public void Resolver_ExcedeLimiteConfigurado_DescartaTodaLaLista()
    {
        var resultado = ResolutorFrasesFinalizacion.Resolver(new OpcionesConversacion
        {
            MaxFrasesFinalizacion = 1,
            FrasesFinalizarIdea = new List<string> { "cerrar esta idea", "dejar esta idea" },
        });

        resultado.FinalizarIdea.FueDescartada.Should().BeTrue();
        resultado.FinalizarIdea.MotivoDescarte.Should().Be("limite");
    }

    [Fact]
    public void Resolver_ConfigValida_NormalizaYConservaVersionOperativa()
    {
        var resultado = ResolutorFrasesFinalizacion.Resolver(new OpcionesConversacion
        {
            VersionFrasesFinalizacion = "2026-08-08-r2",
            FrasesFinalizarIdea = new List<string> { "  Cerrar esta propuesta! " },
        });

        resultado.FinalizarIdea.Origen.Should().Be(OrigenFrasesFinalizacion.Configuracion);
        resultado.FinalizarIdea.Frases.Should().Equal("cerrar esta propuesta");
        resultado.FinalizarIdea.Version.Should().Be("2026-08-08-r2");
    }
}
