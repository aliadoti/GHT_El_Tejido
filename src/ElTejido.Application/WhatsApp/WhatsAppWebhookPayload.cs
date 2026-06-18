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

    /// <summary>Notificaciones de estado de mensajes salientes (sent/delivered/read/failed, 04 §6.2).</summary>
    [JsonPropertyName("statuses")]
    public IReadOnlyList<WhatsAppStatus>? Statuses { get; init; }
}

/// <summary>Estado de entrega de un mensaje saliente reportado por Meta (incl. errores de entrega).</summary>
public sealed record WhatsAppStatus
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("errors")]
    public IReadOnlyList<WhatsAppStatusError>? Errors { get; init; }
}

public sealed record WhatsAppStatusError
{
    [JsonPropertyName("code")]
    public int Code { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("error_data")]
    public WhatsAppStatusErrorData? ErrorData { get; init; }
}

public sealed record WhatsAppStatusErrorData
{
    [JsonPropertyName("details")]
    public string? Details { get; init; }
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

    [JsonPropertyName("button")]
    public WhatsAppMessageButton? Button { get; init; }

    [JsonPropertyName("interactive")]
    public WhatsAppMessageInteractive? Interactive { get; init; }
}

public sealed record WhatsAppMessageText
{
    [JsonPropertyName("body")]
    public string? Body { get; init; }
}

public sealed record WhatsAppMessageButton
{
    [JsonPropertyName("text")]
    public string? Text { get; init; }

    [JsonPropertyName("payload")]
    public string? Payload { get; init; }
}

public sealed record WhatsAppMessageInteractive
{
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("button_reply")]
    public WhatsAppMessageInteractiveButtonReply? ButtonReply { get; init; }
}

public sealed record WhatsAppMessageInteractiveButtonReply
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }
}
