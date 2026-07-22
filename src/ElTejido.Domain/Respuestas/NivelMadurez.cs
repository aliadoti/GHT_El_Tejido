namespace ElTejido.Domain.Respuestas;

/// <summary>
/// I-17 — nivel de madurez con que queda registrada una respuesta (03 §3.8). Se <b>sella al evaluar</b>
/// server-side (no lo decide el LLM): <c>Maduro</c> cuando la calificacion supera el umbral de la
/// rubrica de esa campania/pregunta; <c>Incubacion</c> en caso contrario, en fallback/pendiente o
/// cuando el participante rechaza explicitamente que se guarde. El default seguro para documentos
/// historicos sin el campo es <c>Incubacion</c> (mantiene el comportamiento plano previo).
/// </summary>
public enum NivelMadurez
{
    Incubacion,
    Maduro,
}
