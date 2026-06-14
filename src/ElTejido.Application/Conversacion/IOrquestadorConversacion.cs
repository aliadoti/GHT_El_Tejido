using ElTejido.Application.Identidad;
using ElTejido.Application.WhatsApp;

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
}
