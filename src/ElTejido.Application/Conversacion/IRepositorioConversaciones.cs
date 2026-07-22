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
    /// Hilos ABIERTOS de una campania cuya ultima actividad es anterior a <paramref name="limite"/>.
    /// Sirve al barrido de expiracion (I-17 §7) para cerrarlos por inactividad con la ventana efectiva
    /// de <b>esa</b> campania (minutos por campaña → global → horas legacy).
    /// </summary>
    Task<IReadOnlyCollection<DominioConversacion>> ListarAbiertasInactivasAsync(
        string campaniaId,
        DateTimeOffset limite,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Mensaje>> ListarMensajesAsync(
        string campaniaId,
        string conversacionId,
        CancellationToken cancellationToken);

    Task GuardarMensajeAsync(Mensaje mensaje, CancellationToken cancellationToken);

    /// <summary>
    /// Borra fisicamente las conversaciones y sus mensajes dentro de una campania (P-03, reinicio de
    /// datos de prueba). Con <paramref name="usuarioId"/> = null borra todas las de la campania; con
    /// un usuario, solo las de ese usuario. Acotado a la particion <c>campaniaId</c>; idempotente
    /// (re-invocar sobre datos ya limpios devuelve ceros). Devuelve los conteos borrados.
    /// </summary>
    Task<ConteoBorradoConversaciones> EliminarPorUsuarioAsync(
        string campaniaId,
        string? usuarioId,
        CancellationToken cancellationToken);
}

/// <summary>Conteos del borrado de conversaciones/mensajes de un alcance (P-03).</summary>
public sealed record ConteoBorradoConversaciones(int Conversaciones, int Mensajes);
