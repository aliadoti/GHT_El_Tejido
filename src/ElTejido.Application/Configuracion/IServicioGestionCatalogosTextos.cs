using ElTejido.Domain.Configuracion;

namespace ElTejido.Application.Configuracion;

public interface IServicioGestionCatalogosTextos
{
    Task<IReadOnlyCollection<VersionCatalogoTextos>> BuscarAsync(
        string? idioma,
        EstadoCatalogoTextos? estado,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<VersionCatalogoTextos>> ListarVersionesAsync(
        string familiaId,
        string idioma,
        CancellationToken cancellationToken);

    Task<VersionCatalogoTextos> ObtenerAsync(
        string familiaId,
        string idioma,
        int version,
        CancellationToken cancellationToken);

    Task<VersionCatalogoTextos> CrearAsync(
        SolicitudGuardarCatalogoTextos solicitud,
        string actorId,
        CancellationToken cancellationToken);

    Task<VersionCatalogoTextos> CrearVersionAsync(
        string familiaId,
        string idioma,
        SolicitudContenidoCatalogoTextos? contenido,
        string actorId,
        CancellationToken cancellationToken);

    Task<VersionCatalogoTextos> ImportarAsync(
        SolicitudGuardarCatalogoTextos solicitud,
        string actorId,
        CancellationToken cancellationToken);

    Task<VersionCatalogoTextos> ActualizarBorradorAsync(
        string familiaId,
        string idioma,
        int version,
        SolicitudContenidoCatalogoTextos contenido,
        string etag,
        string actorId,
        CancellationToken cancellationToken);

    Task<VersionCatalogoTextos> ActivarAsync(
        string familiaId,
        string idioma,
        int version,
        string etag,
        string actorId,
        CancellationToken cancellationToken);
}

public sealed record SolicitudContenidoCatalogoTextos(
    IReadOnlyDictionary<string, string> Mensajes,
    IReadOnlyDictionary<string, IReadOnlyCollection<string>> Frases);

public sealed record SolicitudGuardarCatalogoTextos(
    string FamiliaId,
    string Idioma,
    IReadOnlyDictionary<string, string> Mensajes,
    IReadOnlyDictionary<string, IReadOnlyCollection<string>> Frases);
