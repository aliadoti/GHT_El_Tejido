using ElTejido.Domain.Identidad;
using ElTejido.Domain.Seguridad;

namespace ElTejido.Application.Seguridad;

/// <summary>
/// Puerto de persistencia del contenedor Cosmos <c>security</c> para CodigoAuthAdmin.
/// Cubre 03 §3.14, 06 §4 y REQ §10.3 sin acoplar la aplicacion a Cosmos.
/// </summary>
public interface IRepositorioCodigosAuth
{
    Task GuardarAsync(CodigoAuthAdmin codigo, CancellationToken cancellationToken);

    /// <summary>Devuelve el codigo mas reciente emitido para el numero administrativo dado.</summary>
    Task<CodigoAuthAdmin?> ObtenerVigenteMasRecienteAsync(
        NumeroWhatsApp numero,
        CancellationToken cancellationToken);
}
