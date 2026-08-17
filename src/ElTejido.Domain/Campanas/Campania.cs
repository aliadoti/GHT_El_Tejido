using ElTejido.Domain.Common;
using ElTejido.Domain.Localizacion;

namespace ElTejido.Domain.Campanas;

public sealed class Campania
{
    private Campania(
        string id,
        string nombre,
        string descripcion,
        string objetivo,
        EstadoCampania estado,
        IReadOnlyCollection<MensajeInicial> mensajesIniciales,
        IReadOnlyCollection<Pregunta> preguntas,
        string rubricaRef,
        IReadOnlyDictionary<string, string> promptRefs,
        string configLlmRef,
        ConfigMarkdown configMarkdown,
        ConfigConversacional configConversacional,
        LimitesSeguridad configSeguridad,
        IReadOnlyCollection<string> usuariosHabilitados,
        IReadOnlyCollection<IdiomaConversacion> idiomasHabilitados,
        IReadOnlyDictionary<string, LocalizacionCampania> localizaciones,
        DateTimeOffset creadoEn,
        DateTimeOffset actualizadoEn)
    {
        Id = id;
        Nombre = nombre;
        Descripcion = descripcion;
        Objetivo = objetivo;
        Estado = estado;
        MensajesIniciales = mensajesIniciales;
        Preguntas = preguntas;
        RubricaRef = rubricaRef;
        PromptRefs = promptRefs;
        ConfigLlmRef = configLlmRef;
        ConfigMarkdown = configMarkdown;
        ConfigConversacional = configConversacional;
        ConfigSeguridad = configSeguridad;
        UsuariosHabilitados = usuariosHabilitados;
        IdiomasInternosHabilitados = idiomasHabilitados;
        IdiomasHabilitados = idiomasHabilitados.Select(idioma => idioma.Codigo).ToArray();
        Localizaciones = localizaciones;
        CreadoEn = creadoEn;
        ActualizadoEn = actualizadoEn;
    }

    public string Id { get; }

    public string Nombre { get; }

    public string Descripcion { get; }

    public string Objetivo { get; }

    public EstadoCampania Estado { get; }

    public IReadOnlyCollection<MensajeInicial> MensajesIniciales { get; }

    public IReadOnlyCollection<Pregunta> Preguntas { get; }

    public string RubricaRef { get; }

    public IReadOnlyDictionary<string, string> PromptRefs { get; }

    public string ConfigLlmRef { get; }

    public ConfigMarkdown ConfigMarkdown { get; }

    public ConfigConversacional ConfigConversacional { get; }

    public LimitesSeguridad ConfigSeguridad { get; }

    public IReadOnlyCollection<string> UsuariosHabilitados { get; }

    /// <summary>Idiomas editoriales admitidos por la campaña; un documento histórico equivale a <c>es</c>.</summary>
    public IReadOnlyCollection<string> IdiomasHabilitados { get; }

    public IReadOnlyCollection<IdiomaConversacion> IdiomasInternosHabilitados { get; }

    /// <summary>Contenido editorial por idioma, indexado por código ISO corto.</summary>
    public IReadOnlyDictionary<string, LocalizacionCampania> Localizaciones { get; }

    public DateTimeOffset CreadoEn { get; }

    public DateTimeOffset ActualizadoEn { get; }

    public bool PermiteInteraccion => Estado == EstadoCampania.Activa;

    public static Campania Crear(
        string id,
        string nombre,
        string descripcion,
        string objetivo,
        EstadoCampania estado,
        IEnumerable<MensajeInicial>? mensajesIniciales,
        IEnumerable<Pregunta>? preguntas,
        string rubricaRef,
        IReadOnlyDictionary<string, string>? promptRefs,
        string configLlmRef,
        ConfigMarkdown configMarkdown,
        ConfigConversacional configConversacional,
        LimitesSeguridad configSeguridad,
        IEnumerable<string>? usuariosHabilitados,
        DateTimeOffset creadoEn,
        DateTimeOffset actualizadoEn,
        IEnumerable<string>? idiomasHabilitados = null,
        IReadOnlyDictionary<string, LocalizacionCampania>? localizaciones = null)
    {
        var fechaCreacionUtc = creadoEn.ToUniversalTime();
        var fechaActualizacionUtc = actualizadoEn.ToUniversalTime();

        if (fechaActualizacionUtc < fechaCreacionUtc)
        {
            throw new DomainValidationException(
                "FECHA_ACTUALIZACION_INVALIDA",
                "La fecha de actualizacion no puede ser anterior a la fecha de creacion.");
        }

        return new Campania(
            DomainGuards.Required(id, nameof(id)),
            DomainGuards.Required(nombre, nameof(nombre)),
            DomainGuards.Required(descripcion, nameof(descripcion)),
            DomainGuards.Required(objetivo, nameof(objetivo)),
            estado,
            NormalizeCollection(mensajesIniciales),
            NormalizeCollection(preguntas),
            DomainGuards.Required(rubricaRef, nameof(rubricaRef)),
            NormalizeMap(promptRefs),
            DomainGuards.Required(configLlmRef, nameof(configLlmRef)),
            configMarkdown,
            configConversacional,
            configSeguridad,
            NormalizeStrings(usuariosHabilitados),
            NormalizeIdiomas(idiomasHabilitados),
            NormalizeLocalizaciones(localizaciones),
            fechaCreacionUtc,
            fechaActualizacionUtc);
    }

    /// <summary>
    /// Resuelve el contenido para un idioma. El español conserva el respaldo de los campos históricos;
    /// para inglés no existe fallback silencioso a español.
    /// </summary>
    public bool TryObtenerLocalizacion(string idioma, out LocalizacionCampania localizacion)
    {
        if (!IdiomaConversacion.TryCrear(idioma, out var idiomaInterno)
            || !IdiomasInternosHabilitados.Contains(idiomaInterno))
        {
            localizacion = null!;
            return false;
        }

        if (Localizaciones.TryGetValue(idiomaInterno.Codigo, out localizacion!))
        {
            return true;
        }

        if (idiomaInterno == IdiomaConversacion.Espanol)
        {
            localizacion = LocalizacionCampania.Crear(
                IdiomaConversacion.CodigoEspanol,
                Nombre,
                Descripcion,
                Objetivo,
                ConfigConversacional.MensajeCierre,
                MensajesIniciales.ToDictionary(
                    mensaje => mensaje.Id,
                    mensaje => new LocalizacionMensajeInicial(mensaje.Texto, null),
                    StringComparer.Ordinal),
                Preguntas.ToDictionary(
                    pregunta => pregunta.Id,
                    pregunta => new LocalizacionPregunta(pregunta.Texto, pregunta.Instruccion),
                    StringComparer.Ordinal));
            return true;
        }

        localizacion = null!;
        return false;
    }

    private static IReadOnlyCollection<T> NormalizeCollection<T>(IEnumerable<T>? values)
    {
        return values?.ToArray() ?? Array.Empty<T>();
    }

    private static IReadOnlyCollection<string> NormalizeStrings(IEnumerable<string>? values)
    {
        if (values is null)
        {
            return Array.Empty<string>();
        }

        return values
            .Select(value => value.Trim())
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyCollection<IdiomaConversacion> NormalizeIdiomas(IEnumerable<string>? values)
    {
        var idiomas = NormalizeStrings(values)
            .Select(IdiomaConversacion.Crear)
            .Distinct()
            .ToArray();
        if (idiomas.Length == 0)
        {
            return new[] { IdiomaConversacion.Espanol };
        }

        return idiomas;
    }

    private static IReadOnlyDictionary<string, LocalizacionCampania> NormalizeLocalizaciones(
        IReadOnlyDictionary<string, LocalizacionCampania>? values)
    {
        if (values is null || values.Count == 0)
        {
            return new Dictionary<string, LocalizacionCampania>(StringComparer.Ordinal);
        }

        return values
            .Where(value => !string.IsNullOrWhiteSpace(value.Key))
            .ToDictionary(
                value => IdiomaConversacion.Crear(value.Key).Codigo,
                value => value.Value,
                StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, string> NormalizeMap(IReadOnlyDictionary<string, string>? values)
    {
        if (values is null || values.Count == 0)
        {
            return new Dictionary<string, string>();
        }

        return values
            .Where(value => !string.IsNullOrWhiteSpace(value.Key) && !string.IsNullOrWhiteSpace(value.Value))
            .ToDictionary(
                value => value.Key.Trim(),
                value => value.Value.Trim(),
                StringComparer.Ordinal);
    }
}
