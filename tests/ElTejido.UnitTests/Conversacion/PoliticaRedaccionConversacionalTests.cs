using ElTejido.Application.Conversacion;
using ElTejido.Domain.Campanas;
using ElTejido.Domain.Common;
using FluentAssertions;

namespace ElTejido.UnitTests.Conversacion;

/// <summary>
/// I-20 §5 (corte 1): decisiones deterministas de la voz conversacional — kill-switch global sin
/// opt-in por campaña y precedencia del prompt efectivo `conversacion` → `retro`.
/// </summary>
public sealed class PoliticaRedaccionConversacionalTests
{
    private static readonly DateTimeOffset Epoca = DateTimeOffset.UnixEpoch;

    [Fact]
    public void PromptDeVoz_DeLaPregunta_PrevaleceSobreLaCampania()
    {
        var pregunta = Pregunta("p_1", new() { ["conversacion"] = "pr_voz_pregunta" });
        var campania = Campania(pregunta, new() { ["conversacion"] = "pr_voz_campania" });

        var politica = Construir();

        politica.ResolverPromptRef(campania, pregunta).Should().Be("pr_voz_pregunta");
        politica.UsaPromptDeVoz(campania, pregunta).Should().BeTrue();
    }

    [Fact]
    public void SinPromptEnLaPregunta_UsaElDeLaCampania()
    {
        var pregunta = Pregunta("p_1", promptRefs: null);
        var campania = Campania(pregunta, new() { ["conversacion"] = "pr_voz_campania" });

        Construir().ResolverPromptRef(campania, pregunta).Should().Be("pr_voz_campania");
    }

    [Fact]
    public void SinPromptDeVoz_CaeAlDeRetroSinRomperCampaniasActuales()
    {
        // Una campaña configurada hoy solo tiene `retro`: debe seguir funcionando y guiar el tono (§5).
        var pregunta = Pregunta("p_1", promptRefs: null);
        var campania = Campania(pregunta, new() { ["retro"] = "pr_retro", ["evaluar"] = "pr_eval" });

        var politica = Construir();

        politica.ResolverPromptRef(campania, pregunta).Should().Be("pr_retro");
        politica.UsaPromptDeVoz(campania, pregunta).Should().BeFalse();
    }

    [Fact]
    public void SinNingunaReferencia_NoHayPromptEfectivo()
    {
        var pregunta = Pregunta("p_1", promptRefs: null);
        var campania = Campania(pregunta, promptRefs: null);

        Construir().ResolverPromptRef(campania, pregunta).Should().BeNull();
    }

    [Fact]
    public void ReferenciaVacia_SeIgnoraYSigueLaPrecedencia()
    {
        var pregunta = Pregunta("p_1", new() { ["conversacion"] = "   " });
        var campania = Campania(pregunta, new() { ["conversacion"] = "pr_voz_campania" });

        Construir().ResolverPromptRef(campania, pregunta).Should().Be("pr_voz_campania");
    }

    [Fact]
    public void KillSwitch_ApagadoDeshabilitaLaVozSinOptInPorCampania()
    {
        Construir(habilitada: true).Habilitada.Should().BeTrue();
        Construir(habilitada: false).Habilitada.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void MaxCaracteresInvalido_SeNormalizaAlDefaultSeguro(int configurado)
    {
        Construir(maxCaracteres: configurado).MaxCaracteres.Should().Be(320);
    }

    [Theory]
    [InlineData(ActoConversacional.Confirmar, true)]
    [InlineData(ActoConversacional.Mejorar, true)]
    [InlineData(ActoConversacional.Aclarar, true)]
    [InlineData(ActoConversacional.Reabrir, true)]
    [InlineData(ActoConversacional.Transicionar, false)]
    [InlineData(ActoConversacional.Cerrar, false)]
    public void SoloLosActosQueLoExigenAdmitenPregunta(ActoConversacional acto, bool admite)
    {
        // §4.1: como máximo una pregunta visible, y solo en el acto que la necesita.
        PoliticaRedaccionConversacional.AdmitePregunta(acto).Should().Be(admite);
    }

    private static PoliticaRedaccionConversacional Construir(
        bool habilitada = true, int maxCaracteres = 320)
        => new(habilitada, maxCaracteres);

    private static Pregunta Pregunta(string id, Dictionary<string, string>? promptRefs)
        => ElTejido.Domain.Campanas.Pregunta.Crear(
            id, "Pregunta", "Instruccion", "categoria", 1, EstadoRegistro.Activo,
            rubricaRef: null, versionRubrica: null, promptRefs: promptRefs, maxRepreguntas: 1,
            LimitesSeguridad.ParaPregunta(1500, 2), ConfigMarkdown.Crear(TipoArtefactoMarkdown.Respuesta));

    private static Campania Campania(Pregunta pregunta, Dictionary<string, string>? promptRefs)
        => ElTejido.Domain.Campanas.Campania.Crear(
            "c_1", "Campania", "Descripcion", "Objetivo", EstadoCampania.Activa, null, new[] { pregunta },
            "rub_1", promptRefs, "llm_1", ConfigMarkdown.Crear(TipoArtefactoMarkdown.Respuesta),
            ConfigConversacional.Crear(1, "Gracias por participar."),
            LimitesSeguridad.Crear(1500, 10, 2), usuariosHabilitados: null, Epoca, Epoca);
}
