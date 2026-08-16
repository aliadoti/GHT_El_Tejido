using ElTejido.Domain.Configuracion;
using ElTejido.Domain.Evaluacion;

namespace ElTejido.Application.Evaluacion;

/// <summary>
/// Calculo determinista (server-side, <b>nunca</b> el LLM) del criterio con menor puntaje de una
/// evaluacion (I-03, REQ §21). Solo sirve como soporte de observabilidad de <see cref="FiltroSalidaRubrica"/>;
/// no se persiste ni se expone al participante.
/// <para>
/// DT-RUB-01 §8: empareja por <b>id canonico</b>, no por el nombre visible, de modo que renombrar un
/// criterio no rompe el calculo. Desempate determinista y reproducible: menor peso, luego
/// <c>orden</c>, luego <c>id</c> ordinal.
/// </para>
/// </summary>
public static class CalculadorEjeDebil
{
    /// <summary>
    /// Devuelve el criterio de la rubrica con menor puntaje entre <paramref name="calificaciones"/>,
    /// o <c>null</c> si no hay calificaciones o ninguna coincide con un criterio de
    /// <paramref name="criteriosRubrica"/>.
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
            .Select(c => (c.Puntaje, Criterio: Resolver(c, criteriosRubrica)))
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
            .ThenBy(c => c.Orden)
            .ThenBy(c => c.Id, StringComparer.Ordinal)
            .First();
    }

    /// <summary>
    /// Empareja por id canonico. Una calificacion de documento historico no tiene id (03 §3.9): en
    /// ese caso —y solo en ese— se cae al nombre visible, que es lo unico que ese documento guardo.
    /// </summary>
    private static CriterioRubrica? Resolver(
        CalificacionCriterio calificacion,
        IReadOnlyCollection<CriterioRubrica> criteriosRubrica)
    {
        if (calificacion.CriterioId.Length > 0)
        {
            var porId = criteriosRubrica.FirstOrDefault(
                r => string.Equals(r.Id, calificacion.CriterioId, StringComparison.Ordinal));
            if (porId is not null)
            {
                return porId;
            }
        }

        return criteriosRubrica.FirstOrDefault(
            r => string.Equals(r.Nombre, calificacion.Criterio, StringComparison.OrdinalIgnoreCase));
    }
}
