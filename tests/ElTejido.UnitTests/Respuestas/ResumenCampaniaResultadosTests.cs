using ElTejido.Application.Respuestas;
using ElTejido.Domain.Campanas;
using ElTejido.Domain.Configuracion;
using ElTejido.Domain.Evaluacion;
using ElTejido.Domain.Respuestas;
using FluentAssertions;
using DominioEvaluacion = ElTejido.Domain.Evaluacion.Evaluacion;

namespace ElTejido.UnitTests.Respuestas;

/// <summary>
/// P-34 §4.6: el resumen describe el mismo conjunto que la tabla. Lógica pura, probada sin HTTP.
/// </summary>
public sealed class ResumenCampaniaResultadosTests
{
    private static readonly DateTimeOffset Epoca = DateTimeOffset.UnixEpoch;
    private static readonly EscalaRubrica Escala = EscalaRubrica.Crear(1, 5);

    [Fact]
    public void Construir_Participacion_MideSobreLaConvocatoriaCompleta()
    {
        var ideas = new[]
        {
            Idea("idea_1", "u_ana"),
            Idea("idea_2", "u_ana"),
            Idea("idea_3", "u_beto"),
        };

        var resumen = ResumenCampaniaResultados.Construir(
            ideas, SinEvaluaciones, convocados: 10, umbralMadurez: null, umbralUniforme: false, escala: null);

        resumen.TotalIdeas.Should().Be(3);
        resumen.Participacion.Convocados.Should().Be(10);
        resumen.Participacion.ConIdeas.Should().Be(2);
        resumen.Participacion.PromedioIdeasPorActivo.Should().Be(1.5m);
    }

    [Fact]
    public void Construir_SinIdeas_NoDivideEntreCero()
    {
        var resumen = ResumenCampaniaResultados.Construir(
            [], SinEvaluaciones, convocados: 10, umbralMadurez: null, umbralUniforme: false, escala: Escala);

        resumen.Participacion.PromedioIdeasPorActivo.Should().Be(0m);
        resumen.Calificaciones.Evaluadas.Should().Be(0);
        resumen.Calificaciones.Mediana.Should().BeNull();
        resumen.Calificaciones.Tramos.Should().BeEmpty();
        resumen.Temas.Should().BeEmpty();
    }

    // El embudo es acumulativo por definición: iniciadas ≥ confirmadas ≥ con evaluación ≥ maduras.
    [Fact]
    public void Construir_Embudo_EsAcumulativo()
    {
        var madura = Idea("idea_madura", "u_ana")
            .ConfirmarVersion("v_1", Epoca)
            .Cerrar(EstadoResultadoIdeaConsolidada.Madura, "eval_1", "umbral", Epoca);
        var confirmadaSinEvaluar = Idea("idea_confirmada", "u_beto").ConfirmarVersion("v_2", Epoca);
        var enCurso = Idea("idea_en_curso", "u_caro");

        var resumen = ResumenCampaniaResultados.Construir(
            [madura, confirmadaSinEvaluar, enCurso],
            new Dictionary<string, DominioEvaluacion>(StringComparer.Ordinal)
            {
                ["idea_madura"] = Evaluacion(4.5m),
            },
            convocados: 5,
            umbralMadurez: 4m,
            umbralUniforme: true,
            escala: Escala);

        resumen.Embudo.Iniciadas.Should().Be(3);
        resumen.Embudo.Confirmadas.Should().Be(2);
        resumen.Embudo.ConEvaluacion.Should().Be(1);
        resumen.Embudo.Maduras.Should().Be(1);
    }

    [Fact]
    public void Construir_Calificaciones_MedianaYTramosSobreLaEscalaDeLaRubrica()
    {
        var ideas = new[] { Idea("i1", "u_ana"), Idea("i2", "u_beto"), Idea("i3", "u_caro"), Idea("i4", "u_dani") };
        var evaluaciones = new Dictionary<string, DominioEvaluacion>(StringComparer.Ordinal)
        {
            ["i1"] = Evaluacion(1.5m),
            ["i2"] = Evaluacion(3m),
            ["i3"] = Evaluacion(4m),
            ["i4"] = Evaluacion(5m),
        };

        var resumen = ResumenCampaniaResultados.Construir(
            ideas, evaluaciones, convocados: 4, umbralMadurez: 4m, umbralUniforme: true, escala: Escala);

        resumen.Calificaciones.Evaluadas.Should().Be(4);
        resumen.Calificaciones.Mediana.Should().Be(3.5m);
        resumen.Calificaciones.Minima.Should().Be(1.5m);
        resumen.Calificaciones.Maxima.Should().Be(5m);
        // Cuatro tramos de un punto entre 1 y 5; el último incluye su extremo para no perder el 5.
        resumen.Calificaciones.Tramos.Select(tramo => (tramo.Desde, tramo.Hasta, tramo.Conteo))
            .Should().Equal((1m, 2m, 1), (2m, 3m, 0), (3m, 4m, 1), (4m, 5m, 2));
    }

    [Fact]
    public void Construir_SinEscalaConocida_UsaLosValoresObservados()
    {
        var ideas = new[] { Idea("i1", "u_ana"), Idea("i2", "u_beto") };
        var evaluaciones = new Dictionary<string, DominioEvaluacion>(StringComparer.Ordinal)
        {
            ["i1"] = Evaluacion(2.2m),
            ["i2"] = Evaluacion(3.4m),
        };

        var resumen = ResumenCampaniaResultados.Construir(
            ideas, evaluaciones, convocados: 2, umbralMadurez: null, umbralUniforme: false, escala: null);

        resumen.Calificaciones.Escala.Should().BeNull();
        resumen.Calificaciones.Tramos.Select(tramo => tramo.Desde).Should().Equal(2m, 3m);
    }

    [Fact]
    public void Construir_CoberturaPorPregunta_SeparaEstadosYCuentaLoQueSigueEnCurso()
    {
        var ideas = new[]
        {
            Idea("i1", "u_ana", "p_1").Cerrar(EstadoResultadoIdeaConsolidada.Madura, "e", "umbral", Epoca),
            Idea("i2", "u_beto", "p_1").Cerrar(EstadoResultadoIdeaConsolidada.Pendiente, null, "cierre", Epoca),
            Idea("i3", "u_caro", "p_1"),
            Idea("i4", "u_dani", "p_2").Cerrar(EstadoResultadoIdeaConsolidada.Rechazada, null, "rechazo", Epoca),
        };

        var resumen = ResumenCampaniaResultados.Construir(
            ideas, SinEvaluaciones, convocados: 4, umbralMadurez: null, umbralUniforme: false, escala: null);

        resumen.CoberturaPorPregunta.Select(cobertura => cobertura.PreguntaId).Should().Equal("p_1", "p_2");
        var primera = resumen.CoberturaPorPregunta[0];
        primera.Total.Should().Be(3);
        primera.Maduras.Should().Be(1);
        primera.Pendientes.Should().Be(1);
        primera.EnCurso.Should().Be(1);
        resumen.CoberturaPorPregunta[1].Rechazadas.Should().Be(1);
    }

    // Estable entre llamadas: mismo conteo se desempata alfabéticamente, no por orden de llegada.
    [Fact]
    public void Construir_Temas_OrdenaPorConteoYDesempataAlfabeticamente()
    {
        var ideas = new[] { Idea("i1", "u_ana"), Idea("i2", "u_beto"), Idea("i3", "u_caro") };
        var evaluaciones = new Dictionary<string, DominioEvaluacion>(StringComparer.Ordinal)
        {
            ["i1"] = Evaluacion(4m, ["riego", "zanahoria"]),
            ["i2"] = Evaluacion(4m, ["riego", "agua"]),
            ["i3"] = Evaluacion(4m, ["riego"]),
        };

        var resumen = ResumenCampaniaResultados.Construir(
            ideas, evaluaciones, convocados: 3, umbralMadurez: null, umbralUniforme: false, escala: Escala);

        resumen.Temas.Select(tema => (tema.Tema, tema.Conteo))
            .Should().Equal(("riego", 3), ("agua", 1), ("zanahoria", 1));
    }

    private static IReadOnlyDictionary<string, DominioEvaluacion> SinEvaluaciones { get; } =
        new Dictionary<string, DominioEvaluacion>(StringComparer.Ordinal);

    private static IdeaConsolidada Idea(string id, string usuarioId, string preguntaId = "p_1")
        => IdeaConsolidada.Crear(id, "c_1", usuarioId, preguntaId, "conv_1", "resp_1", 1, Epoca);

    private static DominioEvaluacion Evaluacion(decimal calificacion, string[]? temas = null)
        => DominioEvaluacion.Crear(
            $"eval_{calificacion}", "c_1", "resp_1", "u_ana", "p_1", "rub_1", 1, "pr_1", 1, "llm_1",
            new ConfigLlmSnapshot("AzureOpenAI", "gpt-4o-mini", "https://x", new Dictionary<string, object?>()),
            new Dictionary<string, decimal> { ["claridad"] = 1m },
            [CalificacionCriterio.Crear("claridad", 4m, "clara")],
            calificacion, "explica", "Buena idea", RecomendacionEvaluacion.Cerrar, null,
            temas ?? ["riego"], ["agua"], false, Epoca, ideaId: "idea_1");
}
