using System.Threading.Channels;
using ElTejido.Application.WhatsApp;

namespace ElTejido.Infrastructure.WhatsApp;

/// <summary>
/// Cola in-process del webhook sobre <see cref="Channel{T}"/> (02 §5). Productor: el endpoint tras
/// el ack 200. Consumidor: <see cref="TrabajadorWebhook"/>. Si el proceso se reinicia, los items en
/// memoria se pierden (aceptable: Meta reintenta el webhook, 02 §5).
/// </summary>
public sealed class ColaWebhookCanal : IColaWebhook
{
    private readonly Channel<WhatsAppWebhookPayload> _canal =
        Channel.CreateUnbounded<WhatsAppWebhookPayload>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });

    public ChannelReader<WhatsAppWebhookPayload> Lector => _canal.Reader;

    public ValueTask EncolarAsync(WhatsAppWebhookPayload payload, CancellationToken cancellationToken)
        => _canal.Writer.WriteAsync(payload, cancellationToken);
}
