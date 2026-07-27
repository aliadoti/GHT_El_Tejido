using ElTejido.Domain.Common;
using ElTejido.Domain.Respuestas;
using FluentAssertions;

namespace ElTejido.UnitTests.Respuestas;

public sealed class IdeasConsolidadasTests
{
    private static readonly DateTimeOffset Epoca = new(2026, 7, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Idea_PropuestaConfirmacionYCierreMaduro_ConservaPunterosYCuraduria()
    {
        var idea = IdeaConsolidada.Crear("idea_1", "c_1", "u_1", "p_1", "conv_1", "resp_1", 1, Epoca)
            .ConPropuesta("idea_1_v1", Epoca.AddMinutes(1))
            .ConfirmarVersion("idea_1_v1", Epoca.AddMinutes(2))
            .Cerrar(EstadoResultadoIdeaConsolidada.Madura, "eval_1", "umbral", Epoca.AddMinutes(3));

        idea.VersionConfirmadaRef.Should().Be("idea_1_v1");
        idea.VersionPropuestaRef.Should().BeNull();
        idea.EstadoFlujo.Should().Be(EstadoFlujoIdeaConsolidada.Cerrada);
        idea.EstadoResultado.Should().Be(EstadoResultadoIdeaConsolidada.Madura);
        idea.NivelMadurez.Should().Be(NivelMadurez.Maduro);
        idea.EstadoCuraduria.Should().Be(EstadoCuraduriaIdea.Pendiente);
    }

    [Fact]
    public void Idea_MaduraReabierta_SuspendeResultadoYCuraduria()
    {
        var idea = IdeaConsolidada.Crear("idea_1", "c_1", "u_1", "p_1", "conv_1", "resp_1", 1, Epoca)
            .ConPropuesta("idea_1_v1", Epoca)
            .ConfirmarVersion("idea_1_v1", Epoca)
            .Cerrar(EstadoResultadoIdeaConsolidada.Madura, "eval_1", "umbral", Epoca)
            .Reabrir(Epoca.AddMinutes(1));

        idea.EstadoFlujo.Should().Be(EstadoFlujoIdeaConsolidada.EnRevision);
        idea.EstadoResultado.Should().BeNull();
        idea.EstadoCuraduria.Should().BeNull();
        idea.NivelMadurez.Should().Be(NivelMadurez.Incubacion);
    }

    [Fact]
    public void Version_ExigeAportesNuevosIncluidosEnAcumulados()
    {
        var accion = () => VersionIdeaConsolidada.Crear(
            "idea_1_v1", "c_1", "idea_1", 1, null, "Texto", new[] { "resp_1" }, Array.Empty<string>(),
            TipoAporteIdea.Inicial, EstadoConfirmacionVersionIdea.Propuesta, null, null, null, null, Epoca);

        accion.Should().Throw<DomainValidationException>().Which.Code.Should().Be("APORTES_VERSION_IDEA_INVALIDOS");
    }
}
