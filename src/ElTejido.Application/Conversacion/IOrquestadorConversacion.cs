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
    /// I-18: envia el turno ya evaluado de la idea activa que el barrido por tiempo acaba de activar.
    /// La fachada conserva una sola ruta para enviar y persistir mensajes salientes.
    /// </summary>
    Task EnviarTurnoCoachingPendienteAsync(
        DominioConversacion conversacion,
        Campania campania,
        CancellationToken cancellationToken);
}
