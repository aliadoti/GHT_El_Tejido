using ElTejido.Application.Campanas;
using ElTejido.Application.Common;
using ElTejido.Application.Conversacion;
using ElTejido.Domain.Campanas;
using ElTejido.Domain.Configuracion;

namespace ElTejido.Application.Configuracion;

/// <summary>DT-P32-02 §4.1: estado real de preparacion del catalogo, sin contenido editorial.</summary>
public interface IServicioReadinessCatalogosTextos
{
    Task<ReadinessCatalogosTextos> ObtenerAsync(string? idioma, CancellationToken cancellationToken);
}

public sealed record ReadinessCatalogosTextos(
    bool GateHabilitado,
    int MaxFrasesPorGrupo,
    int MaxBytesImportacionJson,
    IReadOnlyList<ReadinessIdiomaCatalogoTextos> Idiomas);

public sealed record ReadinessIdiomaCatalogoTextos(
    string Idioma,
    bool Listo,
    bool TieneActivo,
    int? VersionActiva,
    string? HuellaActiva,
    bool ActivaValida,
    IReadOnlyList<DetalleError> ProblemasActiva,
    bool TieneBorrador,
    int TotalVersiones,
    bool SemillaBaseDisponible,
    bool LegacyValido,
    ConteosCatalogoTextos ConteosLegacy,
    IReadOnlyList<DetalleError> ProblemasLegacy,
    IReadOnlyList<CampaniaBloqueadaCatalogoTextos> CampaniasBloqueadas);

public sealed record CampaniaBloqueadaCatalogoTextos(string CampaniaId, string Nombre, string Estado, string Motivo);

/// <inheritdoc />
public sealed class ServicioReadinessCatalogosTextos : IServicioReadinessCatalogosTextos
{
    private static readonly string[] IdiomasSoportados = ["es", "en"];

    private readonly IRepositorioCatalogosTextos _catalogos;
    private readonly IRepositorioCampanias _campanias;
    private readonly OpcionesCatalogoTextos _opciones;
    private readonly OpcionesConversacion _opcionesConversacion;

    public ServicioReadinessCatalogosTextos(
        IRepositorioCatalogosTextos catalogos,
        IRepositorioCampanias campanias,
        OpcionesCatalogoTextos opciones,
        OpcionesConversacion opcionesConversacion)
    {
        _catalogos = catalogos;
        _campanias = campanias;
        _opciones = opciones;
        _opcionesConversacion = opcionesConversacion;
    }

    public async Task<ReadinessCatalogosTextos> ObtenerAsync(
        string? idioma,
        CancellationToken cancellationToken)
    {
        var idiomas = ResolverIdiomas(idioma);
        var campanias = await ListarCampaniasRelevantesAsync(cancellationToken);
        var detalle = new List<ReadinessIdiomaCatalogoTextos>();
        foreach (var valor in idiomas)
        {
            detalle.Add(await ConstruirAsync(valor, campanias, cancellationToken));
        }

        return new ReadinessCatalogosTextos(
            // El gate real del proceso; `/efectivo` es preview y no prueba por si solo este valor.
            _opciones.Habilitado,
            _opciones.MaxFrasesPorGrupo,
            _opciones.MaxBytesImportacionJson,
            detalle);
    }

    private async Task<ReadinessIdiomaCatalogoTextos> ConstruirAsync(
        string idioma,
        IReadOnlyCollection<Campania> campanias,
        CancellationToken cancellationToken)
    {
        var versiones = await _catalogos.BuscarAsync(idioma, estado: null, cancellationToken);
        var activo = versiones.FirstOrDefault(x => x.Catalogo.Estado == EstadoCatalogoTextos.Activo);
        var problemasActiva = activo is null
            ? Array.Empty<DetalleError>()
            : Revisar(activo.Catalogo.Mensajes, activo.Catalogo.Frases).Errores.ToArray();
        var huellaCoincide = activo is not null
            && problemasActiva.Length == 0
            && string.Equals(
                ValidadorCatalogoTextosConversacion.ValidarYCalcularHuella(
                    activo.Catalogo.Mensajes,
                    activo.Catalogo.Frases,
                    _opciones.Limites),
                activo.Catalogo.Huella,
                StringComparison.Ordinal);
        var activaValida = activo is not null && huellaCoincide;

        var semillaBase = Revisar(idioma, opcionesLegacy: null);
        var legacy = Revisar(idioma, _opcionesConversacion);
        var bloqueadas = activaValida
            ? Array.Empty<CampaniaBloqueadaCatalogoTextos>()
            : campanias
                .Where(campania => campania.IdiomasHabilitados.Contains(idioma, StringComparer.OrdinalIgnoreCase))
                .Select(campania => new CampaniaBloqueadaCatalogoTextos(
                    campania.Id,
                    campania.Nombre,
                    campania.Estado.ToString().ToLowerInvariant(),
                    "catalogo_activo_faltante"))
                .ToArray();

        return new ReadinessIdiomaCatalogoTextos(
            idioma,
            Listo: activaValida,
            TieneActivo: activo is not null,
            VersionActiva: activo?.Catalogo.Version,
            HuellaActiva: activo?.Catalogo.Huella,
            ActivaValida: activaValida,
            ProblemasActiva: activo is not null && !huellaCoincide && problemasActiva.Length == 0
                ? [new DetalleError("huella", "no_coincide_con_contenido")]
                : problemasActiva,
            TieneBorrador: versiones.Any(x => x.Catalogo.Estado == EstadoCatalogoTextos.Borrador),
            TotalVersiones: versiones.Count,
            SemillaBaseDisponible: semillaBase.Valido,
            LegacyValido: legacy.Valido,
            ConteosLegacy: legacy.Conteos,
            ProblemasLegacy: legacy.Errores,
            CampaniasBloqueadas: bloqueadas);
    }

    private ResultadoPrevalidacionCatalogoTextos Revisar(string idioma, OpcionesConversacion? opcionesLegacy)
    {
        var semilla = opcionesLegacy is null
            ? CatalogosTextosSemilla.CrearBase(idioma)
            : CatalogosTextosSemilla.CrearDesdeLegacy(idioma, opcionesLegacy);
        return Revisar(semilla.Mensajes, semilla.Frases, semilla.FamiliaId, idioma);
    }

    private ResultadoPrevalidacionCatalogoTextos Revisar(
        IReadOnlyDictionary<string, string> mensajes,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> frases,
        string familiaId = CatalogosTextosSemilla.FamiliaId,
        string idioma = "")
        => ValidadorCatalogoTextosConversacion.Prevalidar(familiaId, idioma, mensajes, frases, _opciones.Limites);

    private async Task<IReadOnlyCollection<Campania>> ListarCampaniasRelevantesAsync(
        CancellationToken cancellationToken)
    {
        var activas = await _campanias.BuscarCampaniasAsync(
            new FiltroCampanias(EstadoCampania.Activa),
            cancellationToken);
        var borradores = await _campanias.BuscarCampaniasAsync(
            new FiltroCampanias(EstadoCampania.Borrador),
            cancellationToken);
        return activas.Concat(borradores).ToArray();
    }

    private static IReadOnlyList<string> ResolverIdiomas(string? idioma)
    {
        if (string.IsNullOrWhiteSpace(idioma))
        {
            return IdiomasSoportados;
        }

        var valor = idioma.Trim().ToLowerInvariant();
        if (!IdiomasSoportados.Contains(valor, StringComparer.Ordinal))
        {
            throw new ErrorValidacion(
                "El idioma debe ser 'es' o 'en'.",
                new[] { new DetalleError("idioma", "valor_invalido") });
        }

        return [valor];
    }
}
