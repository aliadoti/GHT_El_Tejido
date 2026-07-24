using ElTejido.Application.Conversacion;
using ElTejido.Domain.Conversaciones;
using FluentAssertions;

namespace ElTejido.UnitTests.Conversacion;

/// <summary>
/// P-15 (CAL-001) — pruebas de la resolución determinista de la transición extraída del orquestador.
/// Cubren la interpretación de la situación (repregunta, revisiones agotadas, continuar, rechazo del
/// guardado) y las decisiones puras de la siguiente acción (evaluar techos, cerrar sin evaluar, motivo).
/// Reproducen las reglas que antes vivían inline en <see cref="OrquestadorConversacion"/>.
/// </summary>
public sealed class ResolvedorTransicionConversacionTests
{
    private static ResolvedorTransicionConversacion Crear()
        => new(
            new DetectorIntencionContinuar(DetectorIntencionContinuar.FrasesPorDefecto, 40),
            new DetectorIntencionContinuar(DetectorIntencionContinuar.FrasesRechazoGuardadoPorDefecto, 40));

    // ---- Interpretar -------------------------------------------------------------------------------

    [Fact]
    public void Interpretar_NoEsRepregunta_TodoFalseAunqueElTextoParezcaIntencion()
    {
        // Una frase de continuar como respuesta inicial no se interpreta como intención: se evalúa.
        var situacion = Crear().Interpretar(
            EstadoMaquinaConversacion.EsperandoRespuestaInicial, repreguntasUsadas: 0, maxRepreguntas: 1, "sigamos");

        situacion.EsRepregunta.Should().BeFalse();
        situacion.RevisionesAgotadas.Should().BeFalse();
        situacion.DeseaContinuar.Should().BeFalse();
        situacion.DeseaRechazarGuardado.Should().BeFalse();
    }

    [Fact]
    public void Interpretar_RepreguntaConTextoNormal_SoloEsRepregunta()
    {
        var situacion = Crear().Interpretar(
            EstadoMaquinaConversacion.EsperandoRepregunta, repreguntasUsadas: 0, maxRepreguntas: 1, "Mi respuesta mejorada");

        situacion.EsRepregunta.Should().BeTrue();
        situacion.RevisionesAgotadas.Should().BeFalse();
        situacion.DeseaContinuar.Should().BeFalse();
        situacion.DeseaRechazarGuardado.Should().BeFalse();
    }

    [Fact]
    public void Interpretar_RepreguntaConFraseDeContinuar_DeseaContinuar()
    {
        var situacion = Crear().Interpretar(
            EstadoMaquinaConversacion.EsperandoRepregunta, repreguntasUsadas: 0, maxRepreguntas: 2, "Asi esta bien, sigamos");

        situacion.DeseaContinuar.Should().BeTrue();
        situacion.DeseaRechazarGuardado.Should().BeFalse();
    }

    [Fact]
    public void Interpretar_RepreguntaConFraseDeRechazo_DeseaRechazarGuardado()
    {
        var situacion = Crear().Interpretar(
            EstadoMaquinaConversacion.EsperandoRepregunta, repreguntasUsadas: 0, maxRepreguntas: 2, "no es eso");

        situacion.DeseaContinuar.Should().BeFalse();
        situacion.DeseaRechazarGuardado.Should().BeTrue();
    }

    [Fact]
    public void Interpretar_RevisionesAgotadas_CuandoSeAlcanzaElMaximo()
    {
        var situacion = Crear().Interpretar(
            EstadoMaquinaConversacion.EsperandoRepregunta, repreguntasUsadas: 1, maxRepreguntas: 1, "Otra version");

        situacion.RevisionesAgotadas.Should().BeTrue();
    }

    // ---- PermiteEvaluarTechos ----------------------------------------------------------------------

    [Fact]
    public void PermiteEvaluarTechos_SinReglaPreviaDeCierre_EsTrue()
        => ResolvedorTransicionConversacion
            .PermiteEvaluarTechos(revisionesAgotadas: false, deseaContinuar: false, deseaRechazarGuardado: false)
            .Should().BeTrue();

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void PermiteEvaluarTechos_ConAlgunaReglaDeCierre_EsFalse(
        bool revisionesAgotadas, bool deseaContinuar, bool deseaRechazarGuardado)
        => ResolvedorTransicionConversacion
            .PermiteEvaluarTechos(revisionesAgotadas, deseaContinuar, deseaRechazarGuardado)
            .Should().BeFalse();

    // ---- DebeCerrarSinEvaluar ----------------------------------------------------------------------

    [Fact]
    public void DebeCerrarSinEvaluar_SinNingunMotivo_EsFalse()
        => ResolvedorTransicionConversacion
            .DebeCerrarSinEvaluar(false, false, false, false, false)
            .Should().BeFalse();

    [Theory]
    [InlineData(true, false, false, false, false)]
    [InlineData(false, true, false, false, false)]
    [InlineData(false, false, true, false, false)]
    [InlineData(false, false, false, true, false)]
    [InlineData(false, false, false, false, true)]
    public void DebeCerrarSinEvaluar_ConCualquierMotivo_EsTrue(
        bool revisionesAgotadas, bool deseaContinuar, bool deseaRechazarGuardado, bool turnosExcedidos, bool cupoLlmExcedido)
        => ResolvedorTransicionConversacion
            .DebeCerrarSinEvaluar(revisionesAgotadas, deseaContinuar, deseaRechazarGuardado, turnosExcedidos, cupoLlmExcedido)
            .Should().BeTrue();

    // ---- MotivoTecho -------------------------------------------------------------------------------

    [Theory]
    [InlineData(true, false, "tope_turnos_hilo")]
    [InlineData(true, true, "tope_turnos_hilo")] // turnos tiene precedencia
    [InlineData(false, true, "cupo_llamadas_llm_usuario")]
    [InlineData(false, false, "presupuesto_tokens_campania")]
    public void MotivoTecho_DistingueElOrigenDelTecho(bool turnosExcedidos, bool cupoLlamadasUsuarioExcedido, string esperado)
        => ResolvedorTransicionConversacion.MotivoTecho(turnosExcedidos, cupoLlamadasUsuarioExcedido)
            .Should().Be(esperado);
}
