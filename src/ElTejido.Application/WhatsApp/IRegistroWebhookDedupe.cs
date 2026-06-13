namespace ElTejido.Application.WhatsApp;

/// <summary>
/// Puerto de idempotencia para mensajes entrantes de WhatsApp.
/// Cubre ARQ 4.2 y el contrato WebhookDedupe de 03 secciones 3.16 y 4.
/// </summary>
public interface IRegistroWebhookDedupe
{
    /// <returns>
    /// true cuando el mensaje se registra por primera vez; false cuando ya existia y debe descartarse.
    /// </returns>
    Task<bool> IntentarRegistrarMensajeAsync(
        string whatsappMessageId,
        DateTimeOffset procesadoEn,
        CancellationToken cancellationToken);
}
