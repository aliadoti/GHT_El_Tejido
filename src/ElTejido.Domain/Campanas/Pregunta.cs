using ElTejido.Domain.Common;

namespace ElTejido.Domain.Campanas;

public sealed class Pregunta
{
    private Pregunta(
        string id,
        string texto,
        string instruccion,
        string categoria,
        int orden,
        EstadoRegistro estado,
        string? rubricaRef,
        int? versionRubrica,
        IReadOnlyDictionary<string, string> promptRefs,
        int maxRepreguntas,
        LimitesSeguridad limitesSeguridad,
        ConfigMarkdown configMarkdown)
    {
        Id = id;
        Texto = texto;
        Instruccion = instruccion;
        Categoria = categoria;
        Orden = orden;
        Estado = estado;
        RubricaRef = rubricaRef;
        VersionRubrica = versionRubrica;
        PromptRefs = promptRefs;
        MaxRepreguntas = maxRepreguntas;
        LimitesSeguridad = limitesSeguridad;
        ConfigMarkdown = configMarkdown;
    }

    public string Id { get; }

    public string Texto { get; }

    public string Instruccion { get; }

    public string Categoria { get; }

    public int Orden { get; }

    public EstadoRegistro Estado { get; }

    public string? RubricaRef { get; }

    public int? VersionRubrica { get; }

    public IReadOnlyDictionary<string, string> PromptRefs { get; }

    public int MaxRepreguntas { get; }

    public LimitesSeguridad LimitesSeguridad { get; }

    public ConfigMarkdown ConfigMarkdown { get; }

    public static Pregunta Crear(
        string id,
        string texto,
        string instruccion,
        string categoria,
        int orden,
        EstadoRegistro estado,
        string? rubricaRef,
        int? versionRubrica,
        IReadOnlyDictionary<string, string>? promptRefs,
        int maxRepreguntas,
        LimitesSeguridad limitesSeguridad,
        ConfigMarkdown configMarkdown)
    {
        if (orden <= 0)
        {
            throw new DomainValidationException(
                "ORDEN_INVALIDO",
                "El orden debe ser mayor que cero.");
        }

        if (versionRubrica is <= 0)
        {
            throw new DomainValidationException(
                "VERSION_RUBRICA_INVALIDA",
                "La version de rubrica debe ser mayor que cero.");
        }

        if (maxRepreguntas < 0)
        {
            throw new DomainValidationException(
                "MAX_REPREGUNTAS_INVALIDO",
                "El maximo de repreguntas no puede ser negativo.");
        }

        return new Pregunta(
            DomainGuards.Required(id, nameof(id)),
            DomainGuards.Required(texto, nameof(texto)),
            DomainGuards.Required(instruccion, nameof(instruccion)),
            DomainGuards.Required(categoria, nameof(categoria)),
            orden,
            estado,
            NormalizeOptional(rubricaRef),
            versionRubrica,
            NormalizeMap(promptRefs),
            maxRepreguntas,
            limitesSeguridad,
            configMarkdown);
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
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
