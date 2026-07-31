using ElTejido.Domain.Conversaciones;

namespace ElTejido.Application.Conversacion;

/// <summary>
/// Puerto P-26 del tipo <c>EnrutamientoAporte</c> (03 §3.6.1) dentro del contenedor existente
/// <c>conversations</c>, particion interna <c>routing:&lt;usuarioId&gt;</c>. El id del documento es
/// determinista por (usuario, whatsappMessageId): guardar es un upsert idempotente y un reintento no
/// crea otro enrutamiento. No hay borrado: el vencimiento es logico para conservar auditoria.
/// </summary>
public interface IRepositorioEnrutamientosAporte
{
    Task GuardarAsync(EnrutamientoAporte enrutamiento, CancellationToken cancellationToken);

    /// <summary>Recupera el enrutamiento del mensaje raiz de un usuario; null si nunca se conservo.</summary>
    Task<EnrutamientoAporte?> ObtenerPorMensajeAsync(
        string usuarioId,
        string whatsappMessageId,
        CancellationToken cancellationToken);

    /// <summary>Enrutamientos del usuario, para resolver la afinidad vigente y auditar selecciones.</summary>
    Task<IReadOnlyCollection<EnrutamientoAporte>> ListarPorUsuarioAsync(
        string usuarioId,
        CancellationToken cancellationToken);
}
