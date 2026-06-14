using ElTejido.Application.WhatsApp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ElTejido.Infrastructure.WhatsApp;

/// <summary>
/// Consumidor in-process de la cola del webhook (02 §5, 05 §2.4): por cada payload abre un scope de
/// DI, resuelve el <see cref="ProcesadorWebhookEntrante"/> y lo ejecuta. Aisla los fallos por item
/// para no detener la cola. Si la persistencia/resolucion no esta configurada (sin Cosmos), registra
/// y descarta el item.
/// </summary>
public sealed class TrabajadorWebhook : BackgroundService
{
    private readonly ColaWebhookCanal _cola;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TrabajadorWebhook> _logger;

    public TrabajadorWebhook(
        ColaWebhookCanal cola,
        IServiceScopeFactory scopeFactory,
        ILogger<TrabajadorWebhook> logger)
    {
        _cola = cola;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var payload in _cola.Lector.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var procesador = scope.ServiceProvider.GetService<ProcesadorWebhookEntrante>();
                if (procesador is null)
                {
                    _logger.LogWarning("Webhook entrante descartado: procesamiento no configurado (sin persistencia).");
                    continue;
                }

                var resultado = await procesador.ProcesarAsync(payload, stoppingToken);
                _logger.LogInformation("Webhook entrante procesado con resultado {Resultado}.", resultado);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Un fallo por item no debe detener la cola (02 §5).
                _logger.LogError(ex, "Error procesando un webhook entrante.");
            }
        }
    }
}
