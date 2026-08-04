using ElTejido.Domain.Seguridad;

namespace ElTejido.Application.Seguridad;

/// <summary>
/// Puerto append-only del contenedor Cosmos <c>security</c> para LogSeguridad.
/// Cubre 03 §3.15, 10 §6.4 y REQ §30.
/// </summary>
public interface IRepositorioLogSeguridad
{
    Task RegistrarAsync(LogSeguridad log, CancellationToken cancellationToken);

    /// <summary>P-27: llamadas reales del clasificador por usuario/campaña.</summary>
    Task<int> ContarClasificacionesIntencionControlUsuarioAsync(
        string campaniaId,
        string usuarioId,
        CancellationToken cancellationToken) => Task.FromResult(0);

    /// <summary>P-26/P-27: variante con ventana móvil para campañas continuas.</summary>
    Task<int> ContarClasificacionesIntencionControlUsuarioAsync(
        string campaniaId,
        string usuarioId,
        DateTimeOffset desde,
        CancellationToken cancellationToken)
        => ContarClasificacionesIntencionControlUsuarioAsync(campaniaId, usuarioId, cancellationToken);

    /// <summary>P-27: tokens del clasificador para el presupuesto acumulado de campaña.</summary>
    Task<long> SumarTokensClasificacionesIntencionControlCampaniaAsync(
        string campaniaId,
        CancellationToken cancellationToken) => Task.FromResult(0L);
}
