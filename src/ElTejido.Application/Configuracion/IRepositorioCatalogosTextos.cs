using ElTejido.Domain.Configuracion;

namespace ElTejido.Application.Configuracion;

public sealed record VersionCatalogoTextos(CatalogoTextosConversacion Catalogo, string Etag);

/// <summary>Puerto de persistencia del catalogo editorial. Todas las mutaciones usan ETag.</summary>
public interface IRepositorioCatalogosTextos
{
    Task<IReadOnlyCollection<VersionCatalogoTextos>> BuscarAsync(
        string? idioma,
        EstadoCatalogoTextos? estado,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<VersionCatalogoTextos>> ListarVersionesAsync(
        string familiaId,
        string idioma,
        CancellationToken cancellationToken);

    Task<VersionCatalogoTextos?> ObtenerAsync(
        string familiaId,
        string idioma,
        int version,
        CancellationToken cancellationToken);

    Task<VersionCatalogoTextos?> ObtenerActivoAsync(string idioma, CancellationToken cancellationToken);

    Task<VersionCatalogoTextos> CrearAsync(
        CatalogoTextosConversacion catalogo,
        CancellationToken cancellationToken);

    Task<VersionCatalogoTextos> ReemplazarBorradorAsync(
        CatalogoTextosConversacion catalogo,
        string etag,
        CancellationToken cancellationToken);

    /// <summary>Activa el candidato e inactiva el anterior de forma atomica por particion.</summary>
    Task<VersionCatalogoTextos> ActivarAsync(
        CatalogoTextosConversacion catalogoActivo,
        string etag,
        CancellationToken cancellationToken);
}
