using ElTejido.Domain.Common;

namespace ElTejido.Domain.Evaluacion;

/// <summary>
/// Calificacion de un criterio de la rubrica (03 §3.9). El <paramref name="Puntaje"/> debe estar
/// dentro de la escala de la rubrica; la validacion de escala la hace el evaluador (08 §4).
/// </summary>
public sealed record CalificacionCriterio(string Criterio, decimal Puntaje, string Justificacion)
{
    public static CalificacionCriterio Crear(string criterio, decimal puntaje, string justificacion)
        => new(
            DomainGuards.Required(criterio, nameof(criterio)),
            puntaje,
            justificacion?.Trim() ?? string.Empty);
}
