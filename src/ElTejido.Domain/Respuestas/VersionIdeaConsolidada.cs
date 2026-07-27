using ElTejido.Domain.Common;
using ElTejido.Domain.Evaluacion;

namespace ElTejido.Domain.Respuestas;

/// <summary>Versión inmutable, propuesta o confirmada, de una <see cref="IdeaConsolidada"/>.</summary>
public sealed class VersionIdeaConsolidada
{
    private VersionIdeaConsolidada(
        string id, string campaniaId, string ideaId, int numeroVersion, string? versionAnteriorId,
        string texto, IReadOnlyCollection<string> aporteIdsAcumulados, IReadOnlyCollection<string> aporteNuevoIds,
        TipoAporteIdea origen, EstadoConfirmacionVersionIdea estadoConfirmacion, string? evaluacionRef,
        string? promptConsolidacionRef, int? versionPromptConsolidacion,
        ConfigLlmSnapshot? configLlmSnapshot, DateTimeOffset generadaEn, DateTimeOffset? confirmadaEn)
    {
        Id = id; CampaniaId = campaniaId; IdeaId = ideaId; NumeroVersion = numeroVersion;
        VersionAnteriorId = versionAnteriorId; Texto = texto; AporteIdsAcumulados = aporteIdsAcumulados;
        AporteNuevoIds = aporteNuevoIds; Origen = origen; EstadoConfirmacion = estadoConfirmacion;
        EvaluacionRef = evaluacionRef; PromptConsolidacionRef = promptConsolidacionRef;
        VersionPromptConsolidacion = versionPromptConsolidacion; ConfigLlmSnapshot = configLlmSnapshot;
        GeneradaEn = generadaEn; ConfirmadaEn = confirmadaEn;
    }

    public string Id { get; }
    public string CampaniaId { get; }
    public string IdeaId { get; }
    public int NumeroVersion { get; }
    public string? VersionAnteriorId { get; }
    public string Texto { get; }
    public IReadOnlyCollection<string> AporteIdsAcumulados { get; }
    public IReadOnlyCollection<string> AporteNuevoIds { get; }
    public TipoAporteIdea Origen { get; }
    public EstadoConfirmacionVersionIdea EstadoConfirmacion { get; }
    public string? EvaluacionRef { get; }
    public string? PromptConsolidacionRef { get; }
    public int? VersionPromptConsolidacion { get; }
    public ConfigLlmSnapshot? ConfigLlmSnapshot { get; }
    public DateTimeOffset GeneradaEn { get; }
    public DateTimeOffset? ConfirmadaEn { get; }

    public static VersionIdeaConsolidada Crear(
        string id, string campaniaId, string ideaId, int numeroVersion, string? versionAnteriorId, string texto,
        IEnumerable<string> aporteIdsAcumulados, IEnumerable<string> aporteNuevoIds, TipoAporteIdea origen,
        EstadoConfirmacionVersionIdea estadoConfirmacion, string? evaluacionRef, string? promptConsolidacionRef,
        int? versionPromptConsolidacion, ConfigLlmSnapshot? configLlmSnapshot, DateTimeOffset generadaEn,
        DateTimeOffset? confirmadaEn = null)
    {
        if (numeroVersion <= 0 || (numeroVersion == 1) != string.IsNullOrWhiteSpace(versionAnteriorId))
        {
            throw new DomainValidationException("VERSION_IDEA_INVALIDA", "La primera versión no tiene anterior y las siguientes sí.");
        }

        var acumulados = NormalizarIds(aporteIdsAcumulados);
        var nuevos = NormalizarIds(aporteNuevoIds);
        if (acumulados.Count == 0 || nuevos.Count == 0 || nuevos.Any(idAporte => !acumulados.Contains(idAporte)))
        {
            throw new DomainValidationException("APORTES_VERSION_IDEA_INVALIDOS", "La versión debe tener aportes nuevos incluidos en los acumulados.");
        }

        if ((estadoConfirmacion == EstadoConfirmacionVersionIdea.Confirmada) != confirmadaEn.HasValue)
        {
            throw new DomainValidationException("CONFIRMACION_VERSION_IDEA_INVALIDA", "Solo una versión confirmada registra fecha de confirmación.");
        }

        return new VersionIdeaConsolidada(
            DomainGuards.Required(id, nameof(id)), DomainGuards.Required(campaniaId, nameof(campaniaId)),
            DomainGuards.Required(ideaId, nameof(ideaId)), numeroVersion, Normalizar(versionAnteriorId),
            DomainGuards.Required(texto, nameof(texto)), acumulados, nuevos, origen, estadoConfirmacion,
            Normalizar(evaluacionRef), Normalizar(promptConsolidacionRef), versionPromptConsolidacion,
            configLlmSnapshot, generadaEn.ToUniversalTime(), confirmadaEn?.ToUniversalTime());
    }

    /// <summary>
    /// Sella la aceptación del participante sin cambiar el texto ni los aportes que ya fueron auditados.
    /// I-19 permite este único cambio de estado de una propuesta antes de enlazar su evaluación.
    /// </summary>
    public VersionIdeaConsolidada Confirmar(DateTimeOffset confirmadaEn)
    {
        if (EstadoConfirmacion != EstadoConfirmacionVersionIdea.Propuesta)
        {
            throw new DomainValidationException("VERSION_IDEA_NO_CONFIRMABLE", "Solo una propuesta puede confirmarse.");
        }

        return Crear(
            Id, CampaniaId, IdeaId, NumeroVersion, VersionAnteriorId, Texto, AporteIdsAcumulados,
            AporteNuevoIds, Origen, EstadoConfirmacionVersionIdea.Confirmada, EvaluacionRef,
            PromptConsolidacionRef, VersionPromptConsolidacion, ConfigLlmSnapshot, GeneradaEn, confirmadaEn);
    }

    private static IReadOnlyCollection<string> NormalizarIds(IEnumerable<string>? ids)
        => (ids ?? Array.Empty<string>()).Select(id => id.Trim()).Where(id => id.Length > 0)
            .Distinct(StringComparer.Ordinal).ToArray();
    private static string? Normalizar(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
