using ElTejido.Domain.Configuracion;
using ElTejido.Domain.Respuestas;
using DominioEvaluacion = ElTejido.Domain.Evaluacion.Evaluacion;

namespace ElTejido.Application.Respuestas;

/// <summary>Cuánta gente participó, sobre la convocatoria completa (P-34 §4.6).</summary>
public sealed record ParticipacionResumen(int Convocados, int ConIdeas, decimal PromedioIdeasPorActivo);

/// <summary>Embudo acumulativo: iniciadas ≥ confirmadas ≥ con evaluación ≥ maduras.</summary>
public sealed record EmbudoResumen(int Iniciadas, int Confirmadas, int ConEvaluacion, int Maduras);

public sealed record TramoCalificacion(decimal Desde, decimal Hasta, int Conteo);

/// <summary>
/// Distribución de las calificaciones vigentes. <c>UmbralUniforme=false</c> significa que la marca de
/// umbral no aplica a todas las barras y el cliente no debe dibujarla (04 §5.8).
/// </summary>
public sealed record CalificacionesResumen(
    int Evaluadas,
    decimal? Mediana,
    decimal? Minima,
    decimal? Maxima,
    decimal? UmbralMadurez,
    bool UmbralUniforme,
    EscalaRubrica? Escala,
    IReadOnlyList<TramoCalificacion> Tramos);

public sealed record CoberturaPregunta(
    string PreguntaId, int Total, int Maduras, int Pendientes, int Rechazadas, int EnCurso);

public sealed record TemaResumen(string Tema, int Conteo);

public sealed record ResumenCampania(
    int TotalIdeas,
    ParticipacionResumen Participacion,
    EmbudoResumen Embudo,
    CalificacionesResumen Calificaciones,
    IReadOnlyList<CoberturaPregunta> CoberturaPorPregunta,
    IReadOnlyList<TemaResumen> Temas);

/// <summary>
/// P-34 §4.6 (04 §5.8): resumen de campaña sobre el <b>mismo conjunto filtrado</b> que la tabla — por
/// eso <c>TotalIdeas</c> siempre coincide con el `total` del listado (§8.9). Es lógica pura: recibe
/// las ideas ya filtradas y sus evaluaciones vigentes, y no consulta nada.
/// </summary>
public static class ResumenCampaniaResultados
{
    /// <summary>Hasta 20 temas: una nube más larga deja de ser un resumen.</summary>
    public const int MaximoTemas = 20;

    public static ResumenCampania Construir(
        IReadOnlyList<IdeaConsolidada> ideas,
        IReadOnlyDictionary<string, DominioEvaluacion> evaluacionesVigentes,
        int convocados,
        decimal? umbralMadurez,
        bool umbralUniforme,
        EscalaRubrica? escala)
    {
        var participantesActivos = ideas
            .Select(idea => idea.UsuarioId)
            .Distinct(StringComparer.Ordinal)
            .Count();

        var participacion = new ParticipacionResumen(
            convocados,
            participantesActivos,
            participantesActivos == 0
                ? 0m
                : Math.Round((decimal)ideas.Count / participantesActivos, 2, MidpointRounding.AwayFromZero));

        var embudo = new EmbudoResumen(
            ideas.Count,
            ideas.Count(idea => !string.IsNullOrWhiteSpace(idea.VersionConfirmadaRef)),
            ideas.Count(idea => evaluacionesVigentes.ContainsKey(idea.Id)),
            ideas.Count(idea => idea.EstadoResultado == EstadoResultadoIdeaConsolidada.Madura));

        var calificaciones = ideas
            .Where(idea => evaluacionesVigentes.ContainsKey(idea.Id))
            .Select(idea => evaluacionesVigentes[idea.Id].CalificacionTotal)
            .OrderBy(calificacion => calificacion)
            .ToArray();

        return new ResumenCampania(
            ideas.Count,
            participacion,
            embudo,
            ConstruirCalificaciones(calificaciones, umbralMadurez, umbralUniforme, escala),
            ConstruirCobertura(ideas),
            ConstruirTemas(ideas, evaluacionesVigentes));
    }

    private static CalificacionesResumen ConstruirCalificaciones(
        IReadOnlyList<decimal> ordenadas, decimal? umbral, bool umbralUniforme, EscalaRubrica? escala)
    {
        if (ordenadas.Count == 0)
        {
            return new CalificacionesResumen(0, null, null, null, umbral, umbralUniforme, escala, []);
        }

        return new CalificacionesResumen(
            ordenadas.Count,
            Mediana(ordenadas),
            ordenadas[0],
            ordenadas[^1],
            umbral,
            umbralUniforme,
            escala,
            ConstruirTramos(ordenadas, escala));
    }

    private static decimal Mediana(IReadOnlyList<decimal> ordenadas)
    {
        var mitad = ordenadas.Count / 2;
        return ordenadas.Count % 2 == 1
            ? ordenadas[mitad]
            : (ordenadas[mitad - 1] + ordenadas[mitad]) / 2m;
    }

    /// <summary>
    /// Tramos de un punto sobre la escala de la rúbrica. Sin escala conocida —evaluaciones históricas
    /// sin snapshot— se usan los valores observados, para no inventar un rango que nadie declaró.
    /// </summary>
    private static IReadOnlyList<TramoCalificacion> ConstruirTramos(
        IReadOnlyList<decimal> ordenadas, EscalaRubrica? escala)
    {
        var inicio = escala is null ? Math.Floor(ordenadas[0]) : escala.Min;
        var fin = escala is null ? Math.Ceiling(ordenadas[^1]) : escala.Max;
        if (fin <= inicio)
        {
            fin = inicio + 1;
        }

        var tramos = new List<TramoCalificacion>();
        for (var desde = inicio; desde < fin; desde++)
        {
            var hasta = desde + 1;
            // El último tramo incluye su extremo superior: si no, la nota máxima no aparecería.
            var esUltimo = hasta >= fin;
            var conteo = ordenadas.Count(calificacion =>
                calificacion >= desde && (esUltimo ? calificacion <= hasta : calificacion < hasta));
            tramos.Add(new TramoCalificacion(desde, hasta, conteo));
        }

        return tramos;
    }

    private static IReadOnlyList<CoberturaPregunta> ConstruirCobertura(IReadOnlyList<IdeaConsolidada> ideas)
        => ideas
            .GroupBy(idea => idea.PreguntaId, StringComparer.Ordinal)
            .OrderBy(grupo => grupo.Key, StringComparer.Ordinal)
            .Select(grupo => new CoberturaPregunta(
                grupo.Key,
                grupo.Count(),
                grupo.Count(idea => idea.EstadoResultado == EstadoResultadoIdeaConsolidada.Madura),
                grupo.Count(idea => idea.EstadoResultado == EstadoResultadoIdeaConsolidada.Pendiente),
                grupo.Count(idea => idea.EstadoResultado == EstadoResultadoIdeaConsolidada.Rechazada),
                grupo.Count(idea => idea.EstadoResultado is null)))
            .ToArray();

    /// <summary>Desempate alfabético: la lista debe ser estable entre llamadas (04 §5.8).</summary>
    private static IReadOnlyList<TemaResumen> ConstruirTemas(
        IReadOnlyList<IdeaConsolidada> ideas, IReadOnlyDictionary<string, DominioEvaluacion> evaluaciones)
        => ideas
            .Where(idea => evaluaciones.ContainsKey(idea.Id))
            .SelectMany(idea => evaluaciones[idea.Id].Temas)
            .Where(tema => !string.IsNullOrWhiteSpace(tema))
            .Select(tema => tema.Trim())
            .GroupBy(tema => tema, StringComparer.OrdinalIgnoreCase)
            .Select(grupo => new TemaResumen(grupo.First(), grupo.Count()))
            .OrderByDescending(tema => tema.Conteo)
            .ThenBy(tema => tema.Tema, StringComparer.OrdinalIgnoreCase)
            .Take(MaximoTemas)
            .ToArray();
}
