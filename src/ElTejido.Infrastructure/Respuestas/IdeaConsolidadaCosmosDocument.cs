using ElTejido.Domain.Respuestas;
using Newtonsoft.Json;

namespace ElTejido.Infrastructure.Respuestas;

internal sealed class IdeaConsolidadaCosmosDocument
{
    public const string DocumentType = "IdeaConsolidada";
    [JsonProperty("id")] public string Id { get; init; } = string.Empty;
    [JsonProperty("type")] public string Type { get; init; } = DocumentType;
    [JsonProperty("campaniaId")] public string CampaniaId { get; init; } = string.Empty;
    [JsonProperty("usuarioId")] public string UsuarioId { get; init; } = string.Empty;
    [JsonProperty("preguntaId")] public string PreguntaId { get; init; } = string.Empty;
    [JsonProperty("conversacionId")] public string ConversacionId { get; init; } = string.Empty;
    [JsonProperty("respuestaRaizId")] public string RespuestaRaizId { get; init; } = string.Empty;
    [JsonProperty("ideaIndice")] public int IdeaIndice { get; init; }
    [JsonProperty("versionConfirmadaRef", NullValueHandling = NullValueHandling.Ignore)] public string? VersionConfirmadaRef { get; init; }
    [JsonProperty("versionPropuestaRef", NullValueHandling = NullValueHandling.Ignore)] public string? VersionPropuestaRef { get; init; }
    [JsonProperty("evaluacionVigenteRef", NullValueHandling = NullValueHandling.Ignore)] public string? EvaluacionVigenteRef { get; init; }
    [JsonProperty("estadoFlujo")] public string EstadoFlujo { get; init; } = "pendienteConfirmacion";
    [JsonProperty("estadoResultado", NullValueHandling = NullValueHandling.Ignore)] public string? EstadoResultado { get; init; }
    [JsonProperty("nivelMadurez")] public string NivelMadurez { get; init; } = "incubacion";
    [JsonProperty("estadoCuraduria", NullValueHandling = NullValueHandling.Ignore)] public string? EstadoCuraduria { get; init; }
    [JsonProperty("motivoCierre", NullValueHandling = NullValueHandling.Ignore)] public string? MotivoCierre { get; init; }
    [JsonProperty("resumenEnviadoEn", NullValueHandling = NullValueHandling.Ignore)] public DateTimeOffset? ResumenEnviadoEn { get; init; }
    [JsonProperty("resumenEnviadoEnVersion", NullValueHandling = NullValueHandling.Ignore)] public int? ResumenEnviadoEnVersion { get; init; }
    [JsonProperty("creadaEn")] public DateTimeOffset CreadaEn { get; init; }
    [JsonProperty("actualizadaEn")] public DateTimeOffset ActualizadaEn { get; init; }

    public static IdeaConsolidadaCosmosDocument FromDomain(IdeaConsolidada idea) => new()
    {
        Id = idea.Id,
        CampaniaId = idea.CampaniaId,
        UsuarioId = idea.UsuarioId,
        PreguntaId = idea.PreguntaId,
        ConversacionId = idea.ConversacionId,
        RespuestaRaizId = idea.RespuestaRaizId,
        IdeaIndice = idea.IdeaIndice,
        VersionConfirmadaRef = idea.VersionConfirmadaRef,
        VersionPropuestaRef = idea.VersionPropuestaRef,
        EvaluacionVigenteRef = idea.EvaluacionVigenteRef,
        EstadoFlujo = Mapear(idea.EstadoFlujo),
        EstadoResultado = idea.EstadoResultado is null ? null : Mapear(idea.EstadoResultado.Value),
        NivelMadurez = idea.NivelMadurez == Domain.Respuestas.NivelMadurez.Maduro ? "maduro" : "incubacion",
        EstadoCuraduria = idea.EstadoCuraduria is null ? null : "pendiente",
        MotivoCierre = idea.MotivoCierre,
        ResumenEnviadoEn = idea.ResumenEnviadoEn,
        ResumenEnviadoEnVersion = idea.ResumenEnviadoEnVersion,
        CreadaEn = idea.CreadaEn,
        ActualizadaEn = idea.ActualizadaEn,
    };

    public IdeaConsolidada ToDomain() => IdeaConsolidada.Restaurar(
        Id, CampaniaId, UsuarioId, PreguntaId, ConversacionId, RespuestaRaizId, IdeaIndice,
        VersionConfirmadaRef, VersionPropuestaRef, EvaluacionVigenteRef, MapearFlujo(EstadoFlujo),
        MapearResultado(EstadoResultado), NivelMadurez == "maduro" ? Domain.Respuestas.NivelMadurez.Maduro : Domain.Respuestas.NivelMadurez.Incubacion,
        EstadoCuraduria is null ? null : EstadoCuraduriaIdea.Pendiente, MotivoCierre, ResumenEnviadoEn, ResumenEnviadoEnVersion, CreadaEn, ActualizadaEn);

    private static string Mapear(EstadoFlujoIdeaConsolidada estado) => estado switch
    { EstadoFlujoIdeaConsolidada.PendienteConfirmacion => "pendienteConfirmacion", EstadoFlujoIdeaConsolidada.EnMejora => "enMejora", EstadoFlujoIdeaConsolidada.EnRevision => "enRevision", _ => "cerrada" };
    private static EstadoFlujoIdeaConsolidada MapearFlujo(string estado) => estado switch
    { "pendienteConfirmacion" => EstadoFlujoIdeaConsolidada.PendienteConfirmacion, "enMejora" => EstadoFlujoIdeaConsolidada.EnMejora, "enRevision" => EstadoFlujoIdeaConsolidada.EnRevision, "cerrada" => EstadoFlujoIdeaConsolidada.Cerrada, _ => throw new InvalidOperationException($"Estado de flujo no soportado: {estado}.") };
    private static string Mapear(EstadoResultadoIdeaConsolidada estado) => estado switch
    { EstadoResultadoIdeaConsolidada.Madura => "madura", EstadoResultadoIdeaConsolidada.Pendiente => "pendiente", _ => "rechazada" };
    private static EstadoResultadoIdeaConsolidada? MapearResultado(string? estado) => estado switch
    { null or "" => null, "madura" => EstadoResultadoIdeaConsolidada.Madura, "pendiente" => EstadoResultadoIdeaConsolidada.Pendiente, "rechazada" => EstadoResultadoIdeaConsolidada.Rechazada, _ => throw new InvalidOperationException($"Estado de resultado no soportado: {estado}.") };
}
