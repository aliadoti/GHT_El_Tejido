using ElTejido.Application.Identidad;
using ElTejido.Application.WhatsApp;
using ElTejido.Domain.Campanas;
using DominioConversacion = ElTejido.Domain.Conversaciones.Conversacion;

namespace ElTejido.Application.Conversacion;

/// <summary>
/// Puerto del orquestador conversacional (05 §4.1): gobierna la maquina de estados de un hilo a
/// partir de un mensaje entrante de un participante autorizado.
/// </summary>
/// <remarks>
/// Es el punto de entrega del WhatsApp Gateway (05 §2.4 paso g). La maquina de estados completa
/// (evaluacion LLM, repregunta unica, compilacion Markdown) pertenece a las Fases 6/7; en esta
/// fase (Gateway) existe una implementacion provisional que solo registra el hito sin procesar.
/// </remarks>
public interface IOrquestadorConversacion
{
    Task ProcesarMensajeEntranteAsync(
        ParticipanteResuelto participante,
        MensajeEntrante mensaje,
        CancellationToken cancellationToken);

    /// <summary>
    /// P-26 corte 3 (05 §4.4.3): entrega un aporte cuya campania y pregunta ya fueron resueltas por el
    /// enrutamiento determinista. Con la conversacion mas reciente de esa pregunta abierta se procesa
    /// alli (afinidad); sin conversacion se aplica el primer contacto de siempre; con la conversacion
    /// cerrada se crea un <b>ciclo nuevo</b> (§5.7) con id derivado del mensaje raiz — un reintento no
    /// duplica el ciclo — y el aporte se procesa como contenido sustantivo del ciclo.
    /// </summary>
    Task ProcesarAporteEnrutadoAsync(
        ParticipanteResuelto participante,
        MensajeEntrante mensaje,
        ContextoAporteEnrutado contexto,
        CancellationToken cancellationToken);

    /// <summary>
    /// P-28: envía la entrada humana sin crear hilo ni tratar el saludo como una idea. El alcance ya
    /// fue resuelto de forma determinista por P-26.
    /// </summary>
    Task EnviarDespertarProactivoAsync(
        ParticipanteResuelto participante,
        MensajeEntrante mensaje,
        CancellationToken cancellationToken);

    /// <summary>
    /// P-29 §5.2: avisa la pausa del hilo que el barrido de inactividad (I-17 §7) <b>ya cerro</b>. No
    /// decide el cierre, no toca estados ni <c>motivoCierre</c> y envia un unico mensaje; fuera de la
    /// ventana de servicio de 24 h se omite el envio libre.
    /// </summary>
    Task EnviarPausaPorInactividadAsync(
        DominioConversacion conversacion,
        Campania campania,
        CancellationToken cancellationToken);

    /// <summary>
    /// I-18: envia el turno ya evaluado de la idea activa que el barrido por tiempo acaba de activar.
    /// La fachada conserva una sola ruta para enviar y persistir mensajes salientes.
    /// </summary>
    Task EnviarTurnoCoachingPendienteAsync(
        DominioConversacion conversacion,
        Campania campania,
        CancellationToken cancellationToken);
}

/// <summary>
/// P-26: alcance ya resuelto de un aporte enrutado — la pregunta elegida y, si el aporte estuvo
/// conservado, el id del <c>EnrutamientoAporte</c> que lo origino (auditoria en la conversacion).
/// </summary>
public sealed record ContextoAporteEnrutado(string PreguntaId, string? EnrutamientoAporteId);
