namespace ElTejido.Domain.Evaluacion;

/// <summary>
/// Recomendacion del LLM sobre el siguiente paso del hilo (03 §3.9, 08 §4). La <b>decision</b>
/// final (respetando el tope de 1 repregunta del MVP) la toma el orquestador (05 §4.4); este valor
/// es solo la sugerencia del modelo.
/// </summary>
public enum RecomendacionEvaluacion
{
    Cerrar,
    Repreguntar,
}
