using ElTejido.Domain.Conversaciones;
using Newtonsoft.Json;

namespace ElTejido.Infrastructure.Conversaciones;

internal sealed class MensajeCosmosDocument
{
    public const string DocumentType = "Mensaje";

    [JsonProperty("id")]
    public string Id { get; init; } = string.Empty;

    [JsonProperty("type")]
    public string Type { get; init; } = DocumentType;

    [JsonProperty("campaniaId")]
    public string CampaniaId { get; init; } = string.Empty;

    [JsonProperty("conversacionId")]
    public string ConversacionId { get; init; } = string.Empty;

    [JsonProperty("direccion")]
    public string Direccion { get; init; } = "in";

    [JsonProperty("texto")]
    public string Texto { get; init; } = string.Empty;

    [JsonProperty("whatsappMessageId", NullValueHandling = NullValueHandling.Ignore)]
    public string? WhatsappMessageId { get; init; }

    [JsonProperty("timestamp")]
    public DateTimeOffset Timestamp { get; init; }

    public static MensajeCosmosDocument FromDomain(Mensaje mensaje)
        => new()
        {
            Id = mensaje.Id,
            Type = DocumentType,
            CampaniaId = mensaje.CampaniaId,
            ConversacionId = mensaje.ConversacionId,
            Direccion = mensaje.Direccion == DireccionMensaje.Out ? "out" : "in",
            Texto = mensaje.Texto,
            WhatsappMessageId = mensaje.WhatsappMessageId,
            Timestamp = mensaje.Timestamp,
        };

    public Mensaje ToDomain()
        => Mensaje.Crear(
            Id,
            CampaniaId,
            ConversacionId,
            Direccion == "out" ? DireccionMensaje.Out : DireccionMensaje.In,
            Texto,
            WhatsappMessageId,
            Timestamp);
}
