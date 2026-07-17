using ElTejido.Domain.Campanas;
using ElTejido.Domain.Configuracion;
using ElTejido.Domain.Usuarios;

namespace ElTejido.Application.Evaluacion;

/// <summary>
/// Contexto necesario para evaluar una respuesta (08 §2). Lo arma el orquestador (05 §4.3) e incluye
/// los snapshots de rubrica, prompt y configLLM ya resueltos para garantizar reproducibilidad
/// (ARQ §8.3). El <see cref="HistorialReciente"/> esta acotado por el llamador (10 §2).
/// </summary>
public sealed record ContextoEvaluacion(
    Campania Campania,
    Pregunta Pregunta,
    Usuario Usuario,
    string RespuestaId,
    string RespuestaTexto,
    IReadOnlyList<string> HistorialReciente,
    Rubrica RubricaSnapshot,
    Prompt PromptSnapshot,
    ConfigLlm ConfigLlmSnapshot)
{
    /// <summary>
    /// I-09 tejido colectivo: líneas del bloque <c>APORTES_DE_LA_COMUNIDAD</c> ya <b>sanitizadas y
    /// presupuestadas</b> que el orquestador inyecta como <b>dato no confiable</b> (08 §3.2). Vacío
    /// (default) = conversación autocontenida, sin bloque de tejido. Es aditivo: los llamadores
    /// existentes construyen el contexto sin tejido y se comportan igual.
    /// </summary>
    public IReadOnlyList<string> AportesComunidad { get; init; } = Array.Empty<string>();
}
