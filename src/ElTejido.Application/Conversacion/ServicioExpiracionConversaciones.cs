using ElTejido.Domain.Conversaciones;

namespace ElTejido.Application.Conversacion;

/// <summary>
/// Cierra los hilos conversacionales abandonados (sin respuesta del participante) pasado el plazo
/// configurado (<see cref="OpcionesConversacion.HorasExpiracionSinRespuesta"/>). El cierre es
/// <b>silencioso</b> (no envia mensaje): pasado tiempo la ventana de servicio de 24h puede estar
/// cerrada y un texto libre no se entregaria. La ultima evaluacion registrada queda como definitiva.
/// </summary>
public sealed class ServicioExpiracionConversaciones
{
    private readonly IRepositorioConversaciones _conversaciones;
    private readonly OpcionesConversacion _opciones;
    private readonly TimeProvider _tiempo;

    public ServicioExpiracionConversaciones(
        IRepositorioConversaciones conversaciones,
        OpcionesConversacion opciones,
        TimeProvider tiempo)
    {
        _conversaciones = conversaciones;
        _opciones = opciones;
        _tiempo = tiempo;
    }

    /// <summary>¿Esta habilitada la expiracion por configuracion?</summary>
    public bool Habilitada => _opciones.HorasExpiracionSinRespuesta > 0;

    /// <summary>Cierra los hilos abiertos sin actividad pasado el plazo; devuelve cuantos cerro.</summary>
    public async Task<int> CerrarExpiradasAsync(CancellationToken cancellationToken)
    {
        if (!Habilitada)
        {
            return 0;
        }

        var ahora = _tiempo.GetUtcNow();
        var limite = ahora.AddHours(-_opciones.HorasExpiracionSinRespuesta);
        var expiradas = await _conversaciones.ListarAbiertasInactivasAsync(limite, cancellationToken);

        var cerradas = 0;
        foreach (var conversacion in expiradas)
        {
            if (conversacion.Estado == EstadoConversacion.Cerrada)
            {
                continue;
            }

            await _conversaciones.GuardarConversacionAsync(conversacion.Cerrar(ahora), cancellationToken);
            cerradas++;
        }

        return cerradas;
    }
}
