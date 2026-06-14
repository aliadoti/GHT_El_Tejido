namespace ElTejido.Application.WhatsApp;

/// <summary>
/// Cola in-process de envios salientes (02 §5, 05 §2.5): el servicio de envios encola un
/// <see cref="TrabajoEnvio"/> por participante y un <c>IHostedService</c> los procesa con throttling.
/// </summary>
public interface IColaEnvios
{
    ValueTask EncolarAsync(TrabajoEnvio trabajo, CancellationToken cancellationToken);
}
