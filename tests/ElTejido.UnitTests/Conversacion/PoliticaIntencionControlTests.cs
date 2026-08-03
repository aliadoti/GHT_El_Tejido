using ElTejido.Application.Conversacion;
using ElTejido.Domain.Conversaciones;
using FluentAssertions;

namespace ElTejido.UnitTests.Conversacion;

public sealed class PoliticaIntencionControlTests
{
    private readonly PoliticaIntencionControl _politica = new(maxCaracteres: 160);

    [Theory]
    [InlineData("Quiero parar aquí", DecisionIntencionControl.FinalizarIdea)]
    [InlineData("quiero pasar a otra idea", DecisionIntencionControl.FinalizarIdea)]
    [InlineData("stop now", DecisionIntencionControl.FinalizarParticipacion)]
    public void Resolver_AliasInequivoco_TransicionaSinNecesitarLlm(string texto, DecisionIntencionControl esperado)
    {
        var decision = _politica.Resolver(EstadoMaquinaConversacion.EsperandoRepregunta, hayUnidadActiva: true, texto);

        decision.Should().Be(esperado);
    }

    [Theory]
    [InlineData("Hay que parar la máquina antes de iniciar la actividad")]
    [InlineData("stop losses es el criterio de riesgo de mi propuesta")]
    public void Resolver_AporteSustantivo_PermaneceComoAporte(string texto)
    {
        var decision = _politica.Resolver(EstadoMaquinaConversacion.EsperandoRepregunta, hayUnidadActiva: true, texto);

        decision.Should().Be(DecisionIntencionControl.Aportar);
    }

    [Fact]
    public void Resolver_PrimerAporte_IgnoraInclusoCandidatoDeCierre()
    {
        var decision = _politica.Resolver(
            EstadoMaquinaConversacion.EsperandoRespuestaInicial,
            hayUnidadActiva: false,
            "stop now",
            IntencionControl.FinalizarParticipacion);

        decision.Should().Be(DecisionIntencionControl.Aportar);
    }

    [Fact]
    public void Resolver_CandidatoLlmElegible_PasaPorLaPoliticaDelServidor()
    {
        var decision = _politica.Resolver(
            EstadoMaquinaConversacion.EsperandoRepregunta,
            hayUnidadActiva: true,
            "me parece suficiente por ahora",
            IntencionControl.FinalizarIdea);

        decision.Should().Be(DecisionIntencionControl.FinalizarIdea);
    }
}
