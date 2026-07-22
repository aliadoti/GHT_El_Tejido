using ElTejido.Domain.Campanas;
using ElTejido.Domain.Common;
using FluentAssertions;

namespace ElTejido.UnitTests.Campanas;

public sealed class PreguntaTests
{
    [Fact]
    public void Crear_NormalizesQuestionConfiguration()
    {
        var pregunta = Pregunta.Crear(
            " p_ingresos ",
            " Escribe una idea para mejorar los ingresos. ",
            " Se concreto. ",
            " ingresos ",
            1,
            EstadoRegistro.Activo,
            " r_general ",
            3,
            new Dictionary<string, string> { [" evaluar "] = " pr_eval ", [""] = "omitido" },
            1,
            LimitesSeguridad.ParaPregunta(1500, 2),
            ConfigMarkdown.Crear(TipoArtefactoMarkdown.Respuesta));

        pregunta.Id.Should().Be("p_ingresos");
        pregunta.Texto.Should().Be("Escribe una idea para mejorar los ingresos.");
        pregunta.Instruccion.Should().Be("Se concreto.");
        pregunta.Categoria.Should().Be("ingresos");
        pregunta.RubricaRef.Should().Be("r_general");
        pregunta.VersionRubrica.Should().Be(3);
        pregunta.PromptRefs.Should().Contain("evaluar", "pr_eval");
        pregunta.MaxRepreguntas.Should().Be(1);
        pregunta.LimitesSeguridad.MaxCaracteresMensaje.Should().Be(1500);
        pregunta.LimitesSeguridad.MaxLlamadasLlmPorUsuario.Should().Be(2);
        pregunta.ConfigMarkdown.TipoArtefacto.Should().Be(TipoArtefactoMarkdown.Respuesta);
    }

    [Fact]
    public void Crear_RejectsInvalidRubricVersion()
    {
        var act = () => Pregunta.Crear(
            "p_1",
            "Pregunta",
            "Instruccion",
            "categoria",
            1,
            EstadoRegistro.Activo,
            "r_general",
            0,
            null,
            1,
            LimitesSeguridad.ParaPregunta(1500, 2),
            ConfigMarkdown.Crear(TipoArtefactoMarkdown.Respuesta));

        act.Should()
            .Throw<DomainValidationException>()
            .Where(exception => exception.Code == "VERSION_RUBRICA_INVALIDA");
    }

    [Fact]
    public void Crear_AceptaOverrideDeUmbralPorPregunta()
    {
        var pregunta = Pregunta.Crear(
            "p_1",
            "Pregunta",
            "Instruccion",
            "categoria",
            1,
            EstadoRegistro.Activo,
            "r_general",
            1,
            null,
            1,
            LimitesSeguridad.ParaPregunta(1500, 2),
            ConfigMarkdown.Crear(TipoArtefactoMarkdown.Respuesta),
            umbralCierreAnticipado: 0.8);

        pregunta.UmbralCierreAnticipado.Should().Be(0.8);
    }

    [Fact]
    public void Crear_UmbralPorPreguntaNuloHeredaCampania()
    {
        var pregunta = Pregunta.Crear(
            "p_1",
            "Pregunta",
            "Instruccion",
            "categoria",
            1,
            EstadoRegistro.Activo,
            "r_general",
            1,
            null,
            1,
            LimitesSeguridad.ParaPregunta(1500, 2),
            ConfigMarkdown.Crear(TipoArtefactoMarkdown.Respuesta));

        pregunta.UmbralCierreAnticipado.Should().BeNull();
    }

    [Fact]
    public void Crear_RechazaUmbralPorPreguntaMayorQueUno()
    {
        var act = () => Pregunta.Crear(
            "p_1",
            "Pregunta",
            "Instruccion",
            "categoria",
            1,
            EstadoRegistro.Activo,
            "r_general",
            1,
            null,
            1,
            LimitesSeguridad.ParaPregunta(1500, 2),
            ConfigMarkdown.Crear(TipoArtefactoMarkdown.Respuesta),
            umbralCierreAnticipado: 1.5);

        act.Should()
            .Throw<DomainValidationException>()
            .Where(exception => exception.Code == "UMBRAL_CIERRE_ANTICIPADO_INVALIDO");
    }

    [Fact]
    public void Crear_RejectsNegativeFollowUpLimit()
    {
        var act = () => Pregunta.Crear(
            "p_1",
            "Pregunta",
            "Instruccion",
            "categoria",
            1,
            EstadoRegistro.Activo,
            "r_general",
            1,
            null,
            -1,
            LimitesSeguridad.ParaPregunta(1500, 2),
            ConfigMarkdown.Crear(TipoArtefactoMarkdown.Respuesta));

        act.Should()
            .Throw<DomainValidationException>()
            .Where(exception => exception.Code == "MAX_REPREGUNTAS_INVALIDO");
    }
}
