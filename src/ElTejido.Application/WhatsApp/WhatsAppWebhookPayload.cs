using System.Text.Json.Serialization;

namespace ElTejido.Application.WhatsApp;

/// <summary>
/// Forma minima del payload estandar de WhatsApp Cloud API
/// (<c>entry[].changes[].value.messages[]</c>, 04 §6.2). El Edge deserializa el cuerpo crudo a este
/// grafo y se lo entrega al gateway para <see cref="IWhatsAppGateway.ParsearWebhook"/>. Solo se
/// modelan los campos que el MVP consume; el resto se ignora.
/// </summary>
public sealed record WhatsAppWebhookPayload
{
    [JsonPropertyName("entry")]
    public IReadOnlyList<WhatsAppEntry>? Entry { get; init; }
}

public sealed record WhatsAppEntry
{
    [JsonPropertyName("changes")]
    public IReadOnlyList<WhatsAppChange>? Changes { get; init; }
}

public sealed record WhatsAppChange
{
    [JsonPropertyName("value")]
    public WhatsAppChangeValue? Value { get; init; }
}

public sealed record WhatsAppChangeValue
{
    [JsonPropertyName("messages")]
    public IReadOnlyList<WhatsAppMessage>? Messages { get; init; }
}

public sealed record WhatsAppMessage
{
    [JsonPropertyName("from")]
    public string? From { get; init; }

    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; init; }

    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("text")]
    public WhatsAppMessageText? Text { get; init; }
}

public sealed record WhatsAppMessageText
{
    [JsonPropertyName("body")]
    public string? Body { get; init; }
}
