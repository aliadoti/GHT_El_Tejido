using ElTejido.Application.Evaluacion;

namespace ElTejido.Calibracion;

/// <summary>
/// Abstracción que evalúa <b>una</b> entrada del golden set y devuelve el resultado tipado (08 §2).
/// Desacopla el harness de la construcción del <c>ContextoEvaluacion</c>: el runner real (opt-in, fuera
/// de CI) lo implementa armando la tripleta de staging y llamando a <c>IEvaluadorLlm</c> sobre el
/// <c>ILlmClient</c> real; las pruebas lo mockean para agregar sin costo (verde en CI).
/// </summary>
public interface IEvaluadorEntrada
{
    Task<ResultadoEvaluacion> EvaluarAsync(EntradaGoldenSet entrada, CancellationToken cancellationToken);
}
