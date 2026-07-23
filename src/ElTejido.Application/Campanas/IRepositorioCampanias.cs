using ElTejido.Domain.Campanas;

namespace ElTejido.Application.Campanas;

/// <summary>
/// Puerto de persistencia del contenedor Cosmos <c>campaigns</c> para Campania.
/// Cubre REQ Â§11, Â§15, Â§16 y ARQ Â§8-Â§9 sin acoplar la aplicacion a Cosmos.
/// </summary>
public interface IRepositorioCampanias
{
    Task GuardarCampaniaAsync(Campania campania, CancellationToken cancellationToken);

    Task<Campania?> ObtenerCampaniaPorIdAsync(string id, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Campania>> BuscarCampaniasAsync(
        FiltroCampanias filtro,
        CancellationToken cancellationToken);

    /// <summary>
    /// Borra fisicamente el documento de una campania (P-15, purga total de datos de prueba). Acotado a
    /// su propia particion <c>id</c>; idempotente (tolera que ya no exista). No borra las entidades
    /// asociadas en otros contenedores: el servicio de purga las elimina antes en orden seguro.
    /// </summary>
    Task EliminarCampaniaAsync(string id, CancellationToken cancellationToken);
}
