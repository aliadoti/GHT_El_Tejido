using ElTejido.Domain.Respuestas;
using Newtonsoft.Json;

namespace ElTejido.Infrastructure.Respuestas;

internal sealed class RespuestaCosmosDocument
{
    public const string DocumentType = "Respuesta";

    [JsonProperty("id")]
    public string Id { get; init; } = string.Empty;

    [JsonProperty("type")]
    public string Type { get; init; } = DocumentType;

    [JsonProperty("campaniaId")]
    public string CampaniaId { get; init; } = string.Empty;

    [JsonProperty("usuarioId")]
    public string UsuarioId { get; init; } = string.Empty;

    [JsonProperty("preguntaId")]
    public string PreguntaId { get; init; } = string.Empty;

    [JsonProperty("conversacionId")]
    public string ConversacionId { get; init; } = string.Empty;

    [JsonProperty("texto")]
    public string Texto { get; init; } = string.Empty;

    [JsonProperty("canal")]
    public string Canal { get; init; } = "whatsapp";

    [JsonProperty("esRepregunta")]
    public bool EsRepregunta { get; init; }

    [JsonProperty("estado")]
    public string Estado { get; init; } = "recibida";

    [JsonProperty("fecha")]
    public DateTimeOffset Fecha { get; init; }

    [JsonProperty("tagsSnapshot")]
    public IReadOnlyCollection<string> TagsSnapshot { get; init; } = Array.Empty<string>();

    [JsonProperty("ideaIndice")]
    public int? IdeaIndice { get; init; }

    [JsonProperty("respuestaPadreId")]
    public string? RespuestaPadreId { get; init; }

    public static RespuestaCosmosDocument FromDomain(Respuesta respuesta)
        => new()
        {
            Id = respuesta.Id,
            Type = DocumentType,
            CampaniaId = respuesta.CampaniaId,
            UsuarioId = respuesta.UsuarioId,
            PreguntaId = respuesta.PreguntaId,
            ConversacionId = respuesta.ConversacionId,
            Texto = respuesta.Texto,
            Canal = respuesta.Canal,
            EsRepregunta = respuesta.EsRepregunta,
            Estado = MapearEstado(respuesta.Estado),
            Fecha = respuesta.Fecha,
            TagsSnapshot = respuesta.TagsSnapshot,
            IdeaIndice = respuesta.IdeaIndice,
            RespuestaPadreId = respuesta.RespuestaPadreId,
        };

    public Respuesta ToDomain()
        => Respuesta.Crear(
            Id,
            CampaniaId,
            UsuarioId,
            PreguntaId,
            ConversacionId,
            Texto,
            Canal,
            EsRepregunta,
            MapearEstado(Estado),
            Fecha,
            TagsSnapshot,
            IdeaIndice,
            RespuestaPadreId);

    private static string MapearEstado(EstadoRespuesta estado)
        => estado switch
        {
            EstadoRespuesta.Recibida => "recibida",
            EstadoRespuesta.Evaluada => "evaluada",
            EstadoRespuesta.EvaluacionPendiente => "evaluacionPendiente",
            _ => throw new InvalidOperationException($"Estado de respuesta no soportado: {estado}."),
        };

    private static EstadoRespuesta MapearEstado(string estado)
        => estado switch
        {
            "recibida" => EstadoRespuesta.Recibida,
            "evaluada" => EstadoRespuesta.Evaluada,
            "evaluacionPendiente" => EstadoRespuesta.EvaluacionPendiente,
            _ => throw new InvalidOperationException($"Estado de respuesta no soportado en Cosmos: {estado}."),
        };
}
