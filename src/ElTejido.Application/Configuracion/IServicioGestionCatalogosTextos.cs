using ElTejido.Application.Common;
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

    /// <summary>
    /// DT-P32-02 §2.1/§4: crea una version nueva en borrador desde una semilla, distinguiendo en la
    /// auditoria si el origen fue la base curada o la fotografia legacy. Nunca activa ni sobrescribe.
    /// </summary>
    Task<VersionCatalogoTextos> CrearDesdeSemillaAsync(
        SolicitudGuardarCatalogoTextos solicitud,
        OrigenSemillaCatalogoTextos origen,
        string actorId,
        CancellationToken cancellationToken);

    /// <summary>
    /// DT-P32-02 §3.3: prevalida una semilla sin escribir nada. Devuelve todos los errores
    /// detectables y registra la revision en auditoria con conteos, nunca con contenido.
    /// </summary>
    Task<ResultadoPrevalidacionCatalogoTextos> PrevalidarSemillaAsync(
        SolicitudGuardarCatalogoTextos solicitud,
        OrigenSemillaCatalogoTextos origen,
        string actorId,
        CancellationToken cancellationToken);

    /// <summary>
    /// DT-P32-02 §3.3: prevalida el mismo cuerpo que recibe la importacion masiva. No escribe, no
    /// invalida cache y no devuelve textos; incluye los defectos de formato ya detectados.
    /// </summary>
    Task<ResultadoPrevalidacionCatalogoTextos> PrevalidarImportacionAsync(
        SolicitudEdicionMasivaCatalogoTextos solicitud,
        string actorId,
        CancellationToken cancellationToken);

    /// <summary>
    /// DT-P32-02 §2.3/§3.1: importa el JSON editado como una <b>version nueva en borrador</b>. Ejecuta
    /// exactamente la misma prevalidacion: si algo falla lanza <see cref="ErrorValidacion"/> con todos
    /// los detalles y no escribe nada. Nunca activa ni sobrescribe la version activa o el borrador
    /// seleccionado, y los metadatos del archivo (version, estado, huella, ETag) se ignoran.
    /// </summary>
    Task<VersionCatalogoTextos> ImportarMasivoAsync(
        SolicitudEdicionMasivaCatalogoTextos solicitud,
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

/// <summary>DT-P32-02 §2.1: los dos origenes de borrador que ahora estan separados.</summary>
public enum OrigenSemillaCatalogoTextos
{
    /// <summary>Base curada compilada `es/en`, independiente de App Settings.</summary>
    Base,

    /// <summary>Fotografia de la configuracion legacy efectiva del ambiente.</summary>
    Legacy,
}

public sealed record SolicitudContenidoCatalogoTextos(
    IReadOnlyDictionary<string, string> Mensajes,
    IReadOnlyDictionary<string, IReadOnlyCollection<string>> Frases);

public sealed record SolicitudGuardarCatalogoTextos(
    string FamiliaId,
    string Idioma,
    IReadOnlyDictionary<string, string> Mensajes,
    IReadOnlyDictionary<string, IReadOnlyCollection<string>> Frases);
