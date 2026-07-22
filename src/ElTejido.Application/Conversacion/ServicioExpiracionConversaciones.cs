using ElTejido.Application.Campanas;
using ElTejido.Domain.Campanas;
using ElTejido.Domain.Conversaciones;

namespace ElTejido.Application.Conversacion;

/// <summary>
/// Cierra los hilos conversacionales abandonados (sin respuesta del participante) pasada la ventana de
/// inactividad configurada. El cierre es <b>silencioso</b> (no envia mensaje): pasado tiempo la ventana
/// de servicio de 24h puede estar cerrada y un texto libre no se entregaria. La ultima evaluacion
/// registrada queda como definitiva.
/// <para>
/// I-17 §7 anade <b>granularidad sub-hora y parametrizacion por campaña</b>: la ventana efectiva de cada
/// campaña se resuelve como <c>ConfigConversacional.MinutosInactividadSesion</c> (override; <c>&lt;= 0</c>
/// apaga esa campaña) → default global <c>Conversacion:MinutosInactividadSesion</c> → horas legacy
/// <c>Conversacion:HorasExpiracionSinRespuesta</c>. El barrido consulta y cierra por campaña con su propia
/// ventana. El interruptor operativo maestro sigue siendo global (minutos u horas &gt; 0); con ambos en 0
/// el barrido no corre y los overrides por campaña quedan inactivos (coherente con el modelo de
/// kill-switch global de operacion).
/// </para>
/// </summary>
public sealed class ServicioExpiracionConversaciones
{
    private readonly IRepositorioConversaciones _conversaciones;
    private readonly IRepositorioCampanias _campanias;
    private readonly OpcionesConversacion _opciones;
    private readonly TimeProvider _tiempo;

    public ServicioExpiracionConversaciones(
        IRepositorioConversaciones conversaciones,
        IRepositorioCampanias campanias,
        OpcionesConversacion opciones,
        TimeProvider tiempo)
    {
        _conversaciones = conversaciones;
        _campanias = campanias;
        _opciones = opciones;
        _tiempo = tiempo;
    }

    /// <summary>
    /// ¿Esta habilitada la expiracion por configuracion global? (minutos u horas &gt; 0). Es el
    /// interruptor maestro: con ambos en 0 no corre el barrido y los overrides por campaña quedan inactivos.
    /// </summary>
    public bool Habilitada => _opciones.MinutosInactividadSesion > 0 || _opciones.HorasExpiracionSinRespuesta > 0;

    /// <summary>Cierra los hilos abiertos sin actividad pasada su ventana efectiva; devuelve cuantos cerro.</summary>
    public async Task<int> CerrarExpiradasAsync(CancellationToken cancellationToken)
    {
        if (!Habilitada)
        {
            return 0;
        }

        var ahora = _tiempo.GetUtcNow();
        var campanias = await _campanias.BuscarCampaniasAsync(new FiltroCampanias(), cancellationToken);

        var cerradas = 0;
        foreach (var campania in campanias)
        {
            var minutos = MinutosInactividadEfectivos(campania);
            if (minutos <= 0)
            {
                continue;
            }

            var limite = ahora.AddMinutes(-minutos);
            var expiradas = await _conversaciones.ListarAbiertasInactivasAsync(campania.Id, limite, cancellationToken);
            foreach (var conversacion in expiradas)
            {
                if (conversacion.Estado == EstadoConversacion.Cerrada)
                {
                    continue;
                }

                await _conversaciones.GuardarConversacionAsync(conversacion.Cerrar(ahora), cancellationToken);
                cerradas++;
            }
        }

        return cerradas;
    }

    /// <summary>
    /// Ventana de inactividad efectiva de la campaña, en minutos (0 = no expira). Precedencia I-17 §7:
    /// override por campaña (<c>&lt;= 0</c> = off explicito) → default global en minutos → horas legacy.
    /// </summary>
    private int MinutosInactividadEfectivos(Campania campania)
    {
        var overridePorCampania = campania.ConfigConversacional.MinutosInactividadSesion;
        if (overridePorCampania.HasValue)
        {
            return overridePorCampania.Value > 0 ? overridePorCampania.Value : 0;
        }

        if (_opciones.MinutosInactividadSesion > 0)
        {
            return _opciones.MinutosInactividadSesion;
        }

        return _opciones.HorasExpiracionSinRespuesta > 0 ? _opciones.HorasExpiracionSinRespuesta * 60 : 0;
    }
}
