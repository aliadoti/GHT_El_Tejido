using ElTejido.Domain.Conversaciones;

namespace ElTejido.Application.Conversacion;

/// <summary>
/// P-15 (CAL-001) — Corte 2: resolución <b>determinista y sin E/S</b> de la transición conversacional
/// para un entrante en un hilo ya existente (no primer contacto). Interpreta la situación actual del hilo
/// (¿estamos ofreciendo una mejora?, ¿se agotaron las revisiones?, ¿el participante pide continuar o
/// rechaza el guardado?) y expresa las decisiones puras de la siguiente acción (evaluar los techos, cerrar
/// sin evaluar, motivo del techo). No lee configuración, reloj, repositorios ni servicios: recibe entradas
/// explícitas y devuelve decisiones tipadas. Los efectos y la E/S (buscar respuestas maduras, contar
/// turnos/cupos, persistir, enviar) los coordina la fachada <see cref="OrquestadorConversacion"/> con el
/// mismo orden y cortocircuito de antes. Encapsula los detectores de intención (continuar / rechazo de
/// guardado), que antes vivían sueltos en el orquestador.
/// </summary>
public sealed class ResolvedorTransicionConversacion
{
    private readonly DetectorIntencionContinuar _intencionContinuar;
    private readonly DetectorIntencionContinuar _intencionRechazoGuardado;

    public ResolvedorTransicionConversacion(
        DetectorIntencionContinuar intencionContinuar,
        DetectorIntencionContinuar intencionRechazoGuardado)
    {
        _intencionContinuar = intencionContinuar;
        _intencionRechazoGuardado = intencionRechazoGuardado;
    }

    /// <summary>
    /// Interpreta la situación del entrante a partir del estado del hilo y el texto. La intención de
    /// continuar (05 §4.4) y la de <b>rechazo del guardado</b> (I-17 §5.4) solo se consideran cuando ya
    /// ofrecimos una mejora (<c>EsperandoRepregunta</c>); el rechazo, además, solo si no pidió continuar.
    /// El rechazo devuelto es la <b>intención previa a la E/S</b>: la fachada aún debe confirmar que exista
    /// al menos una respuesta madura que degradar antes de tratarlo como cierre por rechazo.
    /// </summary>
    public SituacionEntrante Interpretar(
        EstadoMaquinaConversacion estadoMaquina,
        int repreguntasUsadas,
        int maxRepreguntas,
        string texto)
    {
        var esRepregunta = estadoMaquina == EstadoMaquinaConversacion.EsperandoRepregunta;
        var revisionesAgotadas = esRepregunta && repreguntasUsadas >= maxRepreguntas;
        var deseaContinuar = esRepregunta && _intencionContinuar.DeseaContinuar(texto);
        var deseaRechazarGuardado = esRepregunta && !deseaContinuar && _intencionRechazoGuardado.Coincide(texto);
        return new SituacionEntrante(esRepregunta, revisionesAgotadas, deseaContinuar, deseaRechazarGuardado);
    }

    /// <summary>
    /// Los techos deterministas (tope de turnos por hilo) y los cupos LLM de la campaña solo se evalúan si
    /// ninguna regla previa ya cierra el hilo (revisiones agotadas, continuar o rechazo del guardado): así
    /// se conserva el cortocircuito de E/S que evita consultas innecesarias.
    /// </summary>
    public static bool PermiteEvaluarTechos(bool revisionesAgotadas, bool deseaContinuar, bool deseaRechazarGuardado)
        => !revisionesAgotadas && !deseaContinuar && !deseaRechazarGuardado;

    /// <summary>
    /// Decisión final de la transición: registrar el entrante sin evaluar y cerrar. Ocurre si se agotaron
    /// las revisiones, el participante pidió continuar o rechazó el guardado, o disparó un techo/cupo.
    /// </summary>
    public static bool DebeCerrarSinEvaluar(
        bool revisionesAgotadas,
        bool deseaContinuar,
        bool deseaRechazarGuardado,
        bool turnosExcedidos,
        bool cupoLlmExcedido)
        => revisionesAgotadas || deseaContinuar || deseaRechazarGuardado || turnosExcedidos || cupoLlmExcedido;

    /// <summary>
    /// Motivo del techo determinista para el rastro <c>RateLimit</c> en <c>LogSeguridad</c>, cuando el
    /// cierre lo provocó un tope de turnos o un cupo LLM (turnos → cupo de llamadas → presupuesto de tokens).
    /// </summary>
    public static string MotivoTecho(bool turnosExcedidos, bool cupoLlamadasUsuarioExcedido)
        => turnosExcedidos
            ? "tope_turnos_hilo"
            : cupoLlamadasUsuarioExcedido
                ? "cupo_llamadas_llm_usuario"
                : "presupuesto_tokens_campania";
}

/// <summary>
/// Situación interpretada de un entrante en un hilo existente (P-15 Corte 2). Los tres últimos flags solo
/// pueden ser <c>true</c> cuando <see cref="EsRepregunta"/> lo es. <see cref="DeseaRechazarGuardado"/> es la
/// intención previa a la E/S de reclasificación.
/// </summary>
public readonly record struct SituacionEntrante(
    bool EsRepregunta,
    bool RevisionesAgotadas,
    bool DeseaContinuar,
    bool DeseaRechazarGuardado);
