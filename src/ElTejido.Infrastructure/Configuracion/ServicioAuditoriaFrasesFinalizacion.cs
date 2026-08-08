using ElTejido.Application.Conversacion;
using ElTejido.Application.Seguridad;
using ElTejido.Domain.Seguridad;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ElTejido.Infrastructure.Configuracion;

/// <summary>
/// Deja una traza append-only de la versión efectiva de DT-P27-01 al iniciar. El origen de
/// configuración conserva los valores y sus revisiones; esta auditoría permite identificar qué
/// versión quedó vigente o cuál fue descartada sin copiar aliases a logs ni a Cosmos.
/// </summary>
internal sealed class ServicioAuditoriaFrasesFinalizacion : IHostedService
{
    private readonly OpcionesConversacion _opciones;
    private readonly IRepositorioLogSeguridad _logs;
    private readonly TimeProvider _tiempo;
    private readonly ILogger<ServicioAuditoriaFrasesFinalizacion> _logger;

    public ServicioAuditoriaFrasesFinalizacion(
        OpcionesConversacion opciones,
        IRepositorioLogSeguridad logs,
        TimeProvider tiempo,
        ILogger<ServicioAuditoriaFrasesFinalizacion> logger)
    {
        _opciones = opciones;
        _logs = logs;
        _tiempo = tiempo;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var resolucion = ResolutorFrasesFinalizacion.Resolver(_opciones);
        foreach (var lista in resolucion.Listas)
        {
            var resultado = lista.FueDescartada
                ? "descartada"
                : lista.Origen == OrigenFrasesFinalizacion.Configuracion ? "aplicada" : "default";
            var detalle = lista.FueDescartada
                ? $"lista={lista.Nombre};motivo={lista.MotivoDescarte}"
                : $"lista={lista.Nombre};version={lista.Version}";

            try
            {
                await _logs.RegistrarAsync(
                    LogSeguridad.Crear(
                        "cfg-frases-finalizacion-" + Guid.NewGuid().ToString("N"),
                        TipoEventoSeguridad.ConfiguracionFrasesFinalizacion,
                        usuarioId: null,
                        numero: null,
                        resultado,
                        detalle,
                        correlationId: null,
                        _tiempo.GetUtcNow()),
                    cancellationToken);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                // La auditoría no debe impedir que la aplicación arranque con el fallback seguro.
                _logger.LogWarning(ex, "No fue posible auditar la configuración de {Lista}.", lista.Nombre);
            }

            if (lista.FueDescartada)
            {
                _logger.LogWarning(
                    "Se descartó la configuración de {Lista}; motivo seguro: {Motivo}. Se usa el default compilado.",
                    lista.Nombre,
                    lista.MotivoDescarte);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
