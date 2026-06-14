using ElTejido.Domain.Conversaciones;
using DominioConversacion = ElTejido.Domain.Conversaciones.Conversacion;

namespace ElTejido.Application.Conversacion;

/// <summary>
/// Puerto del contenedor Cosmos <c>conversations</c> (pk <c>campaniaId</c>) para Conversacion y
/// Mensaje (03 §3.6-§3.7). Una conversacion por (usuario, campania, pregunta) en el MVP.
/// </summary>
public interface IRepositorioConversaciones
{
    Task GuardarConversacionAsync(DominioConversacion conversacion, CancellationToken cancellationToken);

    Task<DominioConversacion?> ObtenerConversacionAsync(
        string campaniaId,
        string conversacionId,
        CancellationToken cancellationToken);

    Task GuardarMensajeAsync(Mensaje mensaje, CancellationToken cancellationToken);
}
