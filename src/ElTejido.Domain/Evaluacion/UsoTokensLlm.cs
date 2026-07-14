namespace ElTejido.Domain.Evaluacion;

/// <summary>
/// P-10 — consumo de tokens reportado por el proveedor LLM en una llamada (prompt + completion).
/// Se persiste en la <see cref="Evaluacion"/> para derivar el costo acumulado por campaña sin
/// documentos contadores nuevos (mismo criterio que el conteo de cupos, <c>SUPUESTOS.md#guardrails-cupos-conversacion</c>).
/// Ausente/null en documentos previos o cuando el proveedor no reporta uso (comportamiento seguro:
/// suma 0).
/// </summary>
public sealed record UsoTokensLlm(int PromptTokens, int CompletionTokens)
{
    public int Total => PromptTokens + CompletionTokens;

    /// <summary>Normaliza valores negativos a 0 (el proveedor no debería, pero es dato no confiable).</summary>
    public static UsoTokensLlm Crear(int promptTokens, int completionTokens)
        => new(Math.Max(0, promptTokens), Math.Max(0, completionTokens));
}
