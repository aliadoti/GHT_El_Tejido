using ElTejido.Application.Evaluacion;
using ElTejido.Domain.Evaluacion;

namespace ElTejido.Calibracion;

/// <summary>
/// Observación normalizada de <b>una</b> evaluación de una entrada del golden set en una repetición.
/// Es la unidad que consume el agregador (D5 §3.2); se deriva de <see cref="ResultadoEvaluacion"/>
/// (éxito o fallback) sin depender del LLM real, para que el agregador se pruebe mockeado en CI.
/// </summary>
public sealed record MuestraCalibracion(
    string EntradaId,
    string Categoria,
    bool EsFallback,
    string? MotivoFallback,
    decimal CalificacionTotal,
    IReadOnlyDictionary<string, decimal> CalificacionPorCriterio,
    string Decision,
    IReadOnlyList<string> Ideas,
    int PromptTokens,
    int CompletionTokens)
{
    public int TotalTokens => PromptTokens + CompletionTokens;

    /// <summary>
    /// Deriva la muestra desde el resultado del evaluador (08 §2). El fallback (<c>salida_invalida:*</c>,
    /// <c>error_proveedor</c>) queda marcado con su motivo; sus tokens sí se contabilizan porque el
    /// proveedor ya cobró (P-10), pero su calificación (0) no entra en la distribución de scores.
    /// </summary>
    public static MuestraCalibracion Desde(EntradaGoldenSet entrada, ResultadoEvaluacion resultado)
    {
        ArgumentNullException.ThrowIfNull(entrada);
        ArgumentNullException.ThrowIfNull(resultado);

        var evaluacion = resultado.Evaluacion;
        var esFallback = resultado is ResultadoEvaluacion.Fallback;
        var motivo = resultado is ResultadoEvaluacion.Fallback fallback ? fallback.Motivo : null;

        var criterios = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var calificacion in evaluacion.CalificacionPorCriterio)
        {
            // El último gana si el modelo repite criterio (dato no confiable); no rompemos por eso.
            criterios[calificacion.Criterio] = calificacion.Puntaje;
        }

        var decision = evaluacion.Recomendacion == RecomendacionEvaluacion.Repreguntar
            ? DecisionCalibracion.Repreguntar
            : DecisionCalibracion.Cerrar;

        var ideas = evaluacion.Temas
            .Concat(evaluacion.Entidades)
            .Select(idea => idea.Trim())
            .Where(idea => idea.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var uso = evaluacion.UsoTokens;

        return new MuestraCalibracion(
            entrada.Id,
            entrada.Categoria,
            esFallback,
            motivo,
            evaluacion.CalificacionTotal,
            criterios,
            decision,
            ideas,
            uso?.PromptTokens ?? 0,
            uso?.CompletionTokens ?? 0);
    }
}
