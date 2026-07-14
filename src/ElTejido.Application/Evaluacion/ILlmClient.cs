using ElTejido.Domain.Evaluacion;

namespace ElTejido.Application.Evaluacion;

/// <summary>
/// Adaptador de bajo nivel hacia un proveedor LLM (08 §2). Devuelve el <b>texto crudo</b> del
/// modelo (se espera JSON segun el contrato de 08 §4) mas el consumo de tokens reportado (P-10); la
/// validacion la hace el evaluador. La seleccion por proveedor (<c>AzureOpenAI</c>, <c>OpenAI</c>,
/// <c>Otro</c>) la resuelve la implementacion en Infrastructure a partir de <see cref="LlmRequest.Proveedor"/>.
/// </summary>
public interface ILlmClient
{
    Task<LlmRespuesta> CompletarJsonAsync(LlmRequest request, CancellationToken cancellationToken);
}

/// <summary>Texto crudo del modelo (08 §4) + uso de tokens reportado por el proveedor (P-10, null si no lo reporta).</summary>
public sealed record LlmRespuesta(string Texto, UsoTokensLlm? Uso);

/// <summary>Mensaje con rol explicito para separar instruccion (system) de dato (user) (08 §5, ARQ §12).</summary>
public sealed record LlmMensaje(string Rol, string Contenido)
{
    public const string RolSistema = "system";
    public const string RolUsuario = "user";
}

/// <summary>
/// Solicitud a un proveedor LLM. No contiene la API key, solo su <see cref="ApiKeyRef"/> (nombre del
/// secreto, REQ §19.2.7); la implementacion la resuelve por Key Vault. Tampoco lleva secretos en
/// los mensajes (08 §3.2, ARQ §6 paso 2).
/// </summary>
public sealed record LlmRequest(
    string Proveedor,
    string Endpoint,
    string Modelo,
    string ApiKeyRef,
    IReadOnlyList<LlmMensaje> Mensajes,
    IReadOnlyDictionary<string, object?> Parametros,
    int MaxCompletionTokens,
    int TimeoutSegundos,
    int MaxReintentos,
    string? CampaniaId = null);
