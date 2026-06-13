using ElTejido.Domain.Common;

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
        DateTimeOffset actualizadoEn)
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
            fechaCreacionUtc,
            fechaActualizacionUtc);
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
