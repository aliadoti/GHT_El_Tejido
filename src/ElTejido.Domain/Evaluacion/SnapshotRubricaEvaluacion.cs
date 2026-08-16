using ElTejido.Domain.Configuracion;

namespace ElTejido.Domain.Evaluacion;

/// <summary>
/// Snapshot de la version de rubrica efectivamente usada en una evaluacion (03 §3.9, 08 §3.5,
/// DT-RUB-01 §8). Congela escala, huella y criterios ordenados con nombre, descripcion y peso para
/// que el resultado siga siendo explicable <b>aunque despues se cree o active una version nueva</b>.
/// Es opcional: un documento historico no lo tiene y se lee igual.
/// </summary>
public sealed record SnapshotRubricaEvaluacion(
    string RubricaId,
    int Version,
    EscalaRubrica Escala,
    string HashEstructura,
    IReadOnlyList<CriterioRubrica> Criterios)
{
    public static SnapshotRubricaEvaluacion Desde(Rubrica rubrica)
        => new(
            rubrica.Id,
            rubrica.Version,
            rubrica.Escala,
            rubrica.HashEstructura,
            rubrica.Criterios.OrderBy(c => c.Orden).ToArray());
}
