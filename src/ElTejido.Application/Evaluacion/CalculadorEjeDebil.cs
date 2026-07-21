using ElTejido.Domain.Configuracion;
using ElTejido.Domain.Evaluacion;

namespace ElTejido.Application.Evaluacion;

/// <summary>
/// Calculo determinista (server-side, <b>nunca</b> el LLM) del criterio con menor puntaje de una
/// evaluacion (I-03, REQ §21). Solo sirve como soporte de observabilidad de <see cref="FiltroSalidaRubrica"/>;
/// no se persiste ni se expone al participante. Desempate: menor peso de rubrica, luego orden
/// alfabetico (ordinal) del nombre del criterio, para que el resultado sea reproducible entre
/// corridas con los mismos datos.
/// </summary>
public static class CalculadorEjeDebil
{
    /// <summary>
    /// Devuelve el criterio de la rubrica con menor puntaje entre <paramref name="calificaciones"/>,
    /// o <c>null</c> si no hay calificaciones o ninguna coincide con un criterio de
    /// <paramref name="criteriosRubrica"/> (coincidencia por nombre, insensible a mayusculas).
    /// </summary>
    public static CriterioRubrica? Determinar(
        IReadOnlyList<CalificacionCriterio> calificaciones,
        IReadOnlyCollection<CriterioRubrica> criteriosRubrica)
    {
        if (calificaciones.Count == 0 || criteriosRubrica.Count == 0)
        {
            return null;
        }

        var candidatos = calificaciones
            .Select(c => (
                Puntaje: c.Puntaje,
                Criterio: criteriosRubrica.FirstOrDefault(
                    r => string.Equals(r.Nombre, c.Criterio, StringComparison.OrdinalIgnoreCase))))
            .Where(x => x.Criterio is not null)
            .ToArray();

        if (candidatos.Length == 0)
        {
            return null;
        }

        var puntajeMinimo = candidatos.Min(x => x.Puntaje);
        return candidatos
            .Where(x => x.Puntaje == puntajeMinimo)
            .Select(x => x.Criterio!)
            .OrderBy(c => c.Peso)
            .ThenBy(c => c.Nombre, StringComparer.Ordinal)
            .First();
    }
}
