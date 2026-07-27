using ElTejido.Domain.Evaluacion;
using ElTejido.Domain.Respuestas;
using Newtonsoft.Json;

namespace ElTejido.Infrastructure.Respuestas;

internal sealed class VersionIdeaConsolidadaCosmosDocument
{
    public const string DocumentType = "VersionIdeaConsolidada";
    [JsonProperty("id")] public string Id { get; init; } = string.Empty;
    [JsonProperty("type")] public string Type { get; init; } = DocumentType;
    [JsonProperty("campaniaId")] public string CampaniaId { get; init; } = string.Empty;
    [JsonProperty("ideaId")] public string IdeaId { get; init; } = string.Empty;
    [JsonProperty("numeroVersion")] public int NumeroVersion { get; init; }
    [JsonProperty("versionAnteriorId", NullValueHandling = NullValueHandling.Ignore)] public string? VersionAnteriorId { get; init; }
    [JsonProperty("texto")] public string Texto { get; init; } = string.Empty;
    [JsonProperty("aporteIdsAcumulados")] public IReadOnlyCollection<string> AporteIdsAcumulados { get; init; } = Array.Empty<string>();
    [JsonProperty("aporteNuevoIds")] public IReadOnlyCollection<string> AporteNuevoIds { get; init; } = Array.Empty<string>();
    [JsonProperty("origen")] public string Origen { get; init; } = "inicial";
    [JsonProperty("estadoConfirmacion")] public string EstadoConfirmacion { get; init; } = "propuesta";
    [JsonProperty("evaluacionRef", NullValueHandling = NullValueHandling.Ignore)] public string? EvaluacionRef { get; init; }
    [JsonProperty("promptConsolidacionRef", NullValueHandling = NullValueHandling.Ignore)] public string? PromptConsolidacionRef { get; init; }
    [JsonProperty("versionPromptConsolidacion", NullValueHandling = NullValueHandling.Ignore)] public int? VersionPromptConsolidacion { get; init; }
    [JsonProperty("configLLMSnapshot", NullValueHandling = NullValueHandling.Ignore)] public ConfigSnapshotDocument? ConfigLlmSnapshot { get; init; }
    [JsonProperty("generadaEn")] public DateTimeOffset GeneradaEn { get; init; }
    [JsonProperty("confirmadaEn", NullValueHandling = NullValueHandling.Ignore)] public DateTimeOffset? ConfirmadaEn { get; init; }

    public static VersionIdeaConsolidadaCosmosDocument FromDomain(VersionIdeaConsolidada version) => new()
    {
        Id = version.Id,
        CampaniaId = version.CampaniaId,
        IdeaId = version.IdeaId,
        NumeroVersion = version.NumeroVersion,
        VersionAnteriorId = version.VersionAnteriorId,
        Texto = version.Texto,
        AporteIdsAcumulados = version.AporteIdsAcumulados,
        AporteNuevoIds = version.AporteNuevoIds,
        Origen = Mapear(version.Origen),
        EstadoConfirmacion = Mapear(version.EstadoConfirmacion),
        EvaluacionRef = version.EvaluacionRef,
        PromptConsolidacionRef = version.PromptConsolidacionRef,
        VersionPromptConsolidacion = version.VersionPromptConsolidacion,
        ConfigLlmSnapshot = version.ConfigLlmSnapshot is null ? null : new ConfigSnapshotDocument
        {
            Proveedor = version.ConfigLlmSnapshot.Proveedor,
            Modelo = version.ConfigLlmSnapshot.Modelo,
            Endpoint = version.ConfigLlmSnapshot.Endpoint,
            Parametros = new Dictionary<string, object?>(version.ConfigLlmSnapshot.Parametros),
        },
        GeneradaEn = version.GeneradaEn,
        ConfirmadaEn = version.ConfirmadaEn,
    };

    public VersionIdeaConsolidada ToDomain() => VersionIdeaConsolidada.Crear(
        Id, CampaniaId, IdeaId, NumeroVersion, VersionAnteriorId, Texto, AporteIdsAcumulados, AporteNuevoIds,
        MapearOrigen(Origen), MapearConfirmacion(EstadoConfirmacion), EvaluacionRef, PromptConsolidacionRef,
        VersionPromptConsolidacion, ConfigLlmSnapshot is null ? null : new ConfigLlmSnapshot(
            ConfigLlmSnapshot.Proveedor, ConfigLlmSnapshot.Modelo, ConfigLlmSnapshot.Endpoint, ConfigLlmSnapshot.Parametros),
        GeneradaEn, ConfirmadaEn);

    private static string Mapear(TipoAporteIdea origen) => origen switch
    { TipoAporteIdea.Inicial => "inicial", TipoAporteIdea.Complemento => "complemento", TipoAporteIdea.Correccion => "correccion", _ => "nuevaIdea" };
    private static TipoAporteIdea MapearOrigen(string origen) => origen switch
    { "inicial" => TipoAporteIdea.Inicial, "complemento" => TipoAporteIdea.Complemento, "correccion" => TipoAporteIdea.Correccion, "nuevaIdea" => TipoAporteIdea.NuevaIdea, _ => throw new InvalidOperationException($"Origen de versión no soportado: {origen}.") };
    private static string Mapear(EstadoConfirmacionVersionIdea estado) => estado switch
    { EstadoConfirmacionVersionIdea.Propuesta => "propuesta", EstadoConfirmacionVersionIdea.Confirmada => "confirmada", EstadoConfirmacionVersionIdea.Descartada => "descartada", _ => "expirada" };
    private static EstadoConfirmacionVersionIdea MapearConfirmacion(string estado) => estado switch
    { "propuesta" => EstadoConfirmacionVersionIdea.Propuesta, "confirmada" => EstadoConfirmacionVersionIdea.Confirmada, "descartada" => EstadoConfirmacionVersionIdea.Descartada, "expirada" => EstadoConfirmacionVersionIdea.Expirada, _ => throw new InvalidOperationException($"Confirmación de versión no soportada: {estado}.") };

    internal sealed class ConfigSnapshotDocument
    {
        [JsonProperty("proveedor")] public string Proveedor { get; init; } = string.Empty;
        [JsonProperty("modelo")] public string Modelo { get; init; } = string.Empty;
        [JsonProperty("endpoint")] public string Endpoint { get; init; } = string.Empty;
        [JsonProperty("parametros")] public Dictionary<string, object?> Parametros { get; init; } = new();
    }
}
