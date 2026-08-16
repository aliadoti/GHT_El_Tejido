using ElTejido.Domain.Common;
using ElTejido.Domain.Configuracion;

namespace ElTejido.Domain.Evaluacion;

/// <summary>
/// Calificacion de un criterio de la rubrica (03 §3.9). El <paramref name="Puntaje"/> debe estar
/// dentro de la escala de la rubrica; la validacion de escala la hace el evaluador (08 §4.1).
/// <para>
/// DT-RUB-01: <paramref name="CriterioId"/> es la clave de emparejamiento con la version efectiva y
/// con el snapshot; <paramref name="Criterio"/> es la etiqueta visible congelada, que permite leer
/// documentos historicos y reportes sin resolver la rubrica. Un documento anterior a esta deuda no
/// tiene id: se lee con <see cref="CrearHistorico"/>, que lo deja vacio en vez de adivinarlo, y el
/// documento no se muta.
/// </para>
/// </summary>
public sealed record CalificacionCriterio(string CriterioId, string Criterio, decimal Puntaje, string Justificacion)
{
    /// <summary>Calificacion con id canonico explicito; es la forma que produce el evaluador.</summary>
    public static CalificacionCriterio Crear(string criterioId, string criterio, decimal puntaje, string justificacion)
        => new(
            DomainGuards.Required(criterioId, nameof(criterioId)),
            DomainGuards.Required(criterio, nameof(criterio)),
            puntaje,
            justificacion?.Trim() ?? string.Empty);

    /// <summary>
    /// Deriva el id canonico desde el nombre del criterio. Sirve para fixtures y para los flujos que
    /// todavia identifican el criterio por su etiqueta; una evaluacion real recibe el id de la
    /// version efectiva.
    /// </summary>
    public static CalificacionCriterio Crear(string criterio, decimal puntaje, string justificacion)
        => Crear(
            NormalizacionRubrica.NormalizarId(criterio),
            DomainGuards.Required(criterio, nameof(criterio)),
            puntaje,
            justificacion);

    /// <summary>
    /// Lectura de un documento historico sin <c>criterioId</c> (03 §3.9): conserva el nombre snapshot
    /// y deja el id vacio. No se infiere una clave que el documento nunca tuvo, porque esa inferencia
    /// podria emparejar con un criterio distinto de una version posterior.
    /// </summary>
    public static CalificacionCriterio CrearHistorico(string criterio, decimal puntaje, string justificacion)
        => new(
            string.Empty,
            DomainGuards.Required(criterio, nameof(criterio)),
            puntaje,
            justificacion?.Trim() ?? string.Empty);
}
