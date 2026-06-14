namespace ElTejido.Application.WhatsApp;

/// <summary>
/// Cola in-process de payloads del webhook (02 §5, ARQ §4.2): el endpoint hace ack 200 inmediato y
/// encola; un <c>IHostedService</c> los procesa. El adaptador concreto vive en Infrastructure.
/// </summary>
public interface IColaWebhook
{
    /// <summary>Encola un payload ya verificado (firma valida) para procesamiento asincrono.</summary>
    ValueTask EncolarAsync(WhatsAppWebhookPayload payload, CancellationToken cancellationToken);
}
