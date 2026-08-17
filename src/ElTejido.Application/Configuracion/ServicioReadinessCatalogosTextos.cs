using ElTejido.Application.Campanas;
using ElTejido.Application.Common;
using ElTejido.Application.Conversacion;
using ElTejido.Application.Evaluacion;
using ElTejido.Application.WhatsApp;
using ElTejido.Domain.Campanas;
using ElTejido.Domain.Configuracion;
using ElTejido.Domain.Localizacion;

namespace ElTejido.Application.Configuracion;

/// <summary>DT-P32-02 §4.1: estado real de preparacion del catalogo, sin contenido editorial.</summary>
public interface IServicioReadinessCatalogosTextos
{
    Task<ReadinessCatalogosTextos> ObtenerAsync(string? idioma, CancellationToken cancellationToken);
}

/// <summary>Composición diagnóstica de las mismas políticas usadas por runtime.</summary>
public interface IReadinessMultiidioma : IServicioReadinessCatalogosTextos
{
}

public sealed record ReadinessCatalogosTextos(
    bool GateHabilitado,
    int MaxFrasesPorGrupo,
    int MaxBytesImportacionJson,
    IReadOnlyList<ReadinessIdiomaCatalogoTextos> Idiomas,
    IReadOnlyList<MapeoPlantillaMetaEvaluado> MapeosMeta)
{
    /// <summary>
    /// DT-P32-03 §3.2: señal operativa agregada. Exige catálogos válidos por idioma **y** los
    /// mapeos Meta requeridos estructuralmente configurados. No certifica la aprobación en Meta:
    /// esa comprobación es manual (`QAS/23`).
    /// <para>
    /// DT-P32-03-01 §3: solo participan los pares que exige al menos una campaña **activa**. Los
    /// pares que hoy piden únicamente borradores se siguen enumerando con sus problemas —el
    /// administrador necesita verlos antes de activar— pero no impiden encender el gate para lo que
    /// ya está operando. La guarda de `borrador → activa` es la que evita activar una campaña
    /// incompleta con el gate ON.
    /// </para>
    /// </summary>
    public bool ListoParaGateOn => Idiomas.All(idioma => idioma.Listo)
        && MapeosMeta.Where(mapeo => mapeo.BloqueaGateOn).All(mapeo => mapeo.Listo);
}

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
public sealed class ServicioReadinessCatalogosTextos : IReadinessMultiidioma
{
    private readonly IRepositorioCatalogosTextos _catalogos;
    private readonly IRepositorioCampanias _campanias;
    private readonly OpcionesCatalogoTextos _opciones;
    private readonly OpcionesConversacion _opcionesConversacion;
    private readonly IResolutorTextosGlobales? _textosGlobales;
    private readonly IResolutorContenidoCampania _contenidoCampania;
    private readonly IResolverPlantillaCanal _plantillaCanal;
    private readonly IPoliticaIdiomaLlm _politicaIdiomaLlm;

    public ServicioReadinessCatalogosTextos(
        IRepositorioCatalogosTextos catalogos,
        IRepositorioCampanias campanias,
        OpcionesCatalogoTextos opciones,
        OpcionesConversacion opcionesConversacion,
        OpcionesPlantillaEnvioInicial? plantillaEnvioInicial = null,
        IResolutorTextosGlobales? textosGlobales = null,
        IResolutorContenidoCampania? contenidoCampania = null,
        IResolverPlantillaCanal? plantillaCanal = null,
        IPoliticaIdiomaLlm? politicaIdiomaLlm = null)
    {
        _catalogos = catalogos;
        _campanias = campanias;
        _opciones = opciones;
        _opcionesConversacion = opcionesConversacion;
        _textosGlobales = textosGlobales;
        _contenidoCampania = contenidoCampania ?? new ResolutorContenidoCampania();
        _plantillaCanal = plantillaCanal
            ?? new ResolverPlantillaCanal(plantillaEnvioInicial ?? new OpcionesPlantillaEnvioInicial());
        _politicaIdiomaLlm = politicaIdiomaLlm ?? new PoliticaIdiomaLlm();
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
            detalle,
            // DT-P32-03 §3.2: los pares Meta que exigirian las campanias si el gate se encendiera.
            ValidadorMapeosPlantillaMeta.Evaluar(
                campanias,
                idiomas,
                _plantillaCanal,
                _contenidoCampania));
    }

    private async Task<ReadinessIdiomaCatalogoTextos> ConstruirAsync(
        string idioma,
        IReadOnlyCollection<Campania> campanias,
        CancellationToken cancellationToken)
    {
        var versiones = await _catalogos.BuscarAsync(idioma, estado: null, cancellationToken);
        var idiomaInterno = IdiomaConversacion.Crear(idioma);
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
        var catalogoDiagnosticado = _textosGlobales is null
            ? null
            : await _textosGlobales.ResolverAsync(
                idiomaInterno,
                ModoResolucionTextosGlobales.Diagnostico,
                cancellationToken);
        var activaValida = activo is not null
            && huellaCoincide
            && catalogoDiagnosticado is not ResultadoTextosGlobales.NoDisponible;

        var semillaBase = Revisar(idioma, opcionesLegacy: null);
        var legacy = Revisar(idioma, _opcionesConversacion);
        var bloqueadasCatalogo = activaValida
            ? Array.Empty<CampaniaBloqueadaCatalogoTextos>()
            : campanias
                .Where(campania => campania.IdiomasHabilitados.Contains(idioma, StringComparer.OrdinalIgnoreCase))
                .Select(campania => new CampaniaBloqueadaCatalogoTextos(
                    campania.Id,
                    campania.Nombre,
                    campania.Estado.ToString().ToLowerInvariant(),
                    "catalogo_activo_faltante"))
                .ToArray();
        var bloqueadasContenido = campanias
            .Where(campania => campania.Estado == EstadoCampania.Activa
                && campania.IdiomasInternosHabilitados.Contains(idiomaInterno))
            .Select(campania => new
            {
                Campania = campania,
                Resultado = _contenidoCampania.Resolver(
                    new ContextoLocalizacion(campania, idiomaInterno, CatalogoTextosHabilitado: true)),
            })
            .Where(item => item.Resultado is ResultadoContenidoCampania.NoDisponible)
            .Select(item => new CampaniaBloqueadaCatalogoTextos(
                item.Campania.Id,
                item.Campania.Nombre,
                item.Campania.Estado.ToString().ToLowerInvariant(),
                ((ResultadoContenidoCampania.NoDisponible)item.Resultado).CodigoPrincipal.ToLowerInvariant()))
            .ToArray();
        var directivaDisponible = _politicaIdiomaLlm.Resolver(
            idioma,
            TipoDirectivaIdiomaLlm.SalidaObligatoria) is ResultadoDirectivaIdiomaLlm.Disponible;
        var bloqueadas = bloqueadasCatalogo.Concat(bloqueadasContenido).Distinct().ToArray();

        return new ReadinessIdiomaCatalogoTextos(
            idioma,
            Listo: activaValida && bloqueadasContenido.Length == 0 && directivaDisponible,
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
            return IdiomaConversacion.CodigosSoportados.ToArray();
        }

        if (!IdiomaConversacion.TryCrear(idioma, out var valor))
        {
            throw new ErrorValidacion(
                "El idioma debe ser 'es' o 'en'.",
                new[] { new DetalleError("idioma", "valor_invalido") });
        }

        return [valor.Codigo];
    }
}
