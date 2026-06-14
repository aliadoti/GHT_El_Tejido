using System.Threading.Channels;
using ElTejido.Application.WhatsApp;

namespace ElTejido.Infrastructure.WhatsApp;

/// <summary>
/// Cola in-process de envios salientes sobre <see cref="Channel{T}"/> (02 §5, 05 §2.5). Productor:
/// el servicio de envios. Consumidor: <see cref="TrabajadorEnvios"/>, que aplica throttling.
/// </summary>
public sealed class ColaEnviosCanal : IColaEnvios
{
    private readonly Channel<TrabajoEnvio> _canal =
        Channel.CreateUnbounded<TrabajoEnvio>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });

    public ChannelReader<TrabajoEnvio> Lector => _canal.Reader;

    public ValueTask EncolarAsync(TrabajoEnvio trabajo, CancellationToken cancellationToken)
        => _canal.Writer.WriteAsync(trabajo, cancellationToken);
}
