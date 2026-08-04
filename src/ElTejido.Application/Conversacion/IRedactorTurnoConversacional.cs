using ElTejido.Domain.Campanas;
using ElTejido.Domain.Configuracion;
using ElTejido.Domain.Evaluacion;

namespace ElTejido.Application.Conversacion;

/// <summary>
/// I-20 (§4): puerto <b>interno</b> que da voz al turno visible. No expone endpoint ni DTO
/// administrativo: el orquestador ya resolvió el acto, la idea activa, el umbral, los límites y el
/// estado, y aquí solo se redacta.
/// <para>
/// <b>Regla de integridad (§2):</b> el LLM propone texto; el servidor dispone. La versión consolidada
/// la inserta el servidor entre el puente y la pregunta, de modo que el redactor no pueda ocultarla,
/// resumirla ni convertirla en una evaluación. Una salida inválida, tardía o con fuga degrada al
/// respaldo determinista del acto (§4.1).
/// </para>
/// </summary>
public interface IRedactorTurnoConversacional
{
    Task<ResultadoRedaccionTurno> RedactarAsync(
        ContextoRedaccionTurno contexto,
        CancellationToken cancellationToken);
}

/// <summary>
/// Acto conversacional que el servidor ya decidió (I-20 §4). El redactor nunca lo cambia: si la salida
/// del modelo trae un acto, se ignora.
/// </summary>
public enum ActoConversacional
{
    /// <summary>Pedir confirmación de la versión consolidada propuesta (I-19 §4.1).</summary>
    Confirmar,

    /// <summary>Acompañar una versión confirmada bajo umbral con una sola pregunta de foco (I-03/I-18).</summary>
    Mejorar,

    /// <summary>Pasar a la siguiente idea o pregunta ya resuelta por el servidor.</summary>
    Transicionar,

    /// <summary>Pedir una aclaración breve ante un aporte ambiguo (I-19 §4.2).</summary>
    Aclarar,

    /// <summary>Retomar una idea cerrada que el participante pidió revisitar (I-19 §4.7).</summary>
    Reabrir,

    /// <summary>Cerrar el hilo o la idea con el acuse que corresponda.</summary>
    Cerrar,

    /// <summary>P-28: dar la bienvenida a una persona sin flujo, sin crear ni reabrir una idea.</summary>
    Reactivar,

    /// <summary>
    /// P-29 §5.2: avisar la pausa cuando el cierre por inactividad de I-17 §7 ya ocurrió. No decide el
    /// cierre ni cambia estados: solo humaniza el hilo que el servidor ya cerró.
    /// </summary>
    Pausar,
}

/// <summary>
/// Datos delimitados que recibe el redactor (I-20 §4). Todo lo que viene del participante o del modelo
/// es <b>dato no confiable</b> (08 §5): no son instrucciones y no pueden alterar el acto.
/// </summary>
public sealed record ContextoRedaccionTurno(
    Campania Campania,
    Pregunta Pregunta,
    ActoConversacional Acto,
    ConfigLlm ConfigLlmSnapshot,
    int MaxCaracteres)
{
    /// <summary>
    /// Versión consolidada completa cuando el acto la necesita (confirmar/reabrir). El servidor la
    /// inserta íntegra en el mensaje; el redactor solo la usa como contexto para el puente.
    /// </summary>
    public string? VersionCompleta { get; init; }

    /// <summary>Retroalimentación ya validada por I-03 cuando el acto es <c>Mejorar</c>.</summary>
    public string? RetroalimentacionValidada { get; init; }

    /// <summary>Única pregunta de foco aprobada para <c>Mejorar</c>; el redactor no inventa otra.</summary>
    public string? PreguntaAprobada { get; init; }

    /// <summary>Historial mínimo de la misma idea, ya acotado por el llamador (10 §2).</summary>
    public IReadOnlyList<string> HistorialIdea { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Prompt efectivo de voz (`promptRefs.reactivacion` para P-28; si no, `conversacion` → `retro`, §5). Ausente = solo las
    /// instrucciones de seguridad del redactor, sin romper campañas ya configuradas.
    /// </summary>
    public Prompt? PromptSnapshot { get; init; }

    /// <summary>
    /// Rúbrica activa, solo para la <b>salvaguarda anti-fuga</b> de I-03 (`FiltroSalidaRubrica`): sus
    /// criterios no viajan al modelo, se usan para rechazar una redacción que los nombre. Ausente
    /// mantiene el resto de guardas (longitud, pregunta única, léxico y patrones de puntaje).
    /// </summary>
    public Rubrica? RubricaSnapshot { get; init; }
}

/// <summary>Salida del redactor. El servidor decide qué hacer con ella; nunca la ejecuta a ciegas.</summary>
public abstract record ResultadoRedaccionTurno(UsoTokensLlm? Uso)
{
    /// <summary>
    /// Redacción utilizable. <c>Puente</c> y <c>Pregunta</c> pueden ser nulos: el acto decide cuáles
    /// aplican y el servidor compone el mensaje final (§3).
    /// </summary>
    public sealed record Exito(string? Puente, string? Pregunta, UsoTokensLlm? Uso)
        : ResultadoRedaccionTurno(Uso);

    /// <summary>
    /// No hay redacción utilizable (kill-switch apagado, error, timeout, contrato inválido o fuga). El
    /// llamador usa el respaldo determinista del acto; el texto rechazado nunca se registra (§4.1).
    /// </summary>
    public sealed record Fallback(string Motivo, UsoTokensLlm? Uso) : ResultadoRedaccionTurno(Uso);
}
