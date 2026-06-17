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

    Task<IReadOnlyCollection<DominioConversacion>> ListarConversacionesAsync(
        string campaniaId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Hilos ABIERTOS (cualquier campania) cuya ultima actividad es anterior a <paramref name="limite"/>.
    /// Sirve al barrido de expiracion para cerrarlos por inactividad.
    /// </summary>
    Task<IReadOnlyCollection<DominioConversacion>> ListarAbiertasInactivasAsync(
        DateTimeOffset limite,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Mensaje>> ListarMensajesAsync(
        string campaniaId,
        string conversacionId,
        CancellationToken cancellationToken);

    Task GuardarMensajeAsync(Mensaje mensaje, CancellationToken cancellationToken);
}
