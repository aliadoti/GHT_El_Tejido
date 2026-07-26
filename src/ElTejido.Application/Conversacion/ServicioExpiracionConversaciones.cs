using ElTejido.Application.Campanas;
using ElTejido.Application.Common;
using ElTejido.Application.Seguridad;
using ElTejido.Domain.Campanas;
using ElTejido.Domain.Conversaciones;
using ElTejido.Domain.Seguridad;

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
    private readonly PoliticaColaCoachingIdeas _colaCoaching = new();
    private readonly IRepositorioLogSeguridad _logSeguridad;
    private readonly IProveedorCorrelacion _correlacion;
    private readonly IOrquestadorConversacion _orquestador;

    public ServicioExpiracionConversaciones(
        IRepositorioConversaciones conversaciones,
        IRepositorioCampanias campanias,
        IRepositorioLogSeguridad logSeguridad,
        IProveedorCorrelacion correlacion,
        IOrquestadorConversacion orquestador,
        OpcionesConversacion opciones,
        TimeProvider tiempo)
    {
        _conversaciones = conversaciones;
        _campanias = campanias;
        _logSeguridad = logSeguridad;
        _correlacion = correlacion;
        _orquestador = orquestador;
        _opciones = opciones;
        _tiempo = tiempo;
    }

    /// <summary>
    /// ¿Esta habilitada la expiracion por configuracion global? (minutos u horas &gt; 0). Es el
    /// interruptor maestro: con ambos en 0 no corre el barrido y los overrides por campaña quedan inactivos.
    /// </summary>
    public bool Habilitada =>
        _opciones.CoachingSecuencialIdeas
        || _opciones.MinutosInactividadSesion > 0
        || _opciones.HorasExpiracionSinRespuesta > 0;

    /// <summary>
    /// Aplica los timeouts de coaching y cierra los hilos inactivos; devuelve cuantas transiciones realizo.
    /// </summary>
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
            var minutosCoaching = MinutosCoachingEfectivos(campania);
            if (_opciones.CoachingSecuencialIdeas
                && _opciones.SegmentacionIdeas
                && campania.ConfigConversacional.CoachingSecuencialIdeas
                && campania.ConfigConversacional.SegmentacionIdeas
                && minutosCoaching > 0)
            {
                var conversaciones = await _conversaciones.ListarConversacionesAsync(campania.Id, cancellationToken);
                foreach (var conversacion in conversaciones.Where(conversacion =>
                             conversacion.Estado == EstadoConversacion.Abierta
                             && conversacion.CoachingIdeas?.IdeaActiva?.IniciadaEn <= ahora.AddMinutes(-minutosCoaching)))
                {
                    var cola = _colaCoaching.FinalizarActiva(
                        conversacion.CoachingIdeas!,
                        MotivoFinalizacionIdea.Tiempo,
                        ahora);
                    var actualizada = conversacion.ConCoachingIdeas(cola);
                    if (cola.Estado == EstadoCoachingIdeas.Finalizado)
                    {
                        actualizada = actualizada.Cerrar(ahora);
                    }

                    await _conversaciones.GuardarConversacionAsync(actualizada, cancellationToken);
                    if (actualizada.Estado == EstadoConversacion.Abierta && actualizada.VentanaAbierta(ahora))
                    {
                        await _orquestador.EnviarTurnoCoachingPendienteAsync(
                            actualizada,
                            campania,
                            cancellationToken);
                    }

                    await _logSeguridad.RegistrarAsync(
                        LogSeguridad.Crear(
                            "log_" + Guid.NewGuid().ToString("N"),
                            TipoEventoSeguridad.CoachingSecuencialIdeas,
                            conversacion.UsuarioId,
                            numero: null,
                            "timeout",
                            FormattableString.Invariant(
                                $"accion:timeout;ideaIndice:{conversacion.CoachingIdeas!.IdeaActivaIndice};ideasTotal:{conversacion.CoachingIdeas.Ideas.Count};revision:{conversacion.CoachingIdeas.IdeaActiva!.RepreguntasUsadas};motivo:tiempo"),
                            _correlacion.CorrelationIdActual,
                            ahora),
                        cancellationToken);
                    cerradas++;
                }
            }

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

    private int MinutosCoachingEfectivos(Campania campania)
    {
        var overridePorCampania = campania.ConfigConversacional.MinutosCoachingPorIdea;
        if (overridePorCampania.HasValue)
        {
            return Math.Max(0, overridePorCampania.Value);
        }

        return Math.Max(0, _opciones.MinutosCoachingPorIdea);
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
