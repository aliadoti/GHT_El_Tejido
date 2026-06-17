using ElTejido.Application.Conversacion;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ElTejido.Infrastructure.Conversaciones;

/// <summary>
/// Barrido periodico que cierra los hilos conversacionales abandonados (sin respuesta del
/// participante) pasado el plazo configurado (<see cref="OpcionesConversacion"/>). Si la expiracion
/// esta desactivada (0 horas) el trabajador no hace nada. Aisla fallos para no tumbar el host.
/// </summary>
public sealed class TrabajadorExpiracionConversaciones : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OpcionesConversacion _opciones;
    private readonly ILogger<TrabajadorExpiracionConversaciones> _logger;

    public TrabajadorExpiracionConversaciones(
        IServiceScopeFactory scopeFactory,
        OpcionesConversacion opciones,
        ILogger<TrabajadorExpiracionConversaciones> logger)
    {
        _scopeFactory = scopeFactory;
        _opciones = opciones;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_opciones.HorasExpiracionSinRespuesta <= 0)
        {
            return; // expiracion desactivada por configuracion
        }

        var intervalo = TimeSpan.FromMinutes(Math.Max(1, _opciones.IntervaloRevisionMinutos));
        using var timer = new PeriodicTimer(intervalo);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var servicio = scope.ServiceProvider.GetService<ServicioExpiracionConversaciones>();
                    if (servicio is null)
                    {
                        continue;
                    }

                    var cerradas = await servicio.CerrarExpiradasAsync(stoppingToken);
                    if (cerradas > 0)
                    {
                        _logger.LogInformation(
                            "Expiracion: {Cerradas} conversacion(es) cerradas por inactividad ({Horas}h).",
                            cerradas,
                            _opciones.HorasExpiracionSinRespuesta);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Error en el barrido de expiracion de conversaciones.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Apagado normal.
        }
    }
}
