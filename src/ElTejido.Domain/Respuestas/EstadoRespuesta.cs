namespace ElTejido.Domain.Respuestas;

/// <summary>
/// Estado de una respuesta del participante (03 §3.8). <c>EvaluacionPendiente</c> marca que la
/// evaluacion cayo en fallback (08 §6) y queda para reproceso (REQ §20.3.10).
/// </summary>
public enum EstadoRespuesta
{
    Recibida,
    Evaluada,
    EvaluacionPendiente,
}
