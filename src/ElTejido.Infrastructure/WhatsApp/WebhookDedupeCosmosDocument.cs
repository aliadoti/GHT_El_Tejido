using Newtonsoft.Json;

namespace ElTejido.Infrastructure.WhatsApp;

internal sealed class WebhookDedupeCosmosDocument
{
    public const string DocumentType = "WebhookDedupe";
    public const int TimeToLiveSeconds = 604800;

    [JsonProperty("id")]
    public string Id { get; init; } = string.Empty;

    [JsonProperty("type")]
    public string Type { get; init; } = DocumentType;

    [JsonProperty("procesadoEn")]
    public DateTimeOffset ProcesadoEn { get; init; }

    [JsonProperty("ttl")]
    public int Ttl { get; init; } = TimeToLiveSeconds;

    public static WebhookDedupeCosmosDocument Create(string whatsappMessageId, DateTimeOffset procesadoEn)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(whatsappMessageId);

        return new WebhookDedupeCosmosDocument
        {
            Id = whatsappMessageId.Trim(),
            Type = DocumentType,
            ProcesadoEn = procesadoEn.ToUniversalTime(),
            Ttl = TimeToLiveSeconds,
        };
    }
}
