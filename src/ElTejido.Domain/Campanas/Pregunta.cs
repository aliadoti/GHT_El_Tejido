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
        ConfigMarkdown configMarkdown,
        double? umbralCierreAnticipado)
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
        UmbralCierreAnticipado = umbralCierreAnticipado;
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

    /// <summary>
    /// I-17 — override del umbral (compartido madurez + cierre anticipado) para esta pregunta,
    /// fraccion <c>[0,1]</c> de la escala de la rubrica. <c>null</c> hereda el umbral de la campania
    /// (y este, a su vez, el default global). Precedencia: pregunta → campania → global.
    /// </summary>
    public double? UmbralCierreAnticipado { get; }

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
        ConfigMarkdown configMarkdown,
        double? umbralCierreAnticipado = null)
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

        if (umbralCierreAnticipado is > 1)
        {
            throw new DomainValidationException(
                "UMBRAL_CIERRE_ANTICIPADO_INVALIDO",
                "El umbral de cierre anticipado no puede ser mayor que 1.");
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
            configMarkdown,
            umbralCierreAnticipado);
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
