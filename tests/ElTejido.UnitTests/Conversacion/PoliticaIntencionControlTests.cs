using ElTejido.Application.Conversacion;
using ElTejido.Domain.Conversaciones;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace ElTejido.UnitTests.Conversacion;

public sealed class PoliticaIntencionControlTests
{
    private readonly PoliticaIntencionControl _politica = new(new OpcionesConversacion());

    [Theory]
    [InlineData("Quiero parar aquí", DecisionIntencionControl.FinalizarIdea)]
    [InlineData("quiero pasar a otra idea", DecisionIntencionControl.FinalizarIdea)]
    [InlineData("stop now", DecisionIntencionControl.FinalizarParticipacion)]
    [InlineData("no quiero continuar", DecisionIntencionControl.FinalizarParticipacion)]
    [InlineData("no más", DecisionIntencionControl.FinalizarParticipacion)]
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

    [Fact]
    public void Resolver_ConfigValida_ReemplazaLosAliasYConservaLaNormalizacion()
    {
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Conversacion:FrasesFinalizarIdea:0"] = "  Cerrar esta propuesta  ",
                ["Conversacion:FrasesFinalizarParticipacion:0"] = "TERMINAR EL EJERCÍCIO",
                ["Conversacion:MaxCaracteresClasificacionIntencionControl"] = "160",
            })
            .Build();
        var opciones = configuracion.GetSection(OpcionesConversacion.Seccion).Get<OpcionesConversacion>();
        var politica = new PoliticaIntencionControl(opciones!);

        politica.Resolver(
                EstadoMaquinaConversacion.EsperandoRepregunta,
                hayUnidadActiva: true,
                "¿Cerrar ésta propuesta!")
            .Should().Be(DecisionIntencionControl.FinalizarIdea);
        politica.Resolver(
                EstadoMaquinaConversacion.EsperandoRepregunta,
                hayUnidadActiva: true,
                "terminar el ejercicio")
            .Should().Be(DecisionIntencionControl.FinalizarParticipacion);
        politica.Resolver(
                EstadoMaquinaConversacion.EsperandoRepregunta,
                hayUnidadActiva: true,
                "quiero parar aquí")
            .Should().Be(DecisionIntencionControl.Aportar,
                "una lista configurada reemplaza el default compilado");
    }
}
