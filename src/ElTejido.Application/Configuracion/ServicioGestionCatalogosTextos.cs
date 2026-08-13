using ElTejido.Application.Common;
using ElTejido.Application.Seguridad;
using ElTejido.Domain.Configuracion;
using ElTejido.Domain.Seguridad;

namespace ElTejido.Application.Configuracion;

public sealed class ServicioGestionCatalogosTextos : IServicioGestionCatalogosTextos
{
    private readonly IRepositorioCatalogosTextos _repositorio;
    private readonly IRepositorioLogSeguridad _auditoria;
    private readonly TimeProvider _tiempo;
    private readonly IInvalidacionCacheCatalogosTextos? _invalidacionCache;

    public ServicioGestionCatalogosTextos(
        IRepositorioCatalogosTextos repositorio,
        IRepositorioLogSeguridad auditoria,
        TimeProvider tiempo,
        IInvalidacionCacheCatalogosTextos? invalidacionCache = null)
    {
        _repositorio = repositorio;
        _auditoria = auditoria;
        _tiempo = tiempo;
        _invalidacionCache = invalidacionCache;
    }

    public Task<IReadOnlyCollection<VersionCatalogoTextos>> BuscarAsync(
        string? idioma,
        EstadoCatalogoTextos? estado,
        CancellationToken cancellationToken)
        => _repositorio.BuscarAsync(
            string.IsNullOrWhiteSpace(idioma) ? null : ValidarIdioma(idioma),
            estado,
            cancellationToken);

    public Task<IReadOnlyCollection<VersionCatalogoTextos>> ListarVersionesAsync(
        string familiaId,
        string idioma,
        CancellationToken cancellationToken)
        => _repositorio.ListarVersionesAsync(Requerir(familiaId, "familiaId"), ValidarIdioma(idioma), cancellationToken);

    public async Task<VersionCatalogoTextos> ObtenerAsync(
        string familiaId,
        string idioma,
        int version,
        CancellationToken cancellationToken)
        => await _repositorio.ObtenerAsync(
                Requerir(familiaId, "familiaId"),
                ValidarIdioma(idioma),
                ValidarVersion(version),
                cancellationToken)
            ?? throw new ErrorNoEncontrado("La version del catalogo no existe.");

    public async Task<VersionCatalogoTextos> CrearAsync(
        SolicitudGuardarCatalogoTextos solicitud,
        string actorId,
        CancellationToken cancellationToken)
    {
        var familiaId = Requerir(solicitud.FamiliaId, "familiaId");
        var idioma = ValidarIdioma(solicitud.Idioma);
        var existentes = await _repositorio.ListarVersionesAsync(familiaId, idioma, cancellationToken);
        if (existentes.Count > 0)
        {
            throw new ErrorConflicto("Ya existe el catalogo para esa familia e idioma; crea una nueva version.");
        }

        var catalogo = CrearCatalogo(
            familiaId,
            idioma,
            1,
            solicitud.Mensajes,
            solicitud.Frases,
            actorId,
            _tiempo.GetUtcNow());
        var creado = await _repositorio.CrearAsync(catalogo, cancellationToken);
        await AuditarAsync("crear", creado.Catalogo, actorId, cancellationToken);
        return creado;
    }

    public async Task<VersionCatalogoTextos> CrearVersionAsync(
        string familiaId,
        string idioma,
        SolicitudContenidoCatalogoTextos? contenido,
        string actorId,
        CancellationToken cancellationToken)
    {
        familiaId = Requerir(familiaId, "familiaId");
        idioma = ValidarIdioma(idioma);
        var versiones = await _repositorio.ListarVersionesAsync(familiaId, idioma, cancellationToken);
        var ultima = versiones.OrderByDescending(x => x.Catalogo.Version).FirstOrDefault()
            ?? throw new ErrorNoEncontrado("El catalogo no existe.");
        var mensajes = contenido?.Mensajes ?? ultima.Catalogo.Mensajes;
        var frases = contenido?.Frases ?? ultima.Catalogo.Frases;
        var ahora = _tiempo.GetUtcNow();
        var catalogo = CrearCatalogo(
            familiaId,
            idioma,
            ultima.Catalogo.Version + 1,
            mensajes,
            frases,
            actorId,
            ahora);
        var creado = await _repositorio.CrearAsync(catalogo, cancellationToken);
        await AuditarAsync("crearVersion", creado.Catalogo, actorId, cancellationToken);
        return creado;
    }

    public async Task<VersionCatalogoTextos> ImportarAsync(
        SolicitudGuardarCatalogoTextos solicitud,
        string actorId,
        CancellationToken cancellationToken)
    {
        var familiaId = Requerir(solicitud.FamiliaId, "familiaId");
        var idioma = ValidarIdioma(solicitud.Idioma);
        var versiones = await _repositorio.ListarVersionesAsync(familiaId, idioma, cancellationToken);
        var version = versiones.Count == 0
            ? 1
            : versiones.Max(x => x.Catalogo.Version) + 1;
        var ahora = _tiempo.GetUtcNow();
        var catalogo = CrearCatalogo(
            familiaId,
            idioma,
            version,
            solicitud.Mensajes,
            solicitud.Frases,
            actorId,
            ahora);
        var creado = await _repositorio.CrearAsync(catalogo, cancellationToken);
        await AuditarAsync("importar", creado.Catalogo, actorId, cancellationToken);
        return creado;
    }

    public async Task<VersionCatalogoTextos> ActualizarBorradorAsync(
        string familiaId,
        string idioma,
        int version,
        SolicitudContenidoCatalogoTextos contenido,
        string etag,
        string actorId,
        CancellationToken cancellationToken)
    {
        var actual = await ObtenerAsync(familiaId, idioma, version, cancellationToken);
        if (actual.Catalogo.Estado != EstadoCatalogoTextos.Borrador)
        {
            throw new ErrorConflicto("Solo se puede editar en sitio una version en borrador.");
        }

        var actualizado = CrearCatalogo(
            actual.Catalogo.FamiliaId,
            actual.Catalogo.Idioma,
            actual.Catalogo.Version,
            contenido.Mensajes,
            contenido.Frases,
            actual.Catalogo.CreadoPor,
            actual.Catalogo.CreadoEn,
            _tiempo.GetUtcNow());
        var guardado = await _repositorio.ReemplazarBorradorAsync(
            actualizado,
            Requerir(etag, "If-Match"),
            cancellationToken);
        await AuditarAsync("editar", guardado.Catalogo, actorId, cancellationToken);
        return guardado;
    }

    public async Task<VersionCatalogoTextos> ActivarAsync(
        string familiaId,
        string idioma,
        int version,
        string etag,
        string actorId,
        CancellationToken cancellationToken)
    {
        var actual = await ObtenerAsync(familiaId, idioma, version, cancellationToken);
        if (actual.Catalogo.Estado is not (EstadoCatalogoTextos.Borrador or EstadoCatalogoTextos.Inactivo))
        {
            throw new ErrorConflicto("Solo una version en borrador o inactiva puede activarse.");
        }

        // Revalida el snapshot completo al activar para impedir que datos historicos invalidos lleguen al runtime.
        ValidadorCatalogoTextosConversacion.ValidarYCalcularHuella(actual.Catalogo.Mensajes, actual.Catalogo.Frases);
        var activo = actual.Catalogo.CambiarEstado(
            EstadoCatalogoTextos.Activo,
            _tiempo.GetUtcNow(),
            Requerir(actorId, "actorId"));
        var guardado = await _repositorio.ActivarAsync(activo, Requerir(etag, "If-Match"), cancellationToken);
        _invalidacionCache?.Invalidar(guardado.Catalogo.Idioma);
        await AuditarAsync(
            actual.Catalogo.Estado == EstadoCatalogoTextos.Inactivo ? "rollback" : "activar",
            guardado.Catalogo,
            actorId,
            cancellationToken);
        return guardado;
    }

    private static CatalogoTextosConversacion CrearCatalogo(
        string familiaId,
        string idioma,
        int version,
        IReadOnlyDictionary<string, string> mensajes,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> frases,
        string actorId,
        DateTimeOffset creadoEn,
        DateTimeOffset? actualizadoEn = null)
    {
        var huella = ValidadorCatalogoTextosConversacion.ValidarYCalcularHuella(mensajes, frases);
        return CatalogoTextosConversacion.Crear(
            familiaId,
            idioma,
            version,
            EstadoCatalogoTextos.Borrador,
            mensajes,
            frases,
            Requerir(actorId, "actorId"),
            aprobadoPor: null,
            creadoEn,
            actualizadoEn ?? creadoEn,
            activadoEn: null,
            huella);
    }

    private async Task AuditarAsync(
        string accion,
        CatalogoTextosConversacion catalogo,
        string actorId,
        CancellationToken cancellationToken)
    {
        var detalle = $"accion={accion};familia={catalogo.FamiliaId};idioma={catalogo.Idioma};" +
            $"version={catalogo.Version};huella={catalogo.Huella}";
        await _auditoria.RegistrarAsync(
            LogSeguridad.Crear(
                "catalogo_" + Guid.NewGuid().ToString("N"),
                TipoEventoSeguridad.CatalogoTextosConversacion,
                Requerir(actorId, "actorId"),
                numero: null,
                resultado: "ok",
                detalle,
                correlationId: null,
                timestamp: _tiempo.GetUtcNow()),
            cancellationToken);
    }

    private static int ValidarVersion(int version)
    {
        if (version <= 0)
        {
            throw new ErrorValidacion(
                "La version debe ser mayor que cero.",
                new[] { new DetalleError("version", "entero_positivo") });
        }

        return version;
    }

    private static string ValidarIdioma(string idioma)
    {
        var valor = Requerir(idioma, "idioma").ToLowerInvariant();
        if (valor is not ("es" or "en"))
        {
            throw new ErrorValidacion(
                "El idioma debe ser 'es' o 'en'.",
                new[] { new DetalleError("idioma", "valor_invalido") });
        }

        return valor;
    }

    private static string Requerir(string? valor, string campo)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            throw new ErrorValidacion(
                $"El campo {campo} es obligatorio.",
                new[] { new DetalleError(campo, "obligatorio") });
        }

        return valor.Trim();
    }
}
