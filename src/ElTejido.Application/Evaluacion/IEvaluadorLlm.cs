namespace ElTejido.Application.Evaluacion;

/// <summary>
/// Puerto de evaluacion con LLM (08 §2): dado el contexto de una respuesta, construye el contexto
/// (separacion instruccion/dato), llama al proveedor configurable, valida la salida estructurada y
/// devuelve una evaluacion normalizada o un fallback seguro (08 §6). La decision cerrar/repreguntar
/// la toma el orquestador respetando el tope de 1 repregunta (05 §4.4).
/// </summary>
public interface IEvaluadorLlm
{
    Task<ResultadoEvaluacion> EvaluarAsync(ContextoEvaluacion contexto, CancellationToken cancellationToken);
}
