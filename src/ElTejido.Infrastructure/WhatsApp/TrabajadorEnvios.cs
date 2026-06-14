using ElTejido.Application.WhatsApp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ElTejido.Infrastructure.WhatsApp;

/// <summary>
/// Consumidor in-process de la cola de envios (02 §5, 05 §2.5): procesa cada <see cref="TrabajoEnvio"/>
/// con <see cref="ProcesadorEnvio"/> aplicando throttling configurable entre envios (ARQ §4.4).
/// Aisla los fallos por item para no detener la cola.
/// </summary>
public sealed class TrabajadorEnvios : BackgroundService
{
    private readonly ColaEnviosCanal _cola;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OpcionesWhatsApp _opciones;
    private readonly TimeProvider _tiempo;
    private readonly ILogger<TrabajadorEnvios> _logger;

    public TrabajadorEnvios(
        ColaEnviosCanal cola,
        IServiceScopeFactory scopeFactory,
        IOptions<OpcionesWhatsApp> opciones,
        TimeProvider tiempo,
        ILogger<TrabajadorEnvios> logger)
    {
        _cola = cola;
        _scopeFactory = scopeFactory;
        _opciones = opciones.Value;
        _tiempo = tiempo;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var trabajo in _cola.Lector.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var procesador = scope.ServiceProvider.GetService<ProcesadorEnvio>();
                if (procesador is null)
                {
                    _logger.LogWarning("Envio descartado: procesamiento no configurado (sin persistencia).");
                    continue;
                }

                await procesador.ProcesarAsync(trabajo, stoppingToken);

                if (_opciones.ThrottleEnvioMs > 0)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(_opciones.ThrottleEnvioMs), _tiempo, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error procesando un envio saliente.");
            }
        }
    }
}
