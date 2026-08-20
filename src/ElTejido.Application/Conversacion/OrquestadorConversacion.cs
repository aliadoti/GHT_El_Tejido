using ElTejido.Application.Campanas;
using ElTejido.Application.Common;
using ElTejido.Application.Configuracion;
using ElTejido.Application.Evaluacion;
using ElTejido.Application.Identidad;
using ElTejido.Application.Markdown;
using ElTejido.Application.Participantes;
using ElTejido.Application.Respuestas;
using ElTejido.Application.Seguridad;
using ElTejido.Application.WhatsApp;
using ElTejido.Domain.Campanas;
using ElTejido.Domain.Common;
using ElTejido.Domain.Configuracion;
using ElTejido.Domain.Conversaciones;
using ElTejido.Domain.Evaluacion;
using ElTejido.Domain.Identidad;
using ElTejido.Domain.Localizacion;
using ElTejido.Domain.Participantes;
using ElTejido.Domain.Respuestas;
using ElTejido.Domain.Seguridad;
using ElTejido.Domain.Usuarios;
using DominioConversacion = ElTejido.Domain.Conversaciones.Conversacion;
using RespuestaUsuario = ElTejido.Domain.Respuestas.Respuesta;

namespace ElTejido.Application.Conversacion;

/// <summary>
/// Orquestador conversacional (05 §4): gobierna la maquina de estados de un hilo a partir de un
/// mensaje entrante. Persiste Mensaje y Respuesta, evalua con el LLM (08), aplica el tope de
/// revisiones del MVP (05 §4.4) y, al cerrar, envia el cierre que corresponda y compila el Markdown (09).
/// Ante fallback del evaluador (08 §6) envia retro neutra y cierra dejando la respuesta como
/// <c>evaluacionPendiente</c> (sin romper el hilo).
/// </summary>
public sealed class OrquestadorConversacion : IOrquestadorConversacion
{
    private const string Canal = "whatsapp";

    /// <summary>Largo máximo de cada paráfrasis en la lista de selección de reapertura (I-19 §4.7).</summary>
    private const int MaxCaracteresParafrasisSeleccion = 160;

    /// <summary>P-26 §9: ventana móvil de los cupos por participante en campañas continuas.</summary>
    private const int HorasVentanaCuposContinua = 24;

    private const string MenuAclaracionSalida =
        "¿Qué prefieres? Responde 1 para seguir con esta idea, 2 para dejar esta idea y pasar a la siguiente, o 3 para terminar por ahora.";
    private const string RespaldoAclaracionSalida =
        "Puedes continuar con tu idea o indicar una salida cuando lo necesites.";

    private readonly IRepositorioConversaciones _conversaciones;
    private readonly IRepositorioRespuestas _respuestas;
    private readonly IRepositorioParticipantes _participantes;
    private readonly IRepositorioConfiguracion _configuracion;
    private readonly IEvaluadorLlm _evaluador;
    private readonly IConsolidadorIdeas? _consolidadorIdeas;
    private readonly IRedactorTurnoConversacional? _redactorTurno;
    private readonly ISegmentadorIdeas _segmentadorIdeas;
    private readonly IBaseConocimientoCampania _baseConocimiento;
    private readonly IWhatsAppGateway _gateway;
    private readonly IRepositorioLogSeguridad _logSeguridad;
    private readonly IProveedorCorrelacion _correlacion;
    private readonly IResolutorTextosConversacion? _resolutorTextos;
    private readonly OpcionesMensajesConversacion _mensajes;
    private readonly ResolvedorTransicionConversacion _transicion;
    private readonly PoliticaLimitesConversacion _limites;
    private readonly ProcesadorResultadoEvaluacion _procesador;
    private readonly bool _cuposHabilitados;
    private readonly int _maxTurnosPorHilo;
    private readonly bool _segmentacionIdeasHabilitada;
    private readonly bool _coachingSecuencialIdeasHabilitado;
    private readonly int _maxIdeasPorMensaje;
    private readonly int _longitudMinimaIdea;
    private readonly bool _tejidoColectivoHabilitado;
    private readonly int _topKAportes;
    private readonly int _presupuestoTokensTejido;
    private readonly bool _parafraseoHabilitado;
    private readonly bool _consolidacionProgresivaHabilitada;
    private readonly bool _visibilidadIdeaParticipanteHabilitada;
    private readonly bool _confirmacionExplicitaIdeasHabilitada;
    private readonly int _maxCaracteresIdeaConsolidada;
    private readonly int _maxCaracteresParafraseo;
    private readonly TimeProvider _tiempo;
    private readonly PoliticaColaCoachingIdeas _colaCoaching = new();
    private readonly PoliticaRedaccionConversacional _redaccion;
    private readonly DetectorIntencionContinuar _intencionConfirmacion;
    private readonly DetectorIntencionContinuar _intencionSolicitarMejora;
    private readonly DetectorIntencionContinuar _intencionRechazoIdea;
    private readonly DetectorIntencionContinuar _intencionRevisitarAnterior;
    private readonly DetectorIntencionContinuar _intencionRevisitarIdea;
    private readonly int _maxCaracteresIntencion;
    private readonly int _maxCaracteresIntencionControl;
    private readonly IClasificadorIntencionControl? _clasificadorIntencionControl;
    private readonly PoliticaIntencionControl _politicaIntencionControl;
    private readonly bool _clasificacionIntencionControlHabilitada;
    private readonly OpcionesCatalogoTextos _opcionesCatalogoTextos;
    private readonly IResolutorContenidoCampania _resolutorContenidoCampania;
    private readonly GuardaCuposLlm _guardaCuposLlm;

    public OrquestadorConversacion(
        IRepositorioConversaciones conversaciones,
        IRepositorioRespuestas respuestas,
        IRepositorioParticipantes participantes,
        IRepositorioConfiguracion configuracion,
        IEvaluadorLlm evaluador,
        ISegmentadorIdeas segmentadorIdeas,
        IBaseConocimientoCampania baseConocimiento,
        ICompiladorMarkdown compilador,
        IWhatsAppGateway gateway,
        IRepositorioLogSeguridad logSeguridad,
        IProveedorCorrelacion correlacion,
        OpcionesConversacion opciones,
        TimeProvider tiempo,
        IConsolidadorIdeas? consolidadorIdeas = null,
        IRedactorTurnoConversacional? redactorTurno = null,
        IClasificadorIntencionControl? clasificadorIntencionControl = null,
        IResolutorTextosConversacion? resolutorTextos = null,
        OpcionesCatalogoTextos? opcionesCatalogoTextos = null,
        IResolutorContenidoCampania? resolutorContenidoCampania = null)
    {
        _conversaciones = conversaciones;
        _respuestas = respuestas;
        _participantes = participantes;
        _configuracion = configuracion;
        _evaluador = evaluador;
        _consolidadorIdeas = consolidadorIdeas;
        _redactorTurno = redactorTurno;
        _clasificadorIntencionControl = clasificadorIntencionControl;
        _resolutorTextos = resolutorTextos;
        _opcionesCatalogoTextos = opcionesCatalogoTextos ?? new OpcionesCatalogoTextos();
        _resolutorContenidoCampania = resolutorContenidoCampania ?? new ResolutorContenidoCampania();
        _segmentadorIdeas = segmentadorIdeas;
        _baseConocimiento = baseConocimiento;
        _gateway = gateway;
        _logSeguridad = logSeguridad;
        _guardaCuposLlm = new GuardaCuposLlm(respuestas, logSeguridad);
        _correlacion = correlacion;
        _mensajes = opciones.Mensajes;
        _limites = new PoliticaLimitesConversacion(
            opciones.UmbralCierreAnticipado,
            opciones.CierreAnticipadoHabilitado,
            opciones.UmbralResumenConsolidacion,
            opciones.ResumenConsolidacionHabilitado);
        _procesador = new ProcesadorResultadoEvaluacion(respuestas, compilador, logSeguridad, correlacion, _limites);
        _cuposHabilitados = opciones.CuposHabilitados;
        _maxTurnosPorHilo = opciones.MaxTurnosPorHilo;
        _segmentacionIdeasHabilitada = opciones.SegmentacionIdeas;
        _coachingSecuencialIdeasHabilitado = opciones.CoachingSecuencialIdeas;
        _maxIdeasPorMensaje = Math.Max(1, opciones.MaxIdeasPorMensaje);
        _longitudMinimaIdea = Math.Max(1, opciones.LongitudMinimaIdea);
        _tejidoColectivoHabilitado = opciones.TejidoColectivo;
        _topKAportes = Math.Max(1, opciones.TopKAportes);
        _presupuestoTokensTejido = opciones.PresupuestoTokensTejido;
        _parafraseoHabilitado = opciones.Parafraseo;
        _consolidacionProgresivaHabilitada = opciones.ConsolidacionProgresivaHabilitada;
        _visibilidadIdeaParticipanteHabilitada = opciones.VisibilidadIdeaParticipanteHabilitada;
        _confirmacionExplicitaIdeasHabilitada = opciones.ConfirmacionExplicitaIdeasHabilitada;
        _redaccion = new PoliticaRedaccionConversacional(
            opciones.RedaccionConversacionalFluidaHabilitada, opciones.MaxCaracteresRedaccionTurno);
        _maxCaracteresIdeaConsolidada = Math.Max(1, opciones.MaxCaracteresIdeaConsolidada);
        _maxCaracteresParafraseo = Math.Max(0, opciones.MaxCaracteresParafraseo);
        IEnumerable<string> frases = opciones.FrasesContinuar is { Count: > 0 }
            ? opciones.FrasesContinuar
            : DetectorIntencionContinuar.FrasesPorDefecto;
        var intencionContinuar = new DetectorIntencionContinuar(frases, opciones.MaxCaracteresIntencionContinuar);
        IEnumerable<string> frasesRechazo = opciones.FrasesRechazoGuardado is { Count: > 0 }
            ? opciones.FrasesRechazoGuardado
            : DetectorIntencionContinuar.FrasesRechazoGuardadoPorDefecto;
        var intencionRechazoGuardado = new DetectorIntencionContinuar(frasesRechazo, opciones.MaxCaracteresIntencionContinuar);
        _transicion = new ResolvedorTransicionConversacion(intencionContinuar, intencionRechazoGuardado);
        _intencionConfirmacion = new DetectorIntencionContinuar(
            frases.Concat(new[] { "si", "sí", "correcto", "eso es", "exacto", "confirmo" }),
            opciones.MaxCaracteresIntencionContinuar);
        _intencionSolicitarMejora = new DetectorIntencionContinuar(
            opciones.FrasesSolicitarMejora is { Count: > 0 }
                ? opciones.FrasesSolicitarMejora
                : DetectorIntencionContinuar.FrasesSolicitarMejoraPorDefecto,
            opciones.MaxCaracteresIntencionContinuar);
        _intencionRechazoIdea = intencionRechazoGuardado;
        _maxCaracteresIntencion = opciones.MaxCaracteresIntencionContinuar;
        _maxCaracteresIntencionControl = opciones.MaxCaracteresClasificacionIntencionControl;
        _politicaIntencionControl = new PoliticaIntencionControl(opciones);
        _clasificacionIntencionControlHabilitada = opciones.ClasificacionIntencionControl;
        _intencionRevisitarAnterior = new DetectorIntencionContinuar(
            opciones.FrasesRevisitarAnterior is { Count: > 0 }
                ? opciones.FrasesRevisitarAnterior
                : DetectorIntencionContinuar.FrasesRevisitarAnteriorPorDefecto,
            opciones.MaxCaracteresIntencionContinuar);
        _intencionRevisitarIdea = new DetectorIntencionContinuar(
            opciones.FrasesRevisitarIdea is { Count: > 0 }
                ? opciones.FrasesRevisitarIdea
                : DetectorIntencionContinuar.FrasesRevisitarIdeaPorDefecto,
            opciones.MaxCaracteresIntencionContinuar);
        _tiempo = tiempo;
    }

    public async Task EnviarTurnoCoachingPendienteAsync(
        DominioConversacion conversacion,
        Campania campania,
        CancellationToken cancellationToken)
    {
        var ahora = _tiempo.GetUtcNow();
        var activa = conversacion.CoachingIdeas?.IdeaActiva;
        if (conversacion.Estado != EstadoConversacion.Abierta
            || !conversacion.VentanaAbierta(ahora)
            || activa is null
            || activa.RepreguntasUsadas > 0)
        {
            return;
        }

        var participante = await _participantes.ObtenerParticipantePorUsuarioAsync(
            campania.Id,
            conversacion.UsuarioId,
            cancellationToken);
        if (participante is null)
        {
            return;
        }

        // I-19: una idea recién activada por timeout todavía no tiene evaluación; su turno pendiente es
        // la confirmación de la versión propuesta, no una pregunta socrática.
        if (ConsolidacionIdeasActiva && !string.IsNullOrWhiteSpace(activa.VersionIdeaVigenteId))
        {
            var propuesta = await ObtenerVersionAsync(campania.Id, activa.VersionIdeaVigenteId, cancellationToken);
            if (propuesta is { EstadoConfirmacion: EstadoConfirmacionVersionIdea.Propuesta })
            {
                await EnviarAsync(
                    conversacion,
                    participante.WhatsappNormalizado,
                    TextoConfirmacion(propuesta.Texto),
                    TipoEnvioMensaje.Repregunta,
                    campania.ConfigConversacional.NumeroWhatsAppSaliente,
                    ahora,
                    cancellationToken);
                await _conversaciones.GuardarConversacionAsync(
                    conversacion.AvanzarA(EstadoMaquinaConversacion.EsperandoRepregunta), cancellationToken);
                return;
            }
        }

        var evaluacion = await _respuestas.ObtenerEvaluacionPorRespuestaAsync(
            campania.Id,
            activa.RespuestaVigenteId,
            cancellationToken);
        if (evaluacion is null)
        {
            return;
        }

        await EnviarPreguntaCoachingAsync(
            conversacion,
            campania,
            participante.UsuarioId,
            participante.WhatsappNormalizado,
            campania.ConfigConversacional.NumeroWhatsAppSaliente,
            evaluacion,
            ahora,
            cancellationToken);
    }

    /// <summary>
    /// P-29 §5.2: el hilo ya fue cerrado por el barrido de inactividad (I-17 §7) y la idea abierta ya
    /// quedo `pendiente` con motivo `inactividad`; aqui solo se humaniza ese cierre con un unico aviso.
    /// No se evalua nada (I-19), no se reabre el hilo y no se toca `motivoCierre`.
    /// </summary>
    public async Task EnviarPausaPorInactividadAsync(
        DominioConversacion conversacion,
        Campania campania,
        CancellationToken cancellationToken)
    {
        var ahora = _tiempo.GetUtcNow();

        // P-29 §9: el cierre administrativo de la campaña prevalece y no se le agrega aviso.
        if (campania.Estado != EstadoCampania.Activa)
        {
            return;
        }

        var participante = await _participantes.ObtenerParticipantePorUsuarioAsync(
            campania.Id,
            conversacion.UsuarioId,
            cancellationToken);
        if (participante is null)
        {
            return;
        }

        // P-29 §9: fuera de la ventana de servicio de 24 h se omite el texto libre y nunca se fuerza
        // una plantilla HSM (eso es P-08). El cierre de I-17 ya quedó registrado igual.
        if (!conversacion.VentanaAbierta(ahora))
        {
            await RegistrarCierrePorInactividadAsync(
                conversacion, participante.WhatsappNormalizado, "avisoOmitidoSinVentana", envio: null, ahora, cancellationToken);
            return;
        }

        // §5.2: el texto lo redacta el LLM (I-20) y el servidor conserva el respaldo determinista para
        // el modelo apagado, la salida inválida, el cupo agotado o el fallo de la llamada.
        var respaldo = await TextoGlobalAsync(
            conversacion,
            "pausaPorInactividad",
            TextoConfigurado(_mensajes.PausaPorInactividad, OpcionesMensajesConversacion.PausaPorInactividadDefault),
            cancellationToken);
        var texto = await ComponerTurnoAsync(
            campania,
            campania.Preguntas.FirstOrDefault(pregunta => pregunta.Id == conversacion.PreguntaId),
            conversacion.UsuarioId,
            participante.WhatsappNormalizado,
            ActoConversacional.Pausar,
            respaldo,
            ahora,
            cancellationToken,
            idioma: conversacion.Idioma);

        var resultado = await EnviarAsync(
            conversacion,
            participante.WhatsappNormalizado,
            texto,
            TipoEnvioMensaje.Cierre,
            campania.ConfigConversacional.NumeroWhatsAppSaliente,
            ahora,
            cancellationToken);

        await RegistrarCierrePorInactividadAsync(
            conversacion,
            participante.WhatsappNormalizado,
            string.Equals(texto, respaldo, StringComparison.Ordinal) ? "fallbackUsado" : "avisoEnviado",
            resultado.Exito,
            ahora,
            cancellationToken);
    }

    /// <summary>
    /// P-29 §7 (10 §6.2): una entrada por cierre por inactividad con aviso. Solo accion, ids internos y
    /// resultado del envio; nunca el texto del aviso ni el del participante.
    /// </summary>
    private Task RegistrarCierrePorInactividadAsync(
        DominioConversacion conversacion,
        NumeroWhatsApp numero,
        string accion,
        bool? envio,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
        => _logSeguridad.RegistrarAsync(
            LogSeguridad.Crear(
                "log_" + Guid.NewGuid().ToString("N"),
                TipoEventoSeguridad.CierrePorInactividad,
                conversacion.UsuarioId,
                numero.Valor,
                accion,
                FormattableString.Invariant(
                    $"accion:{accion};conversacion:{conversacion.Id};pregunta:{conversacion.PreguntaId};ciclo:{conversacion.CicloParticipacion};envio:{(envio is null ? "omitido" : envio.Value ? "ok" : "error")}"),
                _correlacion.CorrelationIdActual,
                ahora,
                campaniaId: conversacion.CampaniaId),
            cancellationToken);

    public async Task ProcesarMensajeEntranteAsync(
        ParticipanteResuelto participante,
        MensajeEntrante mensaje,
        CancellationToken cancellationToken)
        => await ProcesarMensajeEntranteInternoAsync(participante, mensaje, null, cancellationToken);

    public async Task ProcesarMensajeEntranteClasificadoAsync(
        ParticipanteResuelto participante,
        MensajeEntrante mensaje,
        ClasificacionIntencionPrevia clasificacion,
        CancellationToken cancellationToken)
        => await ProcesarMensajeEntranteInternoAsync(participante, mensaje, clasificacion, cancellationToken);

    private async Task ProcesarMensajeEntranteInternoAsync(
        ParticipanteResuelto participante,
        MensajeEntrante mensaje,
        ClasificacionIntencionPrevia? clasificacionPrevia,
        CancellationToken cancellationToken)
    {
        var usuario = participante.Usuario;
        var campania = participante.Campania;
        var numero = usuario.WhatsappNormalizado;
        var emisor = mensaje.PhoneNumberIdDestino;
        var ahora = _tiempo.GetUtcNow();

        // Cupo de mensajes por usuario/campania (10 §2, Campania.ConfigSeguridad): al exceder, el
        // entrante se descarta con rechazo neutral silencioso (como una conversacion cerrada) y el
        // motivo queda solo en LogSeguridad. Gateado por Conversacion:CuposHabilitados (default off).
        if (_cuposHabilitados && await CupoMensajesExcedidoAsync(campania, usuario.Id, ahora, cancellationToken))
        {
            await RegistrarRateLimitAsync(usuario, "cupo_mensajes_usuario", ahora, cancellationToken);
            return;
        }

        var hilo = await ResolverHiloTrabajoAsync(campania, usuario.Id, participante.PreguntaVigente, cancellationToken);
        if (hilo is null)
        {
            // Todas las preguntas activas de la campania ya tienen su hilo cerrado.
            return;
        }

        await ProcesarEnHiloAsync(hilo, participante, mensaje, ahora, clasificacionPrevia, null, cancellationToken);
    }

    /// <summary>
    /// P-26 corte 3 (05 §4.4.3): entrega un aporte con campania y pregunta ya resueltas por el
    /// enrutamiento determinista. Conversacion reciente abierta = afinidad (se procesa alli); sin
    /// conversacion = primer contacto de siempre; cerrada = <b>ciclo nuevo</b> (§5.7) con id derivado
    /// del mensaje raiz para que un reintento no lo duplique, y el aporte se procesa como contenido
    /// sustantivo del ciclo (I-19/P-25), no como saludo.
    /// </summary>
    public async Task ProcesarAporteEnrutadoAsync(
        ParticipanteResuelto participante,
        MensajeEntrante mensaje,
        ContextoAporteEnrutado contexto,
        CancellationToken cancellationToken)
    {
        var usuario = participante.Usuario;
        var campania = participante.Campania;
        var ahora = _tiempo.GetUtcNow();

        // Mismo cupo de mensajes que la entrada normal (10 §2), gateado por Conversacion:CuposHabilitados.
        if (_cuposHabilitados && await CupoMensajesExcedidoAsync(campania, usuario.Id, ahora, cancellationToken))
        {
            await RegistrarRateLimitAsync(usuario, "cupo_mensajes_usuario", ahora, cancellationToken);
            return;
        }

        var pregunta = PreguntasActivasOrdenadas(campania)
            .FirstOrDefault(p => p.Id == contexto.PreguntaId);
        if (pregunta is null)
        {
            // La pregunta dejo de estar activa entre la seleccion y la entrega: degradacion limpia al
            // flujo normal (que resuelve la pregunta vigente como siempre).
            await ProcesarMensajeEntranteAsync(participante, mensaje, cancellationToken);
            return;
        }

        var conversaciones = await _conversaciones.ListarConversacionesAsync(campania.Id, cancellationToken);
        var afinidadExplicita = string.IsNullOrWhiteSpace(contexto.ConversacionIdAfinidad)
            ? null
            : conversaciones.FirstOrDefault(c => c.Id == contexto.ConversacionIdAfinidad
                && c.UsuarioId == usuario.Id
                && c.PreguntaId == pregunta.Id
                && (c.Estado != EstadoConversacion.Cerrada || contexto.IdeaIdReabrir is not null));
        var reciente = afinidadExplicita ?? conversaciones
            .Where(c => c.UsuarioId == usuario.Id && c.PreguntaId == pregunta.Id)
            .OrderByDescending(c => c.FechaInicio)
            .FirstOrDefault();

        HiloTrabajo hilo;
        if (reciente is null)
        {
            hilo = new HiloTrabajo(pregunta, CrearConversacionId(campania.Id, usuario.Id, pregunta.Id), null);
        }
        else if (reciente.Estado != EstadoConversacion.Cerrada)
        {
            hilo = new HiloTrabajo(pregunta, reciente.Id, reciente);
        }
        else if (contexto.IdeaIdReabrir is not null
            || await PideReaperturaEnHiloCerradoAsync(reciente, campania, mensaje.Texto, cancellationToken))
        {
            // P-26 §5.8: una petición explícita de complementar/revisitar NO crea idea ni ciclo nuevo.
            // Se reabre el hilo que contiene la idea y la ruta I-19 §4.7 hace el resto conservando el
            // mismo `ideaId` (lista numerada si hay varias candidatas y curaduría suspendida).
            var reabierta = reciente.Reabrir(ahora);
            await _conversaciones.GuardarConversacionAsync(reabierta, cancellationToken);
            if (contexto.IdeaIdReabrir is not null)
            {
                var ideaConsultada = await _respuestas.ObtenerIdeaConsolidadaAsync(
                    campania.Id, contexto.IdeaIdReabrir, cancellationToken);
                if (ideaConsultada is null
                    || ideaConsultada.UsuarioId != usuario.Id
                    || ideaConsultada.ConversacionId != reabierta.Id
                    || ideaConsultada.PreguntaId != pregunta.Id)
                {
                    return;
                }

                await _respuestas.GuardarIdeaConsolidadaAsync(ideaConsultada.Reabrir(ahora), cancellationToken);
            }
            await RegistrarEnrutamientoAsync(
                usuario,
                "reapertura",
                $"conversacion={reabierta.Id};ciclo={reabierta.CicloParticipacion}",
                ahora,
                cancellationToken);
            hilo = new HiloTrabajo(pregunta, reabierta.Id, reabierta);
        }
        else
        {
            var cicloId = CrearConversacionIdCiclo(campania.Id, usuario.Id, pregunta.Id, mensaje.WhatsappMessageId);
            var existente = await _conversaciones.ObtenerConversacionAsync(campania.Id, cicloId, cancellationToken);
            var conversacionCiclo = existente ?? DominioConversacion.Iniciar(
                cicloId,
                campania.Id,
                usuario.Id,
                pregunta.Id,
                Canal,
                correlationId: null,
                ahora,
                cicloParticipacion: reciente.CicloParticipacion + 1,
                origenAporteMessageId: mensaje.WhatsappMessageId,
                enrutamientoAporteId: contexto.EnrutamientoAporteId,
                idioma: usuario.Idioma);
            if (existente is null)
            {
                await _conversaciones.GuardarConversacionAsync(conversacionCiclo, cancellationToken);
                await RegistrarEnrutamientoAsync(
                    usuario,
                    "cicloNuevo",
                    $"conversacion={cicloId};ciclo={conversacionCiclo.CicloParticipacion}",
                    ahora,
                    cancellationToken);
            }

            hilo = new HiloTrabajo(pregunta, cicloId, conversacionCiclo);
        }

        await ProcesarEnHiloAsync(
            hilo, participante, mensaje, ahora, contexto.ClasificacionPrevia, contexto.IdeaIdConsultada,
            cancellationToken);
    }

    /// <summary>
    /// P-30: aplica la selección histórica ya resuelta por P-26. Revalida propiedad y alcance antes
    /// de reutilizar la reapertura I-19; el texto que expresó la intención/selección no entra a la idea.
    /// </summary>
    public async Task<bool> RetomarIdeaHistoricaAsync(
        ParticipanteResuelto participante,
        MensajeEntrante mensaje,
        ContextoRetomarIdea contexto,
        CancellationToken cancellationToken)
    {
        var usuario = participante.Usuario;
        var campania = participante.Campania;
        var ahora = _tiempo.GetUtcNow();
        var pregunta = campania.Preguntas.FirstOrDefault(
            candidata => candidata.Id == contexto.PreguntaId && candidata.Estado == EstadoRegistro.Activo);
        if (campania.Estado != EstadoCampania.Activa || pregunta is null)
        {
            return false;
        }

        var idea = await _respuestas.ObtenerIdeaConsolidadaAsync(
            campania.Id, contexto.IdeaId, cancellationToken);
        var conversacion = await _conversaciones.ObtenerConversacionAsync(
            campania.Id, contexto.ConversacionId, cancellationToken);
        if (idea is null
            || conversacion is null
            || idea.UsuarioId != usuario.Id
            || idea.PreguntaId != pregunta.Id
            || idea.ConversacionId != conversacion.Id
            || conversacion.UsuarioId != usuario.Id
            || conversacion.PreguntaId != pregunta.Id)
        {
            return false;
        }

        if (conversacion.Estado == EstadoConversacion.Cerrada)
        {
            conversacion = conversacion.Reabrir(ahora);
            await _conversaciones.GuardarConversacionAsync(conversacion, cancellationToken);
        }

        await ReabrirIdeaAsync(
            conversacion,
            campania,
            usuario,
            pregunta,
            usuario.WhatsappNormalizado,
            mensaje.PhoneNumberIdDestino,
            idea,
            ahora,
            cancellationToken);
        await _logSeguridad.RegistrarAsync(
            LogSeguridad.Crear(
                "log_" + Guid.NewGuid().ToString("N"),
                TipoEventoSeguridad.RetomarIdea,
                usuario.Id,
                usuario.WhatsappNormalizado.Valor,
                "reabierto",
                $"enrutamiento={contexto.EnrutamientoAporteId};conversacion={conversacion.Id};pregunta={pregunta.Id};idea={idea.Id}",
                _correlacion.CorrelationIdActual,
                ahora,
                campaniaId: campania.Id),
            cancellationToken);
        return true;
    }

    /// <summary>
    /// P-28: la entrada se emite sobre el mensaje entrante que abre la ventana de WhatsApp, pero no
    /// crea conversación ni idea. Así el siguiente aporte sustantivo vuelve a P-26 y no hereda el
    /// saludo como si fuera contenido.
    /// </summary>
    public async Task EnviarDespertarProactivoAsync(
        ParticipanteResuelto participante,
        MensajeEntrante mensaje,
        CancellationToken cancellationToken)
    {
        var ahora = _tiempo.GetUtcNow();
        var usuario = participante.Usuario;
        var campania = participante.Campania;
        var respaldo = await TextoGlobalParaIdiomaAsync(
            usuario.Idioma,
            "saludoReactivacion",
            TextoConfigurado(_mensajes.SaludoReactivacion, OpcionesMensajesConversacion.SaludoReactivacionDefault),
            cancellationToken);
        var texto = await ComponerTurnoAsync(
            campania,
            participante.PreguntaVigente,
            usuario.Id,
            usuario.WhatsappNormalizado,
            ActoConversacional.Reactivar,
            respaldo,
            ahora,
            cancellationToken,
            idioma: usuario.Idioma);

        var resultado = await _gateway.EnviarTextoAsync(
            usuario.WhatsappNormalizado.Valor,
            texto,
            TipoEnvioMensaje.Inicial,
            cancellationToken,
            mensaje.PhoneNumberIdDestino);
        await _participantes.RegistrarEnvioAsync(
            EnvioMensaje.Crear(
                "env_" + Guid.NewGuid().ToString("N"),
                campania.Id,
                usuario.Id,
                mensajeInicialId: null,
                usuario.WhatsappNormalizado,
                resultado.Exito ? EstadoEnvio.Enviado : EstadoEnvio.Error,
                TipoEnvioMensaje.Inicial,
                resultado.WhatsappMessageId,
                ahora,
                resultado.Error),
            cancellationToken);
        await RegistrarDespertarProactivoAsync(usuario, resultado.Exito ? "reactivacion" : "errorEnvio", ahora, cancellationToken);
    }

    public async Task<ResultadoConsultaIdeaMostrada> MostrarIdeaConsultadaAsync(
        ParticipanteResuelto participante,
        MensajeEntrante mensaje,
        ContextoConsultaIdea contexto,
        CancellationToken cancellationToken)
    {
        var usuario = participante.Usuario;
        var campania = participante.Campania;
        var ahora = _tiempo.GetUtcNow();
        var respaldoSinIdea = await TextoGlobalParaIdiomaAsync(
            usuario.Idioma, "sinIdeaDisponible",
            TextoConfigurado(_mensajes.SinIdeaDisponible, OpcionesMensajesConversacion.SinIdeaDisponibleDefault), cancellationToken);
        var texto = respaldoSinIdea;
        var visible = false;

        if (_visibilidadIdeaParticipanteHabilitada
            && _consolidacionProgresivaHabilitada
            && campania.Estado == EstadoCampania.Activa
            && campania.ConfigConversacional.ConsultaIdea
            && contexto.IdeaId is not null
            && contexto.PreguntaId is not null
            && contexto.ConversacionId is not null)
        {
            var idea = await _respuestas.ObtenerIdeaConsolidadaAsync(campania.Id, contexto.IdeaId, cancellationToken);
            var conversacion = await _conversaciones.ObtenerConversacionAsync(campania.Id, contexto.ConversacionId, cancellationToken);
            if (idea is not null && conversacion is not null
                && idea.UsuarioId == usuario.Id && idea.PreguntaId == contexto.PreguntaId
                && idea.ConversacionId == conversacion.Id && conversacion.UsuarioId == usuario.Id
                && idea.EstadoResultado != EstadoResultadoIdeaConsolidada.Rechazada)
            {
                var versionId = idea.VersionConfirmadaRef ?? idea.VersionPropuestaRef;
                var version = string.IsNullOrWhiteSpace(versionId)
                    ? null
                    : await _respuestas.ObtenerVersionIdeaAsync(campania.Id, versionId, cancellationToken);
                if (version is not null)
                {
                    var encabezado = await TextoGlobalParaIdiomaAsync(
                        usuario.Idioma, "encabezadoConsultaIdea",
                        TextoConfigurado(_mensajes.EncabezadoConsultaIdea, OpcionesMensajesConversacion.EncabezadoConsultaIdeaDefault), cancellationToken);
                    var invitacion = await TextoGlobalParaIdiomaAsync(
                        usuario.Idioma, "invitacionConsultaIdea",
                        TextoConfigurado(_mensajes.InvitacionConsultaIdea, OpcionesMensajesConversacion.InvitacionConsultaIdeaDefault), cancellationToken);
                    texto = Combinar(Combinar(encabezado, version.Texto), invitacion);
                    visible = true;
                }
            }
        }

        var resultado = await _gateway.EnviarTextoAsync(
            usuario.WhatsappNormalizado.Valor, texto, TipoEnvioMensaje.Repregunta, cancellationToken, mensaje.PhoneNumberIdDestino);
        await _logSeguridad.RegistrarAsync(LogSeguridad.Crear(
            "log_" + Guid.NewGuid().ToString("N"), TipoEventoSeguridad.VisibilidadIdeaParticipante,
            usuario.Id, usuario.WhatsappNormalizado.Valor,
            visible ? "consultaMostrada" : "consultaSinIdea",
            $"campania={campania.Id};idea={contexto.IdeaId ?? "ninguna"};envio={(resultado.Exito ? "ok" : "error")}",
            _correlacion.CorrelationIdActual, ahora, campaniaId: campania.Id), cancellationToken);
        return new ResultadoConsultaIdeaMostrada(visible, resultado.Exito);
    }

    private async Task ProcesarEnHiloAsync(
        HiloTrabajo hilo,
        ParticipanteResuelto participante,
        MensajeEntrante mensaje,
        DateTimeOffset ahora,
        ClasificacionIntencionPrevia? clasificacionPrevia,
        string? ideaIdConsultada,
        CancellationToken cancellationToken)
    {
        var usuario = participante.Usuario;
        var campania = participante.Campania;
        var numero = usuario.WhatsappNormalizado;
        var emisor = mensaje.PhoneNumberIdDestino;

        var pregunta = hilo.Pregunta;
        var conversacionId = hilo.ConversacionId;
        var conversacion = hilo.Conversacion;

        // Primer entrante de un hilo nuevo (05 §4): el envio inicial de campania puede haber sido
        // solo un saludo, asi que la pregunta vigente se envia aqui y este mensaje NO se evalua.
        // El SIGUIENTE entrante ya hallara esta conversacion creada y se evalua como respuesta.
        if (conversacion is null)
        {
            await ResponderPrimerContactoAsync(conversacionId, campania, usuario, pregunta, numero, mensaje, ahora, cancellationToken);
            return;
        }

        conversacion ??= DominioConversacion.Iniciar(
            conversacionId,
            campania.Id,
            usuario.Id,
            pregunta.Id,
            Canal,
            null,
            ahora,
            idioma: usuario.Idioma);
        var pendienteControl = conversacion.IntencionControlPendiente;
        if (conversacion.CoachingIdeas is { Estado: EstadoCoachingIdeas.Activo })
        {
            await ProcesarRevisionCoachingAsync(
                conversacion,
                campania,
                usuario,
                participante,
                pregunta,
                numero,
                emisor,
                mensaje,
                ahora,
                clasificacionPrevia,
                ideaIdConsultada,
                cancellationToken);
            return;
        }

        // I-19: el flujo canónico intercepta el hilo simple antes de que la ruta histórica evalúe el
        // último mensaje. Las campañas con cola I-18 entran por ProcesarIdeasSegmentadasAsync, que aplica
        // el mismo ciclo idea por idea; las campañas con I-06 sin coaching conservan su ruta histórica.
        var segmentacionLegacyActiva = _segmentacionIdeasHabilitada && campania.ConfigConversacional.SegmentacionIdeas;
        if (ConsolidacionIdeasActiva && !segmentacionLegacyActiva)
        {
            await ProcesarIdeaConsolidadaAsync(
                conversacion, campania, usuario, participante, pregunta, numero, emisor, mensaje, ahora,
                clasificacionPrevia, ideaIdConsultada, cancellationToken);
            return;
        }
        // Interpretacion determinista de la situacion (05 §4.4 salida conversacional; I-17 §5.4 rechazo del
        // guardado): solo cuentan cuando ya ofrecimos una mejora (esperandoRepregunta); el primer mensaje
        // (su respuesta real) nunca se interpreta asi. El rechazo devuelto es la intencion previa a la E/S:
        // debajo se confirma que exista al menos una respuesta madura que degradar antes de cerrar por ello.
        var detectores = await ResolverDetectoresAsync(conversacion, cancellationToken);
        var situacion = detectores.Transicion.Interpretar(
            conversacion.EstadoMaquina, conversacion.RepreguntasUsadas, pregunta.MaxRepreguntas, mensaje.Texto);
        var esRepregunta = situacion.EsRepregunta;
        var revisionesAgotadas = situacion.RevisionesAgotadas;
        var deseaContinuar = situacion.DeseaContinuar;
        var deseaRechazarGuardado = situacion.DeseaRechazarGuardado;
        IReadOnlyList<RespuestaUsuario> madurasAReclasificar = Array.Empty<RespuestaUsuario>();
        if (deseaRechazarGuardado)
        {
            madurasAReclasificar = (await _respuestas.ListarRespuestasAsync(campania.Id, cancellationToken))
                .Where(r => r.ConversacionId == conversacionId && r.NivelMadurez == NivelMadurez.Maduro)
                .ToArray();
            deseaRechazarGuardado = madurasAReclasificar.Count > 0;
        }

        // Techos deterministas (10 §2 / D2): el tope duro de turnos por hilo garantiza terminacion
        // aunque otras reglas pidan seguir; el cupo de llamadas LLM por usuario/campania evita costo
        // sin limite. Ambos cierran elegante con lo aportado (mismo camino que revisiones agotadas).
        var turnosExcedidos = ResolvedorTransicionConversacion.PermiteEvaluarTechos(revisionesAgotadas, deseaContinuar, deseaRechazarGuardado)
            && await TurnosHiloExcedidosAsync(conversacion, cancellationToken);
        // Cupo LLM de la campania: por usuario (llamadas) o por presupuesto de tokens (P-10). Ambos
        // cierran la campania para el hilo (no se abre la siguiente pregunta); el motivo se distingue
        // en LogSeguridad. Gateados por Conversacion:CuposHabilitados.
        var evaluarCupoLlm = ResolvedorTransicionConversacion.PermiteEvaluarTechos(revisionesAgotadas, deseaContinuar, deseaRechazarGuardado)
            && !turnosExcedidos && _cuposHabilitados;
        var cupoLlamadasUsuarioExcedido = evaluarCupoLlm
            && await CupoLlamadasLlmExcedidoAsync(campania, usuario.Id, ahora, cancellationToken);
        var presupuestoTokensExcedido = evaluarCupoLlm && !cupoLlamadasUsuarioExcedido
            && await PresupuestoTokensExcedidoAsync(campania, cancellationToken);
        var cupoLlmExcedido = cupoLlamadasUsuarioExcedido || presupuestoTokensExcedido;

        var mensajeId = await GuardarMensajeAsync(
            conversacion,
            DireccionMensaje.In,
            mensaje.Texto,
            mensaje.WhatsappMessageId,
            mensaje.Timestamp,
            cancellationToken);

        await MarcarParticipanteRespondioAsync(participante.Participante, ahora, cancellationToken);

        // P-27: tras el rechazo explícito y antes de techos/evaluación, un alias o candidato válido
        // puede cerrar la participación. El mensaje ya quedó auditado, pero no se vuelve Respuesta.
        if (!deseaRechazarGuardado)
        {
            var salidaPendiente = await ResolverSalidaPendienteAsync(
                campania, conversacion, pendienteControl, conversacion.EstadoMaquina, mensaje.Texto, numero, emisor,
                ahora, cancellationToken);
            if (salidaPendiente.Manejado)
            {
                return;
            }

            var decisionControl = salidaPendiente.Decision ?? await ResolverIntencionControlAsync(
                campania, usuario, conversacion, conversacion.EstadoMaquina, esRepregunta,
                quedanUnidadesPendientes: false, mensaje.Texto, ahora, clasificacionPrevia,
                permitirConfirmarIdea: false, cancellationToken);
            if (decisionControl == DecisionIntencionControl.FinalizarParticipacion)
            {
                await CerrarConAgradecimientoAsync(conversacion.RegistrarEntrante(mensaje.Timestamp), numero, campania, null, emisor, ahora, cancellationToken);
                return;
            }

            if (decisionControl == DecisionIntencionControl.FinalizarIdea)
            {
                await CerrarConAgradecimientoAsync(
                    conversacion.RegistrarEntrante(mensaje.Timestamp), numero, campania,
                    await SeleccionarAcuseContinuarAsync(conversacion, cancellationToken), emisor, ahora, cancellationToken);
                await EnviarSiguientePreguntaPendienteAsync(campania, usuario, pregunta, numero, emisor, ahora, cancellationToken);
                return;
            }

            if (decisionControl == DecisionIntencionControl.Ambigua)
            {
                await AbrirAclaracionSalidaAsync(conversacion, numero, emisor, ahora, cancellationToken);
                return;
            }
        }

        var respuestaId = "resp_" + Guid.NewGuid().ToString("N");

        if (ResolvedorTransicionConversacion.DebeCerrarSinEvaluar(revisionesAgotadas, deseaContinuar, deseaRechazarGuardado, turnosExcedidos, cupoLlmExcedido))
        {
            // Se agoto el cupo de revisiones/turnos/LLM, o el participante pidio continuar o rechazo el
            // guardado: se registra sin evaluar y se cierra. Si pidio continuar o rechazo, se antepone un
            // acuse calido. Los techos deterministas dejan ademas rastro RateLimit en LogSeguridad.
            conversacion = conversacion.RegistrarEntrante(mensaje.Timestamp);
            await _procesador.GuardarRespuestaAsync(
                respuestaId,
                campania.Id,
                usuario,
                pregunta,
                conversacionId,
                mensaje.Texto,
                esRepregunta,
                EstadoRespuesta.Recibida,
                ahora,
                cancellationToken);
            if (turnosExcedidos || cupoLlmExcedido)
            {
                var motivo = ResolvedorTransicionConversacion.MotivoTecho(turnosExcedidos, cupoLlamadasUsuarioExcedido);
                await RegistrarRateLimitAsync(usuario, motivo, ahora, cancellationToken);
            }

            // I-17 §5.4: el rechazo explicito degrada a incubacion las respuestas maduras del hilo
            // (regenera su Markdown y registra telemetria) antes de cerrar con el acuse de rechazo.
            if (deseaRechazarGuardado)
            {
                await _procesador.ReclasificarComoIncubacionAsync(campania, usuario, pregunta, madurasAReclasificar, ahora, cancellationToken);
            }

            var acuse = deseaRechazarGuardado
                ? await TextoGlobalAsync(
                    conversacion,
                    "acuseRechazoGuardado",
                    TextoConfigurado(_mensajes.AcuseRechazoGuardado, OpcionesMensajesConversacion.AcuseRechazoGuardadoDefault),
                    cancellationToken)
                : deseaContinuar
                    ? await SeleccionarAcuseContinuarAsync(conversacion, cancellationToken)
                    : null;
            await CerrarConAgradecimientoAsync(conversacion, numero, campania, acuse, emisor, ahora, cancellationToken);
            if (!cupoLlmExcedido)
            {
                // Con el cupo LLM de la campania agotado no tiene sentido abrir la siguiente pregunta
                // (tampoco podria evaluarse); en los demas cierres se avanza como siempre.
                await EnviarSiguientePreguntaPendienteAsync(campania, usuario, pregunta, numero, emisor, ahora, cancellationToken);
            }

            return;
        }

        conversacion = conversacion.RegistrarEntrante(mensaje.Timestamp).AvanzarA(EstadoMaquinaConversacion.Evaluando);
        await _conversaciones.GuardarConversacionAsync(conversacion, cancellationToken);

        var contexto = await ConstruirContextoAsync(campania, pregunta, usuario, conversacionId, respuestaId, mensaje.Texto, cancellationToken);

        if (contexto.Contexto is null)
        {
            // Sin configuracion completa (rubrica/prompt/configLLM) no se puede evaluar: se informa
            // un problema operativo al participante y se cierra sin llamar al LLM.
            await RegistrarConfiguracionNoDisponibleAsync(usuario, contexto.Motivo ?? "configuracion_no_disponible", ahora, cancellationToken);
            await _procesador.GuardarRespuestaAsync(respuestaId, campania.Id, usuario, pregunta, conversacionId, mensaje.Texto, esRepregunta, EstadoRespuesta.EvaluacionPendiente, ahora, cancellationToken);
            await CerrarPorConfiguracionNoDisponibleAsync(conversacion, numero, emisor, ahora, cancellationToken);
            return;
        }

        // I-09 tejido colectivo (05 §4.8): si la campania lo activa y el kill-switch global no lo apaga,
        // se enriquece el contexto con aportes anonimizados de otros participantes ANTES de evaluar. La
        // recuperacion nunca bloquea el hilo: sin aportes o ante error degrada a autocontenido.
        var contextoEval = contexto.Contexto with
        {
            // I-05 (05 §4.5): el flag por campaña nace apagado y el kill-switch global evita incluso
            // solicitar el campo al LLM. Cero caracteres degrada a la retroalimentación previa.
            SolicitarParafraseo = _parafraseoHabilitado && campania.ConfigConversacional.Parafraseo && _maxCaracteresParafraseo > 0,
            MaxCaracteresParafraseo = _maxCaracteresParafraseo,
        };
        if (_tejidoColectivoHabilitado && campania.ConfigConversacional.TejidoColectivo)
        {
            contextoEval = await AplicarTejidoColectivoAsync(
                contextoEval, usuario, conversacionId, mensaje.Texto, ahora, cancellationToken);
        }

        if (_segmentacionIdeasHabilitada && campania.ConfigConversacional.SegmentacionIdeas)
        {
            var respuestaPadreId = string.IsNullOrWhiteSpace(mensaje.WhatsappMessageId)
                ? mensajeId
                : mensaje.WhatsappMessageId;
            await ProcesarIdeasSegmentadasAsync(
                conversacion,
                campania,
                usuario,
                pregunta,
                numero,
                emisor,
                contextoEval,
                mensaje.Texto,
                respuestaPadreId,
                esRepregunta,
                ahora,
                cancellationToken);
            return;
        }

        var resultado = await _evaluador.EvaluarAsync(contextoEval, cancellationToken);
        var escala = contexto.Contexto.RubricaSnapshot.Escala;

        // Efectos posteriores a la evaluacion (P-15 Corte 3): persistir evaluacion+respuesta, sellar la
        // madurez (I-17) y compilar Markdown, en el mismo orden observable; devuelve el nivel sellado.
        var nivelMadurez = await _procesador.PersistirRespuestaEvaluadaAsync(
            resultado, campania, pregunta, usuario, conversacionId, respuestaId, mensaje.Texto, esRepregunta, escala, ahora, cancellationToken);

        var esFallback = resultado is ResultadoEvaluacion.Fallback;
        var evaluacion = resultado.Evaluacion;

        // I-17: la paráfrasis "esto es lo que entendí" (I-05) solo se antepone cuando la idea es madura;
        // en incubación se mantiene la retro/invitación habitual (la idea aún no está lista para guardar).
        var parafraseoMostrable = nivelMadurez == NivelMadurez.Maduro ? evaluacion.ParafraseoDevuelto : null;

        // Cierre anticipado por calificacion alta (05 §4.4): si la calificacion supera el umbral
        // configurado y el cierre está habilitado, no se insiste con una revision; se felicita y cierra.
        var umbralCierre = _limites.ResolverUmbralCierreAnticipado(campania, pregunta);
        var calificacionAlta = !esFallback
            && _limites.UmbralAlcanzado(evaluacion.CalificacionTotal, escala, umbralCierre);

        // Mejora deterministica (05 §4.4): tras una evaluacion valida se ofrece una revision
        // (hasta MaxRepreguntas, default 1) con la retro como base. Si el siguiente mensaje llega
        // con el cupo agotado, se registra sin evaluarlo y se cierra con agradecimiento.
        var ofrecerMejora = !esFallback && !calificacionAlta && _limites.PuedeOfrecerMejora(conversacion, pregunta);
        if (ofrecerMejora)
        {
            var invitacion = await ConstruirInvitacionMejoraAsync(
                conversacion,
                evaluacion.RepreguntaSugerida,
                cancellationToken);
            var texto = CombinarSinDuplicar(
                Combinar(parafraseoMostrable, evaluacion.RetroalimentacionEnviada), invitacion);
            await EnviarAsync(conversacion, numero, texto, TipoEnvioMensaje.Repregunta, emisor, ahora, cancellationToken);

            conversacion = conversacion.RegistrarRepregunta();
            await _conversaciones.GuardarConversacionAsync(conversacion, cancellationToken);
            return;
        }

        // DT-P32-03 §3.1: el cierre localizado se resuelve antes de registrar telemetría o mutar el
        // hilo; si el idioma no tiene contenido, la ruta cierra con el manejo tipificado.
        var cierreCampania = await ResolverMensajeCierreAsync(
            conversacion, campania, numero, emisor, "cierreEvaluacion", ahora, cancellationToken);
        if (cierreCampania is null)
        {
            return;
        }

        // Cierre: retro + agradecimiento en un solo mensaje (tipo Cierre). Si cerro por calificacion
        // alta se intercala una felicitacion para que el corte temprano se sienta natural.
        if (calificacionAlta)
        {
            await _procesador.RegistrarCierreUmbralAsync(
                usuario,
                evaluacion.CalificacionTotal,
                _limites.ValorUmbral(escala, umbralCierre),
                escala,
                umbralCierre,
                _limites.OrigenUmbral(campania, pregunta),
                ahora,
                cancellationToken);
        }

        var cierreFinal = calificacionAlta
            ? Combinar(
                await TextoGlobalAsync(
                    conversacion,
                    "mensajeCalificacionAlta",
                    TextoConfigurado(_mensajes.MensajeCalificacionAlta, OpcionesMensajesConversacion.MensajeCalificacionAltaDefault),
                    cancellationToken),
                cierreCampania)
            : cierreCampania;
        var cierre = Combinar(Combinar(parafraseoMostrable, evaluacion.RetroalimentacionEnviada), cierreFinal);
        await EnviarAsync(conversacion, numero, cierre, TipoEnvioMensaje.Cierre, emisor, ahora, cancellationToken);

        conversacion = conversacion.Cerrar(ahora);
        await _conversaciones.GuardarConversacionAsync(conversacion, cancellationToken);

        await EnviarSiguientePreguntaPendienteAsync(campania, usuario, pregunta, numero, emisor, ahora, cancellationToken);
    }

    /// <summary>
    /// I-19 §4.1-§4.3: conserva el aporte original, propone una versión completa, exige confirmación y
    /// solo entonces evalúa exactamente ese texto. Esta primera integración aplica al hilo de una idea;
    /// la cola I-18 se adapta en el siguiente corte para conservar su semántica una-a-una.
    /// </summary>
    private async Task ProcesarIdeaConsolidadaAsync(
        DominioConversacion conversacion,
        Campania campania,
        Usuario usuario,
        ParticipanteResuelto participante,
        Pregunta pregunta,
        NumeroWhatsApp numero,
        string? emisor,
        MensajeEntrante mensaje,
        DateTimeOffset ahora,
        ClasificacionIntencionPrevia? clasificacionPrevia,
        string? ideaIdConsultada,
        CancellationToken cancellationToken)
    {
        var estadoPrevio = conversacion.EstadoMaquina;
        var pendienteControl = conversacion.IntencionControlPendiente;
        // Techos deterministas (10 §2 / D2) antes de consolidar o evaluar: el hilo simple I-19 los
        // evalúa aquí porque ya no pasa por la rama histórica que los aplicaba.
        var motivoTecho = await MotivoTechoAlcanzadoAsync(campania, conversacion, usuario.Id, ahora, cancellationToken);
        await GuardarMensajeAsync(conversacion, DireccionMensaje.In, mensaje.Texto, mensaje.WhatsappMessageId, mensaje.Timestamp, cancellationToken);
        await MarcarParticipanteRespondioAsync(participante.Participante, ahora, cancellationToken);
        conversacion = conversacion.RegistrarEntrante(mensaje.Timestamp).AvanzarA(EstadoMaquinaConversacion.Evaluando);
        await _conversaciones.GuardarConversacionAsync(conversacion, cancellationToken);

        if (motivoTecho is not null)
        {
            await CerrarPorTechoDeterministaAsync(
                conversacion, campania, usuario, pregunta, numero, emisor, mensaje.Texto, motivoTecho,
                ahora, cancellationToken);
            return;
        }

        // I-19 §4.7: la respuesta a una lista de selección ya ofrecida se resuelve antes que nada; si no
        // es un número válido, la selección se cancela y el mensaje sigue como un turno normal.
        var seleccion = await ResolverSeleccionIdeaPendienteAsync(
            conversacion, campania, usuario, pregunta, numero, emisor, mensaje.Texto, estadoPrevio, ahora, cancellationToken);
        conversacion = seleccion.Conversacion;
        if (seleccion.Manejado
            || await IntentarReaperturaIdeaAsync(
                conversacion, campania, usuario, pregunta, numero, emisor, mensaje.Texto, ahora, cancellationToken))
        {
            return;
        }

        // I-19 §4.6: una sola idea activa y en orden de llegada, para terminar la que está en curso
        // antes de empezar la siguiente aunque el mensaje anterior haya dejado ideas nuevas esperando.
        var idea = await ObtenerIdeaActivaAsync(campania.Id, conversacion.Id, cancellationToken);

        if (idea is null)
        {
            await CrearPropuestaInicialAsync(conversacion, campania, usuario, pregunta, numero, emisor, mensaje.Texto, ahora, cancellationToken);
            return;
        }

        var salidaPendiente = await ResolverSalidaPendienteAsync(
            campania, conversacion, pendienteControl, estadoPrevio, mensaje.Texto, numero, emisor, ahora,
            cancellationToken);
        if (salidaPendiente.Manejado)
        {
            return;
        }

        var decisionControl = salidaPendiente.Decision ?? await ResolverIntencionControlAsync(
            campania, usuario, conversacion, estadoPrevio, hayUnidadActiva: true,
            quedanUnidadesPendientes: await HayOtraIdeaAbiertaAsync(campania.Id, conversacion.Id, idea.Id, cancellationToken),
            mensaje.Texto, ahora, clasificacionPrevia,
            permitirConfirmarIdea: string.Equals(ideaIdConsultada, idea.Id, StringComparison.Ordinal), cancellationToken);
        if (await EjecutarControlSimpleAsync(
                decisionControl, conversacion, campania, usuario, pregunta, numero, emisor, idea, ahora,
                cancellationToken))
        {
            return;
        }

        if (decisionControl == DecisionIntencionControl.ConfirmarIdea
            && idea.EstadoFlujo != EstadoFlujoIdeaConsolidada.PendienteConfirmacion)
        {
            await CerrarIdeaPorControlAsync(idea, campania, usuario, "participante", ahora, cancellationToken);
            var acuse = await SeleccionarAcuseContinuarAsync(conversacion, cancellationToken);
            if (!await ContinuarConIdeaEnEsperaAsync(
                    conversacion, campania, usuario, pregunta, numero, emisor, acuse, ahora, cancellationToken))
            {
                await CerrarConAgradecimientoAsync(
                    conversacion, numero, campania, acuse, emisor, ahora, cancellationToken,
                    omitirIdeaVisible: true);
                await EnviarSiguientePreguntaPendienteAsync(
                    campania, usuario, pregunta, numero, emisor, ahora, cancellationToken);
            }

            return;
        }

        if (idea.EstadoFlujo == EstadoFlujoIdeaConsolidada.PendienteConfirmacion)
        {
            await ConfirmarOCorregirIdeaAsync(
                conversacion, campania, usuario, pregunta, numero, emisor, idea, mensaje.Texto, ahora,
                cancellationToken, confirmacionSemantica: decisionControl == DecisionIntencionControl.ConfirmarIdea);
            return;
        }

        // En mejora, el texto entrante es un nuevo aporte; no se evalúa aislado.
        await CrearPropuestaComplementariaAsync(conversacion, campania, usuario, pregunta, numero, emisor, idea, mensaje.Texto, ahora, cancellationToken);
    }

    /// <summary>
    /// I-19 §4.7: intenta atender una petición de revisitar una idea cerrada. Con “la anterior” resuelve
    /// determinísticamente la más reciente; con una petición vaga y una sola candidata reabre esa; con
    /// varias ofrece una lista breve numerada. Devuelve <c>false</c> si el mensaje no es una petición de
    /// revisitar o si no hay nada que reabrir, para que el turno siga su curso normal.
    /// </summary>
    private async Task<bool> IntentarReaperturaIdeaAsync(
        DominioConversacion conversacion,
        Campania campania,
        Usuario usuario,
        Pregunta pregunta,
        NumeroWhatsApp numero,
        string? emisor,
        string texto,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        // §4.7: una campaña cerrada no acepta cambios del participante.
        if (!ConsolidacionIdeasActiva || campania.Estado != EstadoCampania.Activa)
        {
            return false;
        }

        var detectores = await ResolverDetectoresAsync(conversacion, cancellationToken);
        var pideAnterior = detectores.RevisitarAnterior.Coincide(texto);
        if (!pideAnterior && !detectores.RevisitarIdea.Coincide(texto))
        {
            return false;
        }

        var candidatas = await CandidatasReaperturaAsync(conversacion, campania.Id, cancellationToken);
        if (candidatas.Count == 0)
        {
            return false;
        }

        if (pideAnterior || candidatas.Count == 1)
        {
            await ReabrirIdeaAsync(
                conversacion, campania, usuario, pregunta, numero, emisor, candidatas[0], ahora, cancellationToken);
            return true;
        }

        await OfrecerSeleccionIdeaAsync(conversacion, campania, numero, emisor, candidatas, ahora, cancellationToken);
        return true;
    }

    /// <summary>
    /// I-19 §4.7: resuelve la lista numerada ya ofrecida. La lista se reconstruye con el mismo orden
    /// determinista, así que no hace falta persistirla. Una respuesta que no es un número válido no se
    /// adivina: cancela la selección y el mensaje sigue como un turno normal de la idea activa.
    /// </summary>
    private async Task<ResultadoSeleccionIdea> ResolverSeleccionIdeaPendienteAsync(
        DominioConversacion conversacion,
        Campania campania,
        Usuario usuario,
        Pregunta pregunta,
        NumeroWhatsApp numero,
        string? emisor,
        string texto,
        EstadoMaquinaConversacion estadoPrevio,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        // El estado se lee del turno anterior: al recibir el entrante el hilo ya pasó a `evaluando`.
        if (estadoPrevio != EstadoMaquinaConversacion.EsperandoSeleccionIdea)
        {
            return new ResultadoSeleccionIdea(false, conversacion);
        }

        var candidatas = await CandidatasReaperturaAsync(conversacion, campania.Id, cancellationToken);
        var elegida = NumeroSeleccionado(texto, candidatas.Count);
        if (elegida is null)
        {
            return new ResultadoSeleccionIdea(
                false, conversacion.AvanzarA(EstadoMaquinaConversacion.EsperandoRepregunta));
        }

        await ReabrirIdeaAsync(
            conversacion, campania, usuario, pregunta, numero, emisor, candidatas[elegida.Value - 1],
            ahora, cancellationToken);
        return new ResultadoSeleccionIdea(true, conversacion);
    }

    /// <summary>
    /// Ideas cerradas que el participante puede reabrir en este hilo, de cierre más reciente a más
    /// antiguo (“la anterior” = la primera). Con cola I-18 solo cuentan las que están en la cola.
    /// </summary>
    private async Task<IReadOnlyList<IdeaConsolidada>> CandidatasReaperturaAsync(
        DominioConversacion conversacion, string campaniaId, CancellationToken cancellationToken)
    {
        var cola = conversacion.CoachingIdeas;
        return (await _respuestas.ListarIdeasConsolidadasAsync(campaniaId, cancellationToken))
            .Where(idea => idea.ConversacionId == conversacion.Id
                && idea.EstadoFlujo == EstadoFlujoIdeaConsolidada.Cerrada
                && (cola is null || cola.Ideas.Any(entrada => entrada.IdeaId == idea.Id)))
            .OrderByDescending(idea => idea.ActualizadaEn)
            .ThenByDescending(idea => idea.IdeaIndice)
            .Take(_maxIdeasPorMensaje)
            .ToArray();
    }

    /// <summary>
    /// I-19 §4.7: reabre la idea seleccionada conservando su <c>ideaId</c> y su historial. La versión
    /// confirmada anterior sigue siendo la oficial mientras se prepara la nueva; la curaduría pendiente
    /// queda suspendida y la idea que estaba activa vuelve a la cola en su estado.
    /// </summary>
    private async Task ReabrirIdeaAsync(
        DominioConversacion conversacion,
        Campania campania,
        Usuario usuario,
        Pregunta pregunta,
        NumeroWhatsApp numero,
        string? emisor,
        IdeaConsolidada idea,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        var reabierta = idea.Reabrir(ahora);
        await _respuestas.GuardarIdeaConsolidadaAsync(reabierta, cancellationToken);
        await RegistrarConsolidacionAsync(
            usuario, "reabierta", reabierta, null, "peticionParticipante", null, ahora, cancellationToken);
        if (conversacion.CoachingIdeas is not null)
        {
            conversacion = conversacion.ConCoachingIdeas(
                _colaCoaching.ReactivarIdea(conversacion.CoachingIdeas, idea.Id, ahora));
            await RegistrarCoachingAsync(
                usuario.Id, usuario.WhatsappNormalizado, "reabierta", conversacion.CoachingIdeas!, null,
                ahora, cancellationToken);
        }

        var confirmada = await ObtenerVersionAsync(campania.Id, idea.VersionConfirmadaRef, cancellationToken);
        var acuse = await TextoGlobalAsync(
            conversacion,
            "acuseReaperturaIdea",
            TextoConfigurado(_mensajes.AcuseReaperturaIdea, OpcionesMensajesConversacion.AcuseReaperturaIdeaDefault),
            cancellationToken);
        var invitacion = await TextoGlobalAsync(
            conversacion,
            "invitacionReaperturaIdea",
            TextoConfigurado(_mensajes.InvitacionReaperturaIdea, OpcionesMensajesConversacion.InvitacionReaperturaIdeaDefault),
            cancellationToken);
        // I-20: el acuse y la invitación se redactan; la versión oficial se muestra íntegra en el medio.
        var texto = await ComponerTurnoAsync(
            campania, pregunta, usuario.Id, usuario.WhatsappNormalizado, ActoConversacional.Reabrir,
            respaldo: Combinar(Combinar(acuse, confirmada?.Texto ?? string.Empty), invitacion),
            ahora, cancellationToken,
            cuerpo: confirmada?.Texto,
            versionCompleta: confirmada?.Texto,
            idioma: conversacion.Idioma);
        await EnviarAsync(conversacion, numero, texto, TipoEnvioMensaje.Repregunta, emisor, ahora, cancellationToken);
        await _conversaciones.GuardarConversacionAsync(
            conversacion.AvanzarA(EstadoMaquinaConversacion.EsperandoRepregunta), cancellationToken);
    }

    /// <summary>
    /// I-19 §4.7: lista breve y numerada de paráfrasis —sin calificaciones— para que el participante
    /// elija cuál idea retomar. El hilo queda en <c>esperandoSeleccionIdea</c> (03 §3.6).
    /// </summary>
    private async Task OfrecerSeleccionIdeaAsync(
        DominioConversacion conversacion,
        Campania campania,
        NumeroWhatsApp numero,
        string? emisor,
        IReadOnlyList<IdeaConsolidada> candidatas,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        var lineas = new List<string>(candidatas.Count);
        for (var indice = 0; indice < candidatas.Count; indice++)
        {
            var version = await ObtenerVersionAsync(
                    campania.Id, candidatas[indice].VersionConfirmadaRef, cancellationToken)
                ?? await ObtenerVersionAsync(campania.Id, candidatas[indice].VersionPropuestaRef, cancellationToken);
            lineas.Add(FormattableString.Invariant(
                $"{indice + 1}. {Acotar(version?.Texto ?? string.Empty, MaxCaracteresParafrasisSeleccion)}"));
        }

        var pregunta = await TextoGlobalAsync(
            conversacion,
            "preguntaSeleccionIdea",
            TextoConfigurado(_mensajes.PreguntaSeleccionIdea, OpcionesMensajesConversacion.PreguntaSeleccionIdeaDefault),
            cancellationToken);
        await EnviarAsync(
            conversacion, numero, Combinar(pregunta, string.Join("\n", lineas)), TipoEnvioMensaje.Repregunta,
            emisor, ahora, cancellationToken);
        await _conversaciones.GuardarConversacionAsync(
            conversacion.AvanzarA(EstadoMaquinaConversacion.EsperandoSeleccionIdea), cancellationToken);
    }

    /// <summary>
    /// Número elegido de una lista de <paramref name="total"/> opciones. Solo cuenta en mensajes cortos
    /// (misma guarda que las intenciones deterministas) y dentro del rango ofrecido.
    /// </summary>
    private int? NumeroSeleccionado(string? texto, int total)
    {
        var limpio = texto?.Trim();
        if (string.IsNullOrEmpty(limpio) || (_maxCaracteresIntencion > 0 && limpio.Length > _maxCaracteresIntencion))
        {
            return null;
        }

        var digitos = new string(limpio.Where(char.IsAsciiDigit).ToArray());
        return digitos.Length is > 0 and <= 2
            && int.TryParse(digitos, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var numero)
            && numero >= 1
            && numero <= total
                ? numero
                : null;
    }

    /// <summary>
    /// Techo determinista alcanzado (10 §2 / D2), o <c>null</c> si todavía hay margen: tope de turnos
    /// del hilo, cupo de llamadas LLM del usuario —que incluye consolidaciones I-19 y clasificaciones P-27— y
    /// presupuesto de tokens de la campaña. Los cupos siguen gateados por <c>CuposHabilitados</c>.
    /// </summary>
    private async Task<string?> MotivoTechoAlcanzadoAsync(
        Campania campania,
        DominioConversacion conversacion,
        string usuarioId,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        if (await TurnosHiloExcedidosAsync(conversacion, cancellationToken))
        {
            return "tope_turnos_hilo";
        }

        if (!_cuposHabilitados)
        {
            return null;
        }

        if (await CupoLlamadasLlmExcedidoAsync(campania, usuarioId, ahora, cancellationToken))
        {
            return "cupo_llamadas_llm_usuario";
        }

        return await PresupuestoTokensExcedidoAsync(campania, cancellationToken)
            ? "presupuesto_tokens_campania"
            : null;
    }

    private async Task<string?> MotivoCupoLlmAsync(
        Campania campania,
        string usuarioId,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        if (await CupoLlamadasLlmExcedidoAsync(campania, usuarioId, ahora, cancellationToken))
        {
            return "cupo_llamadas_llm_usuario";
        }

        return await PresupuestoTokensExcedidoAsync(campania, cancellationToken)
            ? "presupuesto_tokens_campania"
            : null;
    }

    /// <summary>
    /// I-19 §12.3: al agotarse un techo no se consolida ni se evalúa, pero **el aporte se conserva**;
    /// la idea en curso queda <c>pendiente</c> y el hilo cierra con agradecimiento. Solo el tope de
    /// turnos abre la siguiente pregunta: sin cupo LLM tampoco podría evaluarse.
    /// </summary>
    private async Task CerrarPorTechoDeterministaAsync(
        DominioConversacion conversacion,
        Campania campania,
        Usuario usuario,
        Pregunta pregunta,
        NumeroWhatsApp numero,
        string? emisor,
        string texto,
        string motivoTecho,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        var idea = await ObtenerIdeaActivaAsync(campania.Id, conversacion.Id, cancellationToken);
        await _procesador.GuardarRespuestaAsync(
            "resp_" + Guid.NewGuid().ToString("N"), campania.Id, usuario, pregunta, conversacion.Id, texto,
            esRepregunta: idea is not null, EstadoRespuesta.Recibida, ahora, cancellationToken,
            // Sin idea activa el aporte se conserva suelto, como en el flujo histórico: `ideaId` y
            // `tipoAporte` solo se informan juntos y no hay idea a la que enlazarlo.
            ideaId: idea?.Id,
            tipoAporte: idea is null ? null : TipoAporteIdea.Complemento);

        if (idea is not null)
        {
            var cerrada = idea.Cerrar(EstadoResultadoIdeaConsolidada.Pendiente, null, "techoDeterminista", ahora);
            await _respuestas.GuardarIdeaConsolidadaAsync(cerrada, cancellationToken);
            await RegistrarConsolidacionAsync(
                usuario, "cerrada", cerrada, null, "techoDeterminista", null, ahora, cancellationToken);
            await _procesador.CompilarMarkdownIdeaAsync(campania.Id, idea.Id, cancellationToken);
        }

        await RegistrarRateLimitAsync(usuario, motivoTecho, ahora, cancellationToken);
        await CerrarConAgradecimientoAsync(conversacion, numero, campania, null, emisor, ahora, cancellationToken);
        if (motivoTecho == "tope_turnos_hilo")
        {
            await EnviarSiguientePreguntaPendienteAsync(campania, usuario, pregunta, numero, emisor, ahora, cancellationToken);
        }
    }

    /// <summary>
    /// Idea abierta que corresponde atender en un hilo sin cola I-18: la de menor índice/antigüedad,
    /// para que la activa se termine antes de pasar a una idea nueva encolada (I-19 §4.6).
    /// </summary>
    private async Task<IdeaConsolidada?> ObtenerIdeaActivaAsync(
        string campaniaId, string conversacionId, CancellationToken cancellationToken)
        => (await _respuestas.ListarIdeasConsolidadasAsync(campaniaId, cancellationToken))
            .Where(candidata => candidata.ConversacionId == conversacionId
                && candidata.EstadoFlujo != EstadoFlujoIdeaConsolidada.Cerrada)
            .OrderBy(candidata => candidata.IdeaIndice)
            .ThenBy(candidata => candidata.CreadaEn)
            .FirstOrDefault();

    private async Task<bool> HayOtraIdeaAbiertaAsync(
        string campaniaId, string conversacionId, string ideaActivaId, CancellationToken cancellationToken)
        => (await _respuestas.ListarIdeasConsolidadasAsync(campaniaId, cancellationToken))
            .Any(idea => idea.ConversacionId == conversacionId
                && idea.Id != ideaActivaId
                && idea.EstadoFlujo != EstadoFlujoIdeaConsolidada.Cerrada);

    /// <summary>
    /// P-27: los alias deterministas se resuelven siempre; el candidato LLM solo se consulta con ambos
    /// opt-ins, estado elegible y cupos disponibles. El resultado sigue siendo una propuesta sometida a
    /// la política pura.
    /// </summary>
    private async Task<DecisionIntencionControl> ResolverIntencionControlAsync(
        Campania campania,
        Usuario usuario,
        DominioConversacion conversacion,
        EstadoMaquinaConversacion estadoPrevio,
        bool hayUnidadActiva,
        bool quedanUnidadesPendientes,
        string texto,
        DateTimeOffset ahora,
        ClasificacionIntencionPrevia? clasificacionPrevia,
        bool permitirConfirmarIdea,
        CancellationToken cancellationToken)
    {
        var politica = (await ResolverDetectoresAsync(conversacion, cancellationToken)).PoliticaIntencionControl;
        var determinista = politica.Resolver(estadoPrevio, hayUnidadActiva, texto);
        if (determinista is DecisionIntencionControl.FinalizarIdea or DecisionIntencionControl.FinalizarParticipacion)
        {
            await RegistrarClasificacionIntencionControlAsync(
                campania.Id, usuario, estadoPrevio, "determinista", "clasificada", determinista, null, "ninguno", false, ahora,
                cancellationToken);
            return determinista;
        }

        if (clasificacionPrevia is not null)
        {
            if (permitirConfirmarIdea && clasificacionPrevia.Intencion == IntencionControl.ConfirmarIdea)
            {
                return DecisionIntencionControl.ConfirmarIdea;
            }

            if (!ClasificacionIntencionControlEfectiva(campania))
            {
                return DecisionIntencionControl.Aportar;
            }

            return politica.Resolver(estadoPrevio, hayUnidadActiva, texto, clasificacionPrevia.Intencion);
        }

        if (!_clasificacionIntencionControlHabilitada
            || !campania.ConfigConversacional.ClasificacionIntencionControl
            || _clasificadorIntencionControl is null
            || !PoliticaIntencionControl.EsElegible(estadoPrevio, hayUnidadActiva)
            || string.IsNullOrWhiteSpace(campania.ConfigLlmRef))
        {
            return DecisionIntencionControl.Aportar;
        }

        var configLlm = await _configuracion.ObtenerConfigLlmAsync(campania.ConfigLlmRef, cancellationToken);
        if (configLlm is null || configLlm.Estado != EstadoRegistro.Activo)
        {
            return DecisionIntencionControl.Aportar;
        }

        var motivoCupo = _cuposHabilitados
            ? await MotivoCupoLlmAsync(campania, usuario.Id, ahora, cancellationToken)
            : null;
        if (motivoCupo is not null)
        {
            await RegistrarClasificacionIntencionControlAsync(
                campania.Id, usuario, estadoPrevio, "llm", "omitida", null, null, motivoCupo, false, ahora,
                cancellationToken);
            return DecisionIntencionControl.Aportar;
        }

        var actoPrevio = estadoPrevio == EstadoMaquinaConversacion.EsperandoConfirmacionSalida
            ? ActoPrevioIntencionControl.Confirmar
            : ActoPrevioIntencionControl.Mejorar;
        var resultado = await _clasificadorIntencionControl.ClasificarAsync(
            new ContextoClasificacionIntencionControl(
                estadoPrevio, actoPrevio, hayUnidadActiva, quedanUnidadesPendientes, null, texto, configLlm),
            cancellationToken);
        if (resultado is ResultadoClasificacionIntencionControl.Exito exito)
        {
            var decision = politica.Resolver(estadoPrevio, hayUnidadActiva, texto, exito.Intencion);
            await RegistrarClasificacionIntencionControlAsync(
                campania.Id,
                usuario,
                estadoPrevio,
                "llm",
                decision == DecisionIntencionControl.Ambigua ? "ambigua" : "clasificada",
                decision,
                exito.Uso,
                "ninguno",
                true,
                ahora,
                cancellationToken);
            return decision;
        }

        var fallback = (ResultadoClasificacionIntencionControl.Fallback)resultado;
        await RegistrarClasificacionIntencionControlAsync(
            campania.Id, usuario, estadoPrevio, "llm", "fallback", null, fallback.Uso, fallback.Motivo, true, ahora,
            cancellationToken);
        return DecisionIntencionControl.Aportar;
    }

    private Task RegistrarClasificacionIntencionControlAsync(
        string campaniaId,
        Usuario usuario,
        EstadoMaquinaConversacion estado,
        string origen,
        string resultado,
        DecisionIntencionControl? intencion,
        UsoTokensLlm? uso,
        string motivo,
        bool esLlamadaLlm,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        var valorIntencion = intencion switch
        {
            DecisionIntencionControl.Aportar => "aportar",
            DecisionIntencionControl.FinalizarIdea => "finalizarIdea",
            DecisionIntencionControl.FinalizarParticipacion => "finalizarParticipacion",
            DecisionIntencionControl.Ambigua => "ninguna",
            _ => "ninguna",
        };
        var detalle = FormattableString.Invariant(
            $"origen:{origen};resultado:{resultado};intencion:{valorIntencion};estado:{MinusculaInicial(estado.ToString())};promptTokens:{uso?.PromptTokens ?? 0};completionTokens:{uso?.CompletionTokens ?? 0};motivo:{motivo}");
        return _logSeguridad.RegistrarAsync(
            LogSeguridad.Crear(
                "log_" + Guid.NewGuid().ToString("N"),
                TipoEventoSeguridad.ClasificacionIntencionControl,
                usuario.Id,
                usuario.WhatsappNormalizado.Valor,
                resultado,
                detalle,
                _correlacion.CorrelationIdActual,
                ahora,
                campaniaId,
                uso?.PromptTokens ?? 0,
                uso?.CompletionTokens ?? 0,
                esLlamadaLlm),
            cancellationToken);
    }

    /// <summary>
    /// P-27 corte 4: consume una aclaración de salida ya persistida antes de clasificar o evaluar el
    /// entrante. Las opciones son deliberadamente pequeñas y el segundo valor inválido devuelve el
    /// hilo al flujo de aporte sin retener una orden ambigua.
    /// </summary>
    private async Task<ResultadoSalidaPendiente> ResolverSalidaPendienteAsync(
        Campania campania,
        DominioConversacion conversacion,
        IntencionControlPendiente? pendiente,
        EstadoMaquinaConversacion estadoPrevio,
        string texto,
        NumeroWhatsApp numero,
        string? emisor,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        if (estadoPrevio != EstadoMaquinaConversacion.EsperandoConfirmacionSalida || pendiente is null)
        {
            return ResultadoSalidaPendiente.Continuar;
        }

        if (!ClasificacionIntencionControlEfectiva(campania))
        {
            await GuardarSalidaPendienteAsync(
                conversacion.AvanzarA(EstadoMaquinaConversacion.EsperandoRepregunta), cancellationToken);
            await EnviarAsync(
                conversacion,
                numero,
                await TextoGlobalAsync(conversacion, "respaldoAclaracionSalida", RespaldoAclaracionSalida, cancellationToken),
                TipoEnvioMensaje.Repregunta,
                emisor,
                ahora,
                cancellationToken);
            return ResultadoSalidaPendiente.Consumida;
        }

        var alias = (await ResolverDetectoresAsync(conversacion, cancellationToken)).PoliticaIntencionControl
            .Resolver(estadoPrevio, hayUnidadActiva: true, texto);
        if (alias is DecisionIntencionControl.FinalizarIdea or DecisionIntencionControl.FinalizarParticipacion)
        {
            return ResultadoSalidaPendiente.ConDecision(alias);
        }

        var opcion = texto.Trim();
        if (opcion == "1")
        {
            await GuardarSalidaPendienteAsync(
                conversacion.AvanzarA(EstadoMaquinaConversacion.EsperandoRepregunta), cancellationToken);
            await EnviarAsync(
                conversacion,
                numero,
                await TextoGlobalAsync(conversacion, "acuseAclaracionContinuar", "Perfecto, continuemos con esta idea.", cancellationToken),
                TipoEnvioMensaje.Repregunta,
                emisor,
                ahora,
                cancellationToken);
            return ResultadoSalidaPendiente.Consumida;
        }

        if (opcion == "2")
        {
            return ResultadoSalidaPendiente.ConDecision(DecisionIntencionControl.FinalizarIdea);
        }

        if (opcion == "3")
        {
            return ResultadoSalidaPendiente.ConDecision(DecisionIntencionControl.FinalizarParticipacion);
        }

        if (pendiente.IntentosInvalidos == 0)
        {
            var reintentando = conversacion
                .AvanzarA(EstadoMaquinaConversacion.EsperandoConfirmacionSalida)
                .ConIntencionControlPendiente(IntencionControlPendiente.Crear(1, pendiente.CreadoEn));
            await GuardarSalidaPendienteAsync(reintentando, cancellationToken);
            await EnviarAsync(
                reintentando,
                numero,
                await TextoGlobalAsync(reintentando, "menuAclaracionSalida", MenuAclaracionSalida, cancellationToken),
                TipoEnvioMensaje.Repregunta,
                emisor,
                ahora,
                cancellationToken);
            return ResultadoSalidaPendiente.Consumida;
        }

        await GuardarSalidaPendienteAsync(
            conversacion.AvanzarA(EstadoMaquinaConversacion.EsperandoRepregunta), cancellationToken);
        await EnviarAsync(
            conversacion,
            numero,
            await TextoGlobalAsync(conversacion, "respaldoAclaracionSalida", RespaldoAclaracionSalida, cancellationToken),
            TipoEnvioMensaje.Repregunta,
            emisor,
            ahora,
            cancellationToken);
        return ResultadoSalidaPendiente.Consumida;
    }

    private bool ClasificacionIntencionControlEfectiva(Campania campania)
        => _clasificacionIntencionControlHabilitada && campania.ConfigConversacional.ClasificacionIntencionControl;

    private Task GuardarSalidaPendienteAsync(DominioConversacion conversacion, CancellationToken cancellationToken)
        => _conversaciones.GuardarConversacionAsync(conversacion, cancellationToken);

    private async Task AbrirAclaracionSalidaAsync(
        DominioConversacion conversacion,
        NumeroWhatsApp numero,
        string? emisor,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        var esperando = conversacion
            .AvanzarA(EstadoMaquinaConversacion.EsperandoConfirmacionSalida)
            .ConIntencionControlPendiente(IntencionControlPendiente.Crear(0, ahora));
        await GuardarSalidaPendienteAsync(esperando, cancellationToken);
        await EnviarAsync(
            esperando,
            numero,
            await TextoGlobalAsync(esperando, "menuAclaracionSalida", MenuAclaracionSalida, cancellationToken),
            TipoEnvioMensaje.Repregunta,
            emisor,
            ahora,
            cancellationToken);
    }

    private async Task<bool> EjecutarControlSimpleAsync(
        DecisionIntencionControl decision,
        DominioConversacion conversacion,
        Campania campania,
        Usuario usuario,
        Pregunta pregunta,
        NumeroWhatsApp numero,
        string? emisor,
        IdeaConsolidada idea,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        if (decision == DecisionIntencionControl.Ambigua)
        {
            await AbrirAclaracionSalidaAsync(conversacion, numero, emisor, ahora, cancellationToken);
            return true;
        }

        if (decision is not (DecisionIntencionControl.FinalizarIdea or DecisionIntencionControl.FinalizarParticipacion))
        {
            return false;
        }

        if (decision == DecisionIntencionControl.FinalizarParticipacion)
        {
            var abiertas = (await _respuestas.ListarIdeasConsolidadasAsync(campania.Id, cancellationToken))
                .Where(candidata => candidata.ConversacionId == conversacion.Id
                    && candidata.EstadoFlujo != EstadoFlujoIdeaConsolidada.Cerrada)
                .ToArray();
            foreach (var abierta in abiertas)
            {
                await CerrarIdeaPorControlAsync(abierta, campania, usuario, "finParticipacion", ahora, cancellationToken);
            }

            await CerrarConAgradecimientoAsync(conversacion, numero, campania, null, emisor, ahora, cancellationToken);
            return true;
        }

        await CerrarIdeaPorControlAsync(idea, campania, usuario, "participante", ahora, cancellationToken);
        var acuse = await SeleccionarAcuseContinuarAsync(conversacion, cancellationToken);
        if (await ContinuarConIdeaEnEsperaAsync(
                conversacion, campania, usuario, pregunta, numero, emisor, acuse, ahora,
                cancellationToken))
        {
            return true;
        }

        await CerrarConAgradecimientoAsync(
            conversacion, numero, campania, acuse, emisor, ahora, cancellationToken);
        await EnviarSiguientePreguntaPendienteAsync(campania, usuario, pregunta, numero, emisor, ahora, cancellationToken);
        return true;
    }

    private async Task<bool> EjecutarControlColaAsync(
        DecisionIntencionControl decision,
        DominioConversacion conversacion,
        Campania campania,
        Usuario usuario,
        Pregunta pregunta,
        NumeroWhatsApp numero,
        string? emisor,
        IdeaConsolidada? idea,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        if (decision == DecisionIntencionControl.Ambigua)
        {
            await AbrirAclaracionSalidaAsync(conversacion, numero, emisor, ahora, cancellationToken);
            return true;
        }

        if (decision is not (DecisionIntencionControl.FinalizarIdea or DecisionIntencionControl.FinalizarParticipacion))
        {
            return false;
        }

        var cola = conversacion.CoachingIdeas!;
        if (decision == DecisionIntencionControl.FinalizarParticipacion)
        {
            var abiertas = (await _respuestas.ListarIdeasConsolidadasAsync(campania.Id, cancellationToken))
                .Where(candidata => candidata.ConversacionId == conversacion.Id
                    && candidata.EstadoFlujo != EstadoFlujoIdeaConsolidada.Cerrada)
                .ToArray();
            foreach (var abierta in abiertas)
            {
                await CerrarIdeaPorControlAsync(abierta, campania, usuario, "finParticipacion", ahora, cancellationToken);
            }

            cola = _colaCoaching.FinalizarTodasAbiertas(cola, MotivoFinalizacionIdea.FinParticipacion, ahora);
            conversacion = conversacion.ConCoachingIdeas(cola);
            await RegistrarCoachingAsync(
                usuario.Id, usuario.WhatsappNormalizado, "finalizada", cola, MotivoFinalizacionIdea.FinParticipacion,
                ahora, cancellationToken);
            await CerrarConAgradecimientoAsync(conversacion, numero, campania, null, emisor, ahora, cancellationToken);
            return true;
        }

        if (idea is not null)
        {
            await CerrarIdeaPorControlAsync(idea, campania, usuario, "participante", ahora, cancellationToken);
        }

        cola = _colaCoaching.FinalizarActiva(cola, MotivoFinalizacionIdea.Participante, ahora);
        conversacion = conversacion.ConCoachingIdeas(cola);
        await RegistrarCoachingAsync(
            usuario.Id, usuario.WhatsappNormalizado, "avance", cola, MotivoFinalizacionIdea.Participante,
            ahora, cancellationToken);
        var acuse = await SeleccionarAcuseContinuarAsync(conversacion, cancellationToken);
        if (idea is not null)
        {
            await ContinuarColaConsolidadaAsync(
                conversacion, campania, usuario, pregunta, numero, emisor, acuse, ahora,
                cancellationToken);
        }
        else
        {
            await ContinuarOFinalizarColaAsync(
                conversacion, campania, usuario, pregunta, numero, emisor, acuse, ahora,
                cancellationToken);
        }

        return true;
    }

    private async Task CerrarIdeaPorControlAsync(
        IdeaConsolidada idea,
        Campania campania,
        Usuario usuario,
        string motivo,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        if (idea.EstadoFlujo == EstadoFlujoIdeaConsolidada.Cerrada)
        {
            return;
        }

        var cerrada = idea.Cerrar(EstadoResultadoIdeaConsolidada.Pendiente, null, motivo, ahora);
        await _respuestas.GuardarIdeaConsolidadaAsync(cerrada, cancellationToken);
        await RegistrarConsolidacionAsync(usuario, "cerrada", cerrada, null, motivo, null, ahora, cancellationToken);
    }

    private sealed record ResultadoSalidaPendiente(bool Manejado, DecisionIntencionControl? Decision)
    {
        public static ResultadoSalidaPendiente Continuar { get; } = new(false, null);
        public static ResultadoSalidaPendiente Consumida { get; } = new(true, null);

        public static ResultadoSalidaPendiente ConDecision(DecisionIntencionControl decision)
            => new(false, decision);
    }

    /// <summary>
    /// I-19 §4.6 sin cola I-18: si queda una idea esperando turno, pide su confirmación (con el acuse o
    /// la retroalimentación anterior como prefijo) y mantiene el hilo abierto. Devuelve <c>false</c>
    /// cuando no hay ninguna y el llamador debe cerrar como siempre.
    /// </summary>
    private async Task<bool> ContinuarConIdeaEnEsperaAsync(
        DominioConversacion conversacion,
        Campania campania,
        Usuario usuario,
        Pregunta pregunta,
        NumeroWhatsApp numero,
        string? emisor,
        string? prefijo,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        var siguiente = await ObtenerIdeaActivaAsync(campania.Id, conversacion.Id, cancellationToken);
        var propuesta = siguiente is null
            ? null
            : await ObtenerVersionAsync(campania.Id, siguiente.VersionPropuestaRef, cancellationToken);
        if (propuesta is null)
        {
            return false;
        }

        if (!_confirmacionExplicitaIdeasHabilitada)
        {
            await ConfirmarOCorregirIdeaAsync(
                conversacion, campania, usuario, pregunta, numero, emisor, siguiente!,
                texto: string.Empty, ahora, cancellationToken, confirmacionAutomatica: true);
            return true;
        }

        await EnviarAsync(
            conversacion, numero, Combinar(prefijo, TextoConfirmacion(propuesta.Texto)),
            TipoEnvioMensaje.Repregunta, emisor, ahora, cancellationToken);
        await _conversaciones.GuardarConversacionAsync(
            conversacion.AvanzarA(EstadoMaquinaConversacion.EsperandoRepregunta), cancellationToken);
        return true;
    }

    private async Task CrearPropuestaInicialAsync(
        DominioConversacion conversacion, Campania campania, Usuario usuario, Pregunta pregunta,
        NumeroWhatsApp numero, string? emisor, string texto, DateTimeOffset ahora, CancellationToken cancellationToken)
    {
        var respuestaId = "resp_" + Guid.NewGuid().ToString("N");
        var ideaId = "idea_" + respuestaId;
        await _procesador.GuardarRespuestaAsync(
            respuestaId, campania.Id, usuario, pregunta, conversacion.Id, texto, false, EstadoRespuesta.Recibida,
            ahora, cancellationToken, ideaId: ideaId, tipoAporte: TipoAporteIdea.Inicial);

        var contexto = await ConstruirContextoAsync(campania, pregunta, usuario, conversacion.Id, respuestaId, texto, cancellationToken);
        if (contexto.Contexto is null)
        {
            await RegistrarConfiguracionNoDisponibleAsync(usuario, contexto.Motivo ?? "configuracion_no_disponible", ahora, cancellationToken);
            await CerrarPorConfiguracionNoDisponibleAsync(conversacion, numero, emisor, ahora, cancellationToken);
            return;
        }

        var idea = IdeaConsolidada.Crear(ideaId, campania.Id, usuario.Id, pregunta.Id, conversacion.Id, respuestaId, 1, ahora);
        var propuesta = await ProponerVersionAsync(
            campania, pregunta, conversacion.Idioma, contexto.Contexto.ConfigLlmSnapshot, idea, versionVigente: null,
            respuestaId, texto, TipoAporteIdea.Inicial, ahora, cancellationToken);
        if (propuesta.PreguntaAclaracion is not null)
        {
            await PedirAclaracionAsync(
                conversacion, campania, pregunta, usuario, numero, emisor, idea, propuesta, ahora,
                cancellationToken);
            return;
        }

        await _respuestas.GuardarVersionIdeaAsync(propuesta.Version, cancellationToken);
        idea = idea.ConPropuesta(propuesta.Version.Id, ahora);
        await _respuestas.GuardarIdeaConsolidadaAsync(idea, cancellationToken);
        await RegistrarPropuestaAsync(usuario, idea, propuesta, "propuesta", ahora, cancellationToken);
        if (!_confirmacionExplicitaIdeasHabilitada)
        {
            await ConfirmarOCorregirIdeaAsync(
                conversacion, campania, usuario, pregunta, numero, emisor, idea,
                texto: string.Empty, ahora, cancellationToken, confirmacionAutomatica: true);
            return;
        }

        await EnviarConfirmacionAsync(
            conversacion, campania, usuario, pregunta, numero, emisor, propuesta.Version.Texto, ahora,
            cancellationToken);
    }

    private async Task ConfirmarOCorregirIdeaAsync(
        DominioConversacion conversacion, Campania campania, Usuario usuario, Pregunta pregunta,
        NumeroWhatsApp numero, string? emisor, IdeaConsolidada idea, string texto, DateTimeOffset ahora,
        CancellationToken cancellationToken, bool confirmacionAutomatica = false, bool confirmacionSemantica = false)
    {
        var detectores = await ResolverDetectoresAsync(conversacion, cancellationToken);
        if (!confirmacionAutomatica && detectores.RechazoIdea.Coincide(texto))
        {
            var rechazada = idea.Cerrar(EstadoResultadoIdeaConsolidada.Rechazada, null, "rechazoParticipante", ahora);
            await _respuestas.GuardarIdeaConsolidadaAsync(rechazada, cancellationToken);
            await RegistrarConsolidacionAsync(
                usuario, "cerrada", rechazada, null, "rechazoParticipante", null, ahora, cancellationToken);
            await _procesador.CompilarMarkdownIdeaAsync(campania.Id, idea.Id, cancellationToken);
            var acuseRechazo = await TextoGlobalAsync(
                conversacion,
                "acuseRechazoGuardado",
                TextoConfigurado(_mensajes.AcuseRechazoGuardado, OpcionesMensajesConversacion.AcuseRechazoGuardadoDefault),
                cancellationToken);
            // §4.5: el rechazo cierra solo esta idea; si otra espera turno, se sigue con ella.
            if (await ContinuarConIdeaEnEsperaAsync(
                    conversacion, campania, usuario, pregunta, numero, emisor, acuseRechazo, ahora,
                    cancellationToken))
            {
                return;
            }

            await EnviarAsync(conversacion, numero, acuseRechazo, TipoEnvioMensaje.Cierre, emisor, ahora, cancellationToken);
            await _conversaciones.GuardarConversacionAsync(conversacion.Cerrar(ahora), cancellationToken);
            return;
        }

        var confirmacionExplicita = confirmacionAutomatica || confirmacionSemantica || detectores.Confirmacion.Coincide(texto);
        var confirmacionImplicitaMejora = !confirmacionAutomatica && detectores.SolicitarMejora.Coincide(texto);
        if (!confirmacionExplicita && !confirmacionImplicitaMejora)
        {
            await CrearPropuestaComplementariaAsync(conversacion, campania, usuario, pregunta, numero, emisor, idea, texto, ahora, cancellationToken);
            return;
        }

        var version = await _respuestas.ObtenerVersionIdeaAsync(campania.Id, idea.VersionPropuestaRef!, cancellationToken);
        if (version is null)
        {
            await EnviarAsync(conversacion, numero, EvaluadorLlm.RetroNeutra, TipoEnvioMensaje.Repregunta, emisor, ahora, cancellationToken);
            return;
        }

        version = version.Confirmar(ahora);
        idea = idea.ConfirmarVersion(version.Id, ahora);
        await _respuestas.GuardarVersionIdeaAsync(version, cancellationToken);
        await _respuestas.GuardarIdeaConsolidadaAsync(idea, cancellationToken);
        await RegistrarConsolidacionAsync(
            usuario,
            confirmacionAutomatica
                ? "confirmadaAutomatica"
                : confirmacionImplicitaMejora
                    ? "confirmadaImplicitaMejora"
                    : "confirmada",
            idea,
            version.NumeroVersion, null, null, ahora, cancellationToken);

        var contexto = await ConstruirContextoAsync(campania, pregunta, usuario, conversacion.Id, idea.RespuestaRaizId, version.Texto, cancellationToken);
        if (contexto.Contexto is null)
        {
            var sinConfiguracion = idea.Cerrar(EstadoResultadoIdeaConsolidada.Pendiente, null, "configuracionNoDisponible", ahora);
            await _respuestas.GuardarIdeaConsolidadaAsync(sinConfiguracion, cancellationToken);
            await RegistrarConsolidacionAsync(
                usuario, "cerrada", sinConfiguracion, null, "configuracionNoDisponible", null, ahora, cancellationToken);
            await CerrarPorConfiguracionNoDisponibleAsync(conversacion, numero, emisor, ahora, cancellationToken);
            return;
        }

        var contextoEvaluacion = contexto.Contexto with
        {
            IdeaId = idea.Id,
            VersionIdeaId = version.Id,
            SolicitarParafraseo = false,
            RespuestaTexto = version.Texto,
        };
        var resultado = ConProcedenciaIdea(
            await _evaluador.EvaluarAsync(contextoEvaluacion, cancellationToken), idea.Id, version.Id);
        await _respuestas.GuardarEvaluacionAsync(resultado.Evaluacion, cancellationToken);
        await RegistrarConsolidacionAsync(
            usuario,
            resultado is ResultadoEvaluacion.Fallback ? "fallback" : "evaluada",
            idea,
            version.NumeroVersion,
            resultado is ResultadoEvaluacion.Fallback fallidaSimple ? fallidaSimple.Motivo : null,
            resultado.Evaluacion.UsoTokens,
            ahora,
            cancellationToken);

        var madura = resultado is not ResultadoEvaluacion.Fallback
            && _limites.UmbralAlcanzado(resultado.Evaluacion.CalificacionTotal, contexto.Contexto.RubricaSnapshot.Escala,
                _limites.ResolverUmbralBase(campania, pregunta));
        var conforme = !confirmacionAutomatica && confirmacionExplicita && detectores.Transicion.Interpretar(
            EstadoMaquinaConversacion.EsperandoRepregunta, 0, pregunta.MaxRepreguntas, texto).DeseaContinuar;

        if (madura || conforme || resultado is ResultadoEvaluacion.Fallback || pregunta.MaxRepreguntas <= 0)
        {
            var estado = madura ? EstadoResultadoIdeaConsolidada.Madura : EstadoResultadoIdeaConsolidada.Pendiente;
            var motivo = madura ? "umbral" : conforme ? "participante" : resultado is ResultadoEvaluacion.Fallback ? "fallbackEvaluacion" : "sinRepreguntas";
            idea = idea.Cerrar(estado, resultado.Evaluacion.Id, motivo, ahora);
            await _respuestas.GuardarIdeaConsolidadaAsync(idea, cancellationToken);
            await RegistrarConsolidacionAsync(
                usuario, "cerrada", idea, version.NumeroVersion, motivo, null, ahora, cancellationToken);
            await _procesador.CompilarMarkdownIdeaAsync(campania.Id, idea.Id, cancellationToken);

            // I-19 §4.6: si una idea nueva quedó esperando su turno, se atiende ahora en lugar de
            // cerrar el hilo; el cierre solo llega cuando no queda ninguna idea abierta.
            if (await ContinuarConIdeaEnEsperaAsync(
                    conversacion, campania, usuario, pregunta, numero, emisor,
                    resultado.Evaluacion.RetroalimentacionEnviada, ahora, cancellationToken))
            {
                return;
            }

            var cierreCampania = await ResolverMensajeCierreAsync(
                conversacion, campania, numero, emisor, "cierreIdeaConsolidada", ahora, cancellationToken);
            if (cierreCampania is null)
            {
                return;
            }

            var cierre = Combinar(resultado.Evaluacion.RetroalimentacionEnviada, cierreCampania);
            await EnviarAsync(conversacion, numero, cierre, TipoEnvioMensaje.Cierre, emisor, ahora, cancellationToken);
            await _conversaciones.GuardarConversacionAsync(conversacion.Cerrar(ahora), cancellationToken);
            await EnviarSiguientePreguntaPendienteAsync(campania, usuario, pregunta, numero, emisor, ahora, cancellationToken);
            return;
        }

        // La idea sigue abierta pero su versión confirmada ya fue evaluada: el artefacto se regenera.
        await _procesador.CompilarMarkdownIdeaAsync(campania.Id, idea.Id, cancellationToken);
        var invitacion = await ConstruirInvitacionMejoraAsync(
            conversacion,
            resultado.Evaluacion.RepreguntaSugerida,
            cancellationToken);
        var preguntaCoaching = string.IsNullOrWhiteSpace(resultado.Evaluacion.RepreguntaSugerida)
            ? EvaluadorLlm.RepreguntaNeutra
            : resultado.Evaluacion.RepreguntaSugerida.Trim();
        var umbralResumen = _limites.ResolverUmbralResumen(campania, pregunta);
        var superaUmbralResumen = resultado is not ResultadoEvaluacion.Fallback
            && _limites.UmbralAlcanzado(
                resultado.Evaluacion.CalificacionTotal,
                contexto.Contexto.RubricaSnapshot.Escala,
                umbralResumen);
        var enviarResumen = superaUmbralResumen && idea.ResumenEnviadoEn is null;
        if (umbralResumen > 0 && resultado is ResultadoEvaluacion.Fallback)
        {
            await RegistrarResumenConsolidacionAsync(
                usuario, idea, version.NumeroVersion, umbralResumen, campania, pregunta,
                resultado.Evaluacion.CalificacionTotal, contexto.Contexto.RubricaSnapshot.Escala,
                "omitidoFallback", ahora, cancellationToken);
        }
        else if (superaUmbralResumen && !enviarResumen)
        {
            await RegistrarResumenConsolidacionAsync(
                usuario, idea, version.NumeroVersion, umbralResumen, campania, pregunta,
                resultado.Evaluacion.CalificacionTotal, contexto.Contexto.RubricaSnapshot.Escala,
                "omitidoYaEnviado", ahora, cancellationToken);
        }
        var encabezadoResumen = await TextoGlobalAsync(
            conversacion,
            "encabezadoResumenAvance",
            TextoConfigurado(_mensajes.EncabezadoResumenAvance, OpcionesMensajesConversacion.EncabezadoResumenAvanceDefault),
            cancellationToken);
        var preguntaResumen = await TextoGlobalAsync(
            conversacion,
            "preguntaContinuarMadurando",
            TextoConfigurado(_mensajes.PreguntaContinuarMadurando, OpcionesMensajesConversacion.PreguntaContinuarMadurandoDefault),
            cancellationToken);
        var turnoCoaching = await ComponerTurnoAsync(
            campania, pregunta, usuario.Id, usuario.WhatsappNormalizado,
            enviarResumen ? ActoConversacional.ResumirAvance : ActoConversacional.Mejorar,
            respaldo: enviarResumen
                ? Combinar(Combinar(Combinar(resultado.Evaluacion.RetroalimentacionEnviada, encabezadoResumen), version.Texto), preguntaResumen)
                : CombinarSinDuplicar(resultado.Evaluacion.RetroalimentacionEnviada, invitacion),
            ahora, cancellationToken,
            cuerpo: enviarResumen ? Combinar(Combinar(resultado.Evaluacion.RetroalimentacionEnviada, encabezadoResumen), version.Texto) : resultado.Evaluacion.RetroalimentacionEnviada,
            versionCompleta: enviarResumen ? version.Texto : null,
            retroalimentacionValidada: resultado.Evaluacion.RetroalimentacionEnviada,
            preguntaAprobada: enviarResumen ? preguntaResumen : preguntaCoaching,
            idioma: conversacion.Idioma);
        if (enviarResumen)
        {
            idea = idea.ConResumenEnviado(version.NumeroVersion, ahora);
            await _respuestas.GuardarIdeaConsolidadaAsync(idea, cancellationToken);
            await RegistrarResumenConsolidacionAsync(
                usuario, idea, version.NumeroVersion, umbralResumen, campania, pregunta,
                resultado.Evaluacion.CalificacionTotal, contexto.Contexto.RubricaSnapshot.Escala,
                "enviado", ahora, cancellationToken);
        }
        await EnviarAsync(
            conversacion, numero, turnoCoaching, TipoEnvioMensaje.Repregunta, emisor, ahora,
            cancellationToken);
        await _conversaciones.GuardarConversacionAsync(conversacion.RegistrarRepregunta(), cancellationToken);
    }

    /// <summary>P-31: trazabilidad del resumen sin aporte, texto consolidado ni texto redactado.</summary>
    private Task RegistrarResumenConsolidacionAsync(
        Usuario usuario,
        IdeaConsolidada idea,
        int numeroVersion,
        double umbral,
        Campania campania,
        Pregunta pregunta,
        decimal score,
        EscalaRubrica escala,
        string accion,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
        => _logSeguridad.RegistrarAsync(
            LogSeguridad.Crear(
                "log_" + Guid.NewGuid().ToString("N"),
                TipoEventoSeguridad.ResumenConsolidacion,
                usuario.Id,
                usuario.WhatsappNormalizado.Valor,
                accion,
                FormattableString.Invariant(
                    $"accion:{accion};idea:{idea.Id};version:{numeroVersion};umbral:{umbral};origen:{_limites.OrigenUmbralResumen(campania, pregunta)};score:{score};escalaMin:{escala.Min};escalaMax:{escala.Max}"),
                _correlacion.CorrelationIdActual,
                ahora,
                campaniaId: campania.Id),
            cancellationToken);

    private async Task CrearPropuestaComplementariaAsync(
        DominioConversacion conversacion, Campania campania, Usuario usuario, Pregunta pregunta,
        NumeroWhatsApp numero, string? emisor, IdeaConsolidada idea, string texto, DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        var respuestaId = "resp_" + Guid.NewGuid().ToString("N");
        await _procesador.GuardarRespuestaAsync(
            respuestaId, campania.Id, usuario, pregunta, conversacion.Id, texto, true, EstadoRespuesta.Recibida,
            ahora, cancellationToken, ideaId: idea.Id, tipoAporte: TipoAporteIdea.Complemento);
        var contexto = await ConstruirContextoAsync(campania, pregunta, usuario, conversacion.Id, respuestaId, texto, cancellationToken);
        if (contexto.Contexto is null)
        {
            await _respuestas.GuardarIdeaConsolidadaAsync(idea.Cerrar(EstadoResultadoIdeaConsolidada.Pendiente, null, "configuracionNoDisponible", ahora), cancellationToken);
            await CerrarPorConfiguracionNoDisponibleAsync(conversacion, numero, emisor, ahora, cancellationToken);
            return;
        }

        var propuesta = await ProponerVersionAsync(
            campania, pregunta, conversacion.Idioma, contexto.Contexto.ConfigLlmSnapshot, idea,
            await ObtenerVersionVigenteAsync(campania.Id, idea, cancellationToken),
            respuestaId, texto, TipoAporteIdea.Complemento, ahora, cancellationToken);
        if (propuesta.PreguntaAclaracion is not null)
        {
            await PedirAclaracionAsync(
                conversacion, campania, pregunta, usuario, numero, emisor, idea, propuesta, ahora,
                cancellationToken);
            return;
        }

        await _respuestas.GuardarVersionIdeaAsync(propuesta.Version, cancellationToken);
        idea = idea.ConPropuesta(propuesta.Version.Id, ahora);
        await _respuestas.GuardarIdeaConsolidadaAsync(idea, cancellationToken);
        await RegistrarPropuestaAsync(usuario, idea, propuesta, "corregida", ahora, cancellationToken);

        // I-19 §4.6: una idea nueva explícita en el mismo mensaje no se mezcla con la activa; queda
        // registrada aparte y se trabaja después (aquí no hay cola I-18: la resuelve el orden de espera).
        await RegistrarIdeasNuevasSinColaAsync(
            conversacion, campania, usuario, pregunta, contexto.Contexto.ConfigLlmSnapshot,
            IdeasNuevasAdmisibles(propuesta.NuevasIdeas, texto, propuesta.Version.Texto), respuestaId,
            ahora, cancellationToken);
        if (!_confirmacionExplicitaIdeasHabilitada)
        {
            await ConfirmarOCorregirIdeaAsync(
                conversacion, campania, usuario, pregunta, numero, emisor, idea,
                texto: string.Empty, ahora, cancellationToken, confirmacionAutomatica: true);
            return;
        }

        await EnviarConfirmacionAsync(
            conversacion, campania, usuario, pregunta, numero, emisor, propuesta.Version.Texto, ahora,
            cancellationToken);
    }

    /// <summary>
    /// I-19 §4.6 sin cola I-18: cada idea nueva obtiene su propio <c>ideaId</c>, aporte y propuesta, y
    /// espera su turno. <see cref="ProcesarIdeaConsolidadaAsync"/> las atiende por orden de llegada, de
    /// modo que la idea activa se termina antes de empezar la siguiente.
    /// </summary>
    private async Task RegistrarIdeasNuevasSinColaAsync(
        DominioConversacion conversacion,
        Campania campania,
        Usuario usuario,
        Pregunta pregunta,
        ConfigLlm configLlm,
        IReadOnlyList<string> nuevasIdeas,
        string aporteOrigenId,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        if (nuevasIdeas.Count == 0)
        {
            return;
        }

        var existentes = (await _respuestas.ListarIdeasConsolidadasAsync(campania.Id, cancellationToken))
            .Where(candidata => candidata.ConversacionId == conversacion.Id)
            .ToArray();
        var indice = existentes.Length == 0 ? 0 : existentes.Max(candidata => candidata.IdeaIndice);
        var orden = 0;
        foreach (var texto in nuevasIdeas)
        {
            if (indice >= _maxIdeasPorMensaje)
            {
                return;
            }

            orden++;
            indice++;
            var respuestaId = $"{aporteOrigenId}_n{orden}";
            if (existentes.Any(candidata => candidata.RespuestaRaizId == respuestaId))
            {
                continue;
            }

            await CrearIdeaNuevaAsync(
                conversacion, campania, usuario, pregunta, configLlm, texto, respuestaId, indice,
                respuestaPadreId: null, ahora, cancellationToken);
        }
    }

    /// <summary>
    /// Crea el aporte inmutable, la idea y la primera versión propuesta de una idea nueva detectada
    /// durante el coaching (I-19 §4.6). No envía nada: la idea espera su turno en silencio.
    /// </summary>
    private async Task<VersionIdeaConsolidada> CrearIdeaNuevaAsync(
        DominioConversacion conversacion,
        Campania campania,
        Usuario usuario,
        Pregunta pregunta,
        ConfigLlm configLlm,
        string texto,
        string respuestaId,
        int ideaIndice,
        string? respuestaPadreId,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        var ideaId = "idea_" + respuestaId;
        await _procesador.GuardarRespuestaAsync(
            respuestaId,
            campania.Id,
            usuario,
            pregunta,
            conversacion.Id,
            texto,
            esRepregunta: true,
            EstadoRespuesta.Recibida,
            ahora,
            cancellationToken,
            respuestaPadreId is null ? null : ideaIndice,
            respuestaPadreId,
            NivelMadurez.Incubacion,
            respuestaId,
            null,
            0,
            ideaId,
            TipoAporteIdea.NuevaIdea);

        var idea = IdeaConsolidada.Crear(
            ideaId, campania.Id, usuario.Id, pregunta.Id, conversacion.Id, respuestaId, ideaIndice, ahora);
        var propuesta = await ProponerVersionAsync(
            campania, pregunta, conversacion.Idioma, configLlm, idea, versionVigente: null, respuestaId, texto,
            TipoAporteIdea.NuevaIdea, ahora, cancellationToken);
        await _respuestas.GuardarVersionIdeaAsync(propuesta.Version, cancellationToken);
        idea = idea.ConPropuesta(propuesta.Version.Id, ahora);
        await _respuestas.GuardarIdeaConsolidadaAsync(idea, cancellationToken);
        await RegistrarPropuestaAsync(usuario, idea, propuesta, "propuesta", ahora, cancellationToken);
        return propuesta.Version;
    }

    /// <summary>
    /// I-19 §4.2: toda propuesta nueva acumula sobre la versión vigente de la idea —la confirmada si
    /// existe y, mientras no la haya, la propuesta que el participante está corrigiendo—. Así una
    /// corrección previa a la primera confirmación conserva el aporte original y encadena la versión.
    /// </summary>
    private async Task<PropuestaConsolidada> ProponerVersionAsync(
        Campania campania, Pregunta pregunta, string idioma, ConfigLlm configLlm, IdeaConsolidada idea,
        VersionIdeaConsolidada? versionVigente, string aporteId, string aporteTexto,
        TipoAporteIdea tipoFallback, DateTimeOffset ahora, CancellationToken cancellationToken)
    {
        if (!IdiomaConversacion.TryCrear(idioma, out var idiomaConversacion))
        {
            idiomaConversacion = IdiomaConversacion.Espanol;
        }

        var contenido = ResolverContenidoCampania(campania, idiomaConversacion, preguntaId: pregunta.Id);
        if (contenido is null || !contenido.Preguntas.TryGetValue(pregunta.Id, out var preguntaEfectiva))
        {
            var numeroFallback = (versionVigente?.NumeroVersion ?? 0) + 1;
            var versionFallback = VersionIdeaConsolidada.Crear(
                $"{idea.Id}_v{numeroFallback}", campania.Id, idea.Id, numeroFallback, versionVigente?.Id,
                aporteTexto,
                (versionVigente?.AporteIdsAcumulados ?? Array.Empty<string>()).Append(aporteId).ToArray(),
                new[] { aporteId }, tipoFallback, EstadoConfirmacionVersionIdea.Propuesta, null, null, null,
                CrearSnapshotConfig(configLlm), ahora);
            return new PropuestaConsolidada(versionFallback, Array.Empty<NuevaIdeaDetectada>(), true, null, null);
        }

        var propuesta = await _consolidadorIdeas!.ConsolidarAsync(
            new ContextoConsolidacionIdeas(
                campania, pregunta, versionVigente?.Texto, aporteTexto, configLlm,
                _maxCaracteresIdeaConsolidada, _maxIdeasPorMensaje)
            {
                Idioma = contenido.Idioma.Codigo,
                TextoPreguntaEfectivo = preguntaEfectiva.Texto,
            },
            cancellationToken);
        var (textoPropuesto, tipo) = TextoYTipoPropuesta(propuesta, aporteTexto, tipoFallback);
        var numeroVersion = (versionVigente?.NumeroVersion ?? 0) + 1;
        var version = VersionIdeaConsolidada.Crear(
            $"{idea.Id}_v{numeroVersion}", campania.Id, idea.Id, numeroVersion, versionVigente?.Id,
            textoPropuesto,
            (versionVigente?.AporteIdsAcumulados ?? Array.Empty<string>()).Append(aporteId).ToArray(),
            new[] { aporteId }, tipo, EstadoConfirmacionVersionIdea.Propuesta, null, null, null,
            CrearSnapshotConfig(configLlm), ahora);
        var nuevas = propuesta is ResultadoConsolidacionIdeas.Exito exito
            ? exito.NuevasIdeas
            : Array.Empty<NuevaIdeaDetectada>();
        // §4.2: una salida que se declara ambigua no se usa para adivinar; el llamador pide la
        // aclaración breve y no crea versión nueva.
        var aclaracion = propuesta is ResultadoConsolidacionIdeas.Exito { RequiereAclaracion: true } ambigua
            ? ambigua.PreguntaAclaracion
            : null;
        return new PropuestaConsolidada(
            version, nuevas, propuesta is ResultadoConsolidacionIdeas.Fallback, propuesta.Uso, aclaracion);
    }

    /// <summary>
    /// I-19 §4.2: pide una aclaración breve en vez de adivinar. El aporte ya quedó guardado; la idea
    /// conserva su estado y su versión vigente, así que el ciclo sigue en el próximo mensaje.
    /// </summary>
    private async Task PedirAclaracionAsync(
        DominioConversacion conversacion,
        Campania campania,
        Pregunta pregunta,
        Usuario usuario,
        NumeroWhatsApp numero,
        string? emisor,
        IdeaConsolidada idea,
        PropuestaConsolidada propuesta,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        // I-20: la aclaración también se redacta; el respaldo es la pregunta cruda del consolidador.
        var texto = await ComponerTurnoAsync(
            campania, pregunta, usuario.Id, usuario.WhatsappNormalizado, ActoConversacional.Aclarar,
            respaldo: propuesta.PreguntaAclaracion!, ahora, cancellationToken, idioma: conversacion.Idioma);
        await EnviarAsync(
            conversacion, numero, texto, TipoEnvioMensaje.Repregunta, emisor, ahora, cancellationToken);
        await _conversaciones.GuardarConversacionAsync(
            conversacion.AvanzarA(EstadoMaquinaConversacion.EsperandoRepregunta), cancellationToken);
        await RegistrarConsolidacionAsync(
            usuario, "aclaracion", idea, null, "ambiguedad", propuesta.Uso, ahora, cancellationToken);
    }

    /// <summary>
    /// I-19 §12.2: telemetría de una transición de idea. Nunca incluye el aporte ni la paráfrasis: solo
    /// acción, índice de idea, número de versión, estados, motivo y tokens de la consolidación.
    /// </summary>
    private Task RegistrarConsolidacionAsync(
        Usuario usuario,
        string accion,
        IdeaConsolidada idea,
        int? numeroVersion,
        string? motivo,
        UsoTokensLlm? uso,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        var detalle = FormattableString.Invariant(
            $"accion:{accion};ideaIndice:{idea.IdeaIndice};version:{numeroVersion ?? 0};estado:{MinusculaInicial(idea.EstadoFlujo.ToString())};resultado:{(idea.EstadoResultado is null ? "ninguno" : MinusculaInicial(idea.EstadoResultado.Value.ToString()))};motivo:{motivo ?? "ninguno"};promptTokens:{uso?.PromptTokens ?? 0};completionTokens:{uso?.CompletionTokens ?? 0}");
        return _logSeguridad.RegistrarAsync(
            LogSeguridad.Crear(
                "log_" + Guid.NewGuid().ToString("N"),
                TipoEventoSeguridad.ConsolidacionProgresivaIdeas,
                usuario.Id,
                usuario.WhatsappNormalizado.Valor,
                accion,
                detalle,
                _correlacion.CorrelationIdActual,
                ahora),
            cancellationToken);
    }

    /// <summary>Registra la propuesta recién guardada, distinguiendo la que degradó a fallback.</summary>
    private Task RegistrarPropuestaAsync(
        Usuario usuario,
        IdeaConsolidada idea,
        PropuestaConsolidada propuesta,
        string accion,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
        => RegistrarConsolidacionAsync(
            usuario,
            propuesta.EsFallback ? "fallback" : accion,
            idea,
            propuesta.Version.NumeroVersion,
            propuesta.EsFallback ? "consolidacionFallback" : null,
            propuesta.Uso,
            ahora,
            cancellationToken);

    /// <summary>
    /// I-19 §4.6: filtra las ideas nuevas que el consolidador propone junto al complemento. El servidor
    /// descarta fragmentos, repeticiones del propio aporte y duplicados antes de encolar nada.
    /// </summary>
    private IReadOnlyList<string> IdeasNuevasAdmisibles(
        IReadOnlyList<NuevaIdeaDetectada> nuevasIdeas, string aporteTexto, string versionActivaTexto)
    {
        var vistos = new HashSet<string>(StringComparer.Ordinal)
        {
            NormalizarTextoIdea(aporteTexto),
            NormalizarTextoIdea(versionActivaTexto),
        };
        return nuevasIdeas
            .Select(nueva => nueva.Texto.Trim())
            .Where(texto => texto.Length >= _longitudMinimaIdea)
            .Where(texto => vistos.Add(NormalizarTextoIdea(texto)))
            .ToArray();
    }

    private async Task<VersionIdeaConsolidada?> ObtenerVersionVigenteAsync(
        string campaniaId, IdeaConsolidada idea, CancellationToken cancellationToken)
        => await ObtenerVersionAsync(campaniaId, idea.VersionConfirmadaRef, cancellationToken)
            ?? await ObtenerVersionAsync(campaniaId, idea.VersionPropuestaRef, cancellationToken);

    private async Task<VersionIdeaConsolidada?> ObtenerVersionAsync(
        string campaniaId, string? versionId, CancellationToken cancellationToken)
        => string.IsNullOrWhiteSpace(versionId)
            ? null
            : await _respuestas.ObtenerVersionIdeaAsync(campaniaId, versionId, cancellationToken);

    /// <summary>
    /// I-20 §3.1: el acto <c>Confirmar</c> se redacta —puente y pregunta contextuales— pero la versión
    /// propuesta la inserta el servidor entre ambos, íntegra. El respaldo es la frase de siempre.
    /// </summary>
    private async Task EnviarConfirmacionAsync(
        DominioConversacion conversacion, Campania campania, Usuario usuario, Pregunta pregunta,
        NumeroWhatsApp numero, string? emisor, string textoPropuesto, DateTimeOffset ahora,
        CancellationToken cancellationToken,
        string? prefijo = null)
    {
        var texto = await ComponerTurnoAsync(
            campania, pregunta, usuario.Id, usuario.WhatsappNormalizado, ActoConversacional.Confirmar,
            respaldo: TextoConfirmacion(textoPropuesto),
            ahora, cancellationToken,
            cuerpo: textoPropuesto,
            versionCompleta: textoPropuesto,
            idioma: conversacion.Idioma);
        await EnviarAsync(
            conversacion, numero, Combinar(prefijo, texto), TipoEnvioMensaje.Repregunta, emisor, ahora,
            cancellationToken);
    }

    /// <summary>
    /// I-20 §3/§4: compone el texto visible de un acto que el servidor **ya decidió**. Pide al redactor
    /// un puente y, si el acto la admite, una pregunta; el <b>cuerpo</b> (la versión consolidada, la
    /// retroalimentación validada) lo inserta este método, no el modelo.
    /// <para>
    /// Con el kill-switch apagado, sin redactor inyectado o ante cualquier <c>Fallback</c>, devuelve
    /// <paramref name="respaldo"/>, que es exactamente el texto determinista de siempre: el
    /// comportamiento actual se conserva sin tocar estados ni evaluación.
    /// </para>
    /// </summary>
    private async Task<string> ComponerTurnoAsync(
        Campania campania,
        Pregunta? pregunta,
        string usuarioId,
        NumeroWhatsApp numeroUsuario,
        ActoConversacional acto,
        string respaldo,
        DateTimeOffset ahora,
        CancellationToken cancellationToken,
        string? cuerpo = null,
        string? versionCompleta = null,
        string? retroalimentacionValidada = null,
        string? preguntaAprobada = null,
        Rubrica? rubrica = null,
        string? idioma = null)
    {
        if (_redactorTurno is null || !_redaccion.Habilitada || pregunta is null)
        {
            return respaldo;
        }

        // I-20 §4.1 / P-10: redactar también gasta LLM. Con el cupo agotado no se llama al modelo y el
        // turno sale con su respaldo determinista: el aporte y el estado no se tocan.
        if (_cuposHabilitados && await CupoLlamadasLlmExcedidoAsync(campania, usuarioId, ahora, cancellationToken))
        {
            return respaldo;
        }

        var promptRef = _redaccion.ResolverPromptRef(campania, pregunta, acto);
        // DT-I20-02 §5.4: la voz de I-20 tambien es un prompt versionado y aprobado (I-20 §5), asi que
        // runtime toma la version vigente mas nueva y no la ultima por numero. Sin ninguna vigente el
        // redactor conserva solo sus reglas duras, como cuando la campania no configura voz.
        var prompt = string.IsNullOrWhiteSpace(promptRef)
            ? null
            : (await _configuracion.ObtenerPromptVigenteAsync(promptRef, cancellationToken)).Prompt;
        var configLlm = string.IsNullOrWhiteSpace(campania.ConfigLlmRef)
            ? null
            : await _configuracion.ObtenerConfigLlmAsync(campania.ConfigLlmRef, cancellationToken);
        if (configLlm is null || configLlm.Estado != EstadoRegistro.Activo)
        {
            // Sin LLM utilizable no se degrada la conversación: se usa el texto de siempre.
            return respaldo;
        }

        if (!IdiomaConversacion.TryCrear(idioma ?? "es", out var idiomaConversacion))
        {
            return respaldo;
        }

        var contenido = ResolverContenidoCampania(campania, idiomaConversacion, preguntaId: pregunta.Id);
        if (contenido is null || !contenido.Preguntas.TryGetValue(pregunta.Id, out var preguntaEfectiva))
        {
            return respaldo;
        }

        var contexto = new ContextoRedaccionTurno(campania, pregunta, acto, configLlm, _redaccion.MaxCaracteres)
        {
            Idioma = contenido.Idioma.Codigo,
            NombreCampaniaEfectivo = contenido.Nombre,
            TextoPreguntaEfectivo = preguntaEfectiva.Texto,
            InstruccionPreguntaEfectiva = preguntaEfectiva.Instruccion,
            VersionCompleta = versionCompleta,
            RetroalimentacionValidada = retroalimentacionValidada,
            PreguntaAprobada = preguntaAprobada,
            // Ya viene filtrado por la resolucion de runtime (activo + aprobado).
            PromptSnapshot = prompt,
            RubricaSnapshot = rubrica,
        };

        var resultado = await _redactorTurno.RedactarAsync(contexto, cancellationToken);

        // El orden es siempre puente → cuerpo → pregunta: así la versión consolidada queda íntegra y
        // visible, y el modelo no puede sustituirla ni esconderla (§4). Un acto sin cuerpo ni pregunta
        // (P-28 `Reactivar`, P-29 `Pausar`) no deja el separador colgando.
        // DT-I20-01 §4.2: antes de unir, la guarda determinista descarta el puente que repita el cuerpo
        // validado; si la pregunta duplicada deja al acto sin su función, se cae al respaldo.
        var composicion = resultado is ResultadoRedaccionTurno.Exito redactado
            ? FiltroDuplicacionTurno.Componer(
                redactado.Puente,
                cuerpo,
                redactado.Pregunta,
                PoliticaRedaccionConversacional.ExigePregunta(acto))
            : null;

        await RegistrarRedaccionAsync(
            usuarioId, numeroUsuario, acto, resultado, _redaccion.UsaPromptDeVoz(campania, pregunta, acto),
            composicion?.Motivo, ahora, cancellationToken);

        return composicion is null || composicion.RequiereRespaldo ? respaldo : composicion.Texto;
    }

    /// <summary>
    /// I-20 (10 §6.2): una entrada por llamada al redactor. Sin el texto redactado ni el rechazado:
    /// solo acto, si se redactó o se usó el respaldo, el motivo técnico y los tokens de esa llamada.
    /// <para>
    /// DT-I20-01 §5: <paramref name="ajuste"/> añade el motivo no sensible de la guarda de duplicación
    /// (p. ej. <c>puente_duplicado_omitido</c>). Es un código fijo, nunca la frase omitida.
    /// </para>
    /// </summary>
    private Task RegistrarRedaccionAsync(
        string usuarioId,
        NumeroWhatsApp numeroUsuario,
        ActoConversacional acto,
        ResultadoRedaccionTurno resultado,
        bool promptDeVoz,
        string? ajuste,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        var motivo = resultado is ResultadoRedaccionTurno.Fallback fallback ? fallback.Motivo : "ninguno";
        var detalle = FormattableString.Invariant(
            $"accion:{MinusculaInicial(acto.ToString())};motivo:{motivo};ajuste:{ajuste ?? "ninguno"};promptVoz:{promptDeVoz};promptTokens:{resultado.Uso?.PromptTokens ?? 0};completionTokens:{resultado.Uso?.CompletionTokens ?? 0}");
        return _logSeguridad.RegistrarAsync(
            LogSeguridad.Crear(
                "log_" + Guid.NewGuid().ToString("N"),
                TipoEventoSeguridad.RedaccionConversacional,
                usuarioId,
                numeroUsuario.Valor,
                resultado is ResultadoRedaccionTurno.Exito ? "redactado" : "respaldo",
                detalle,
                _correlacion.CorrelationIdActual,
                ahora),
            cancellationToken);
    }

    private static string TextoConfirmacion(string textoPropuesto)
        => $"Entendí que propones: {textoPropuesto}\n\n¿Es correcto?";

    private static (string Texto, TipoAporteIdea Tipo) TextoYTipoPropuesta(
        ResultadoConsolidacionIdeas resultado, string aporte, TipoAporteIdea tipoFallback)
        => resultado switch
        {
            ResultadoConsolidacionIdeas.Exito exito => (exito.TextoConsolidado, exito.TipoCambio),
            ResultadoConsolidacionIdeas.Fallback fallback => (fallback.TextoConservador, tipoFallback),
            _ => (aporte, tipoFallback),
        };

    private static ConfigLlmSnapshot CrearSnapshotConfig(ConfigLlm config)
        => new(config.Proveedor, config.Modelo, config.Endpoint, config.Parametros);

    private async Task ProcesarIdeasSegmentadasAsync(
        DominioConversacion conversacion,
        Campania campania,
        Usuario usuario,
        Pregunta pregunta,
        NumeroWhatsApp numero,
        string? emisor,
        ContextoEvaluacion contextoBase,
        string textoOriginal,
        string respuestaPadreId,
        bool esRepregunta,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        var resolucion = await ResolverIdeasAsync(contextoBase, textoOriginal, cancellationToken);
        await RegistrarSegmentacionAsync(usuario, resolucion, ahora, cancellationToken);

        // I-19: ninguna raiz segmentada se evalua antes de que el participante confirme lo entendido.
        // La cola es el estado server-side de "una idea a la vez", no la funcion de coaching: con I-06
        // activo se usa aunque el gate de I-18 este apagado, y en ese caso no hay pregunta socratica.
        if (ConsolidacionIdeasActiva)
        {
            await IniciarColaConsolidadaAsync(
                conversacion, campania, usuario, pregunta, numero, emisor, contextoBase, resolucion,
                respuestaPadreId, esRepregunta, ahora, cancellationToken);
            return;
        }

        // I-17: el origen del umbral (pregunta → campaña → global) es constante por pregunta para todas
        // las ideas; se reusa en la telemetria del cierre por umbral mas abajo.
        var origenUmbral = _limites.OrigenUmbral(campania, pregunta);
        var coachingEfectivo = CoachingEfectivo(campania) && resolucion.FueSegmentada;

        var resultados = new List<(string RespuestaId, ResultadoEvaluacion Resultado, ContextoEvaluacion Contexto)>();
        foreach (var idea in resolucion.Ideas)
        {
            var respuestaId = resolucion.FueSegmentada
                ? CrearRespuestaIdIdea(respuestaPadreId, idea.Indice)
                : "resp_" + Guid.NewGuid().ToString("N");
            var contexto = contextoBase with
            {
                RespuestaId = respuestaId,
                RespuestaTexto = idea.Texto,
                CoachingSecuencialIdeas = coachingEfectivo,
            };
            var resultado = await _evaluador.EvaluarAsync(contexto, cancellationToken);

            // Mismos efectos posteriores que el flujo de una sola respuesta (P-15 Corte 3), por idea.
            await _procesador.PersistirRespuestaEvaluadaAsync(
                resultado, campania, pregunta, usuario, conversacion.Id, respuestaId, idea.Texto, esRepregunta,
                contexto.RubricaSnapshot.Escala, ahora, cancellationToken,
                resolucion.FueSegmentada ? idea.Indice : null,
                resolucion.FueSegmentada ? respuestaPadreId : null,
                coachingEfectivo ? respuestaId : null,
                respuestaAnteriorId: null,
                coachingEfectivo ? 0 : null);

            resultados.Add((respuestaId, resultado, contexto));
        }

        if (coachingEfectivo)
        {
            await IniciarCoachingSecuencialAsync(
                conversacion,
                campania,
                usuario,
                pregunta,
                numero,
                emisor,
                respuestaPadreId,
                resultados,
                ahora,
                cancellationToken);
            return;
        }

        if (resultados.Any(resultado => resultado.Resultado is ResultadoEvaluacion.Fallback))
        {
            await CerrarNeutroAsync(conversacion, numero, campania, emisor, ahora, cancellationToken);
            return;
        }

        var umbralCierre = _limites.ResolverUmbralCierreAnticipado(campania, pregunta);
        foreach (var resultado in resultados)
        {
            if (_limites.UmbralAlcanzado(
                    resultado.Resultado.Evaluacion.CalificacionTotal,
                    resultado.Contexto.RubricaSnapshot.Escala,
                    umbralCierre))
            {
                await _procesador.RegistrarCierreUmbralAsync(
                    usuario,
                    resultado.Resultado.Evaluacion.CalificacionTotal,
                    _limites.ValorUmbral(resultado.Contexto.RubricaSnapshot.Escala, umbralCierre),
                    resultado.Contexto.RubricaSnapshot.Escala,
                    umbralCierre,
                    origenUmbral,
                    ahora,
                    cancellationToken);
            }
        }

        // Una respuesta al participante por turno: las evaluaciones y Markdown quedan individualizados
        // para resultados, pero el hilo conserva su limite de repreguntas por pregunta.
        var calificacionAlta = resultados.All(resultado =>
            _limites.UmbralAlcanzado(
                resultado.Resultado.Evaluacion.CalificacionTotal,
                resultado.Contexto.RubricaSnapshot.Escala,
                umbralCierre));
        var confirmacion = ConfirmacionIdeas(resolucion.Ideas.Count);
        var ofrecerMejora = !calificacionAlta && _limites.PuedeOfrecerMejora(conversacion, pregunta);
        if (ofrecerMejora)
        {
            var texto = Combinar(
                confirmacion,
                await ConstruirInvitacionMejoraAsync(conversacion, repreguntaSugerida: null, cancellationToken));
            await EnviarAsync(conversacion, numero, texto, TipoEnvioMensaje.Repregunta, emisor, ahora, cancellationToken);
            conversacion = conversacion.RegistrarRepregunta();
            await _conversaciones.GuardarConversacionAsync(conversacion, cancellationToken);
            return;
        }

        var cierreCampania = await ResolverMensajeCierreAsync(
            conversacion, campania, numero, emisor, "cierreIdeasSegmentadas", ahora, cancellationToken);
        if (cierreCampania is null)
        {
            return;
        }

        var cierreFinal = calificacionAlta
            ? Combinar(
                await TextoGlobalAsync(
                    conversacion,
                    "mensajeCalificacionAlta",
                    TextoConfigurado(_mensajes.MensajeCalificacionAlta, OpcionesMensajesConversacion.MensajeCalificacionAltaDefault),
                    cancellationToken),
                cierreCampania)
            : cierreCampania;
        await EnviarAsync(conversacion, numero, Combinar(confirmacion, cierreFinal), TipoEnvioMensaje.Cierre, emisor, ahora, cancellationToken);

        conversacion = conversacion.Cerrar(ahora);
        await _conversaciones.GuardarConversacionAsync(conversacion, cancellationToken);
        await EnviarSiguientePreguntaPendienteAsync(campania, usuario, pregunta, numero, emisor, ahora, cancellationToken);
    }

    private async Task IniciarCoachingSecuencialAsync(
        DominioConversacion conversacion,
        Campania campania,
        Usuario usuario,
        Pregunta pregunta,
        NumeroWhatsApp numero,
        string? emisor,
        string respuestaPadreId,
        IReadOnlyList<(string RespuestaId, ResultadoEvaluacion Resultado, ContextoEvaluacion Contexto)> resultados,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        var umbral = _limites.ResolverUmbralBase(campania, pregunta);
        var raices = resultados.Select((item, indice) =>
        {
            var motivo = item.Resultado is ResultadoEvaluacion.Fallback
                ? MotivoFinalizacionIdea.Fallback
                : _limites.UmbralAlcanzado(
                    item.Resultado.Evaluacion.CalificacionTotal,
                    item.Contexto.RubricaSnapshot.Escala,
                    umbral)
                    ? MotivoFinalizacionIdea.Umbral
                    : (MotivoFinalizacionIdea?)null;
            return new RaizIdeaCoaching(indice + 1, item.RespuestaId, motivo);
        });

        var cola = _colaCoaching.Crear(respuestaPadreId, raices, ahora);
        if (pregunta.MaxRepreguntas <= 0 && cola.IdeaActiva is not null)
        {
            cola = _colaCoaching.FinalizarTodasAbiertas(cola, MotivoFinalizacionIdea.MaxRevisiones, ahora);
        }

        conversacion = conversacion.ConCoachingIdeas(cola);
        await RegistrarCoachingAsync(
            usuario.Id,
            usuario.WhatsappNormalizado,
            "iniciado",
            cola,
            null,
            ahora,
            cancellationToken);

        if (cola.Estado == EstadoCoachingIdeas.Finalizado)
        {
            await FinalizarColaAsync(
                conversacion,
                campania,
                usuario,
                pregunta,
                numero,
                emisor,
                prefijo: null,
                ahora,
                cancellationToken);
            return;
        }

        var activa = cola.IdeaActiva!;
        var resultadoActivo = resultados.Single(resultado => resultado.RespuestaId == activa.RespuestaVigenteId).Resultado;
        await EnviarPreguntaCoachingAsync(
            conversacion,
            campania,
            usuario.Id,
            numero,
            emisor,
            resultadoActivo.Evaluacion,
            ahora,
            cancellationToken);
    }

    /// <summary>
    /// I-19 §4.1/§4.6: arranca la cola I-18 en modo consolidado. Cada idea del mensaje conserva su aporte
    /// original inmutable y recibe una versión propuesta propia; la cola mantiene una sola idea activa y
    /// el participante confirma de a una. Ninguna idea se evalúa en este turno (§8.1).
    /// </summary>
    private async Task IniciarColaConsolidadaAsync(
        DominioConversacion conversacion,
        Campania campania,
        Usuario usuario,
        Pregunta pregunta,
        NumeroWhatsApp numero,
        string? emisor,
        ContextoEvaluacion contextoBase,
        IdeasResueltas resolucion,
        string respuestaPadreId,
        bool esRepregunta,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        var raices = new List<RaizIdeaCoaching>();
        foreach (var segmentada in resolucion.Ideas)
        {
            var respuestaId = resolucion.FueSegmentada
                ? CrearRespuestaIdIdea(respuestaPadreId, segmentada.Indice)
                : "resp_" + Guid.NewGuid().ToString("N");
            var ideaId = "idea_" + respuestaId;
            await _procesador.GuardarRespuestaAsync(
                respuestaId,
                campania.Id,
                usuario,
                pregunta,
                conversacion.Id,
                segmentada.Texto,
                esRepregunta,
                EstadoRespuesta.Recibida,
                ahora,
                cancellationToken,
                resolucion.FueSegmentada ? segmentada.Indice : null,
                resolucion.FueSegmentada ? respuestaPadreId : null,
                NivelMadurez.Incubacion,
                respuestaId,
                null,
                0,
                ideaId,
                TipoAporteIdea.Inicial);

            var idea = IdeaConsolidada.Crear(
                ideaId, campania.Id, usuario.Id, pregunta.Id, conversacion.Id, respuestaId, segmentada.Indice, ahora);
            var propuesta = await ProponerVersionAsync(
                campania, pregunta, conversacion.Idioma, contextoBase.ConfigLlmSnapshot, idea, versionVigente: null,
                respuestaId, segmentada.Texto, TipoAporteIdea.Inicial, ahora, cancellationToken);
            await _respuestas.GuardarVersionIdeaAsync(propuesta.Version, cancellationToken);
            idea = idea.ConPropuesta(propuesta.Version.Id, ahora);
            await _respuestas.GuardarIdeaConsolidadaAsync(idea, cancellationToken);
            await RegistrarPropuestaAsync(usuario, idea, propuesta, "propuesta", ahora, cancellationToken);
            raices.Add(new RaizIdeaCoaching(segmentada.Indice, respuestaId, null, ideaId, propuesta.Version.Id));
        }

        var cola = _colaCoaching.Crear(respuestaPadreId, raices, ahora);
        conversacion = conversacion.ConCoachingIdeas(cola);
        await RegistrarCoachingAsync(
            usuario.Id, usuario.WhatsappNormalizado, "iniciado", cola, null, ahora, cancellationToken);
        await PedirConfirmacionIdeaActivaAsync(
            conversacion, campania, usuario, pregunta, numero, emisor, prefijo: null, ahora, cancellationToken);
    }

    /// <summary>
    /// I-19 §4.1: pide confirmación de la versión propuesta de la idea activa. No cuenta como revisión
    /// (§4.3): las repreguntas socráticas solo se registran tras evaluar una versión confirmada.
    /// </summary>
    private async Task PedirConfirmacionIdeaActivaAsync(
        DominioConversacion conversacion,
        Campania campania,
        Usuario usuario,
        Pregunta pregunta,
        NumeroWhatsApp numero,
        string? emisor,
        string? prefijo,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        var activa = conversacion.CoachingIdeas!.IdeaActiva!;
        var version = await ObtenerVersionAsync(campania.Id, activa.VersionIdeaVigenteId, cancellationToken);
        if (version is null)
        {
            // Sin propuesta no hay nada que confirmar: la idea queda pendiente y la cola sigue su curso.
            await CerrarIdeaActivaYContinuarAsync(
                conversacion, campania, usuario, pregunta, numero, emisor, idea: null,
                EstadoResultadoIdeaConsolidada.Pendiente, null, "versionNoDisponible",
                MotivoFinalizacionIdea.Fallback, Combinar(prefijo, EvaluadorLlm.RetroNeutra), ahora, cancellationToken);
            return;
        }

        if (!_confirmacionExplicitaIdeasHabilitada)
        {
            var idea = activa.IdeaId is null
                ? null
                : await _respuestas.ObtenerIdeaConsolidadaAsync(campania.Id, activa.IdeaId, cancellationToken);
            if (idea is null)
            {
                await CerrarIdeaActivaYContinuarAsync(
                    conversacion, campania, usuario, pregunta, numero, emisor, idea: null,
                    EstadoResultadoIdeaConsolidada.Pendiente, null, "ideaNoDisponible",
                    MotivoFinalizacionIdea.Fallback, Combinar(prefijo, EvaluadorLlm.RetroNeutra), ahora,
                    cancellationToken);
                return;
            }

            await ConfirmarYEvaluarIdeaActivaAsync(
                conversacion, campania, usuario, pregunta, numero, emisor, idea, conforme: false,
                confirmacionImplicitaMejora: false, confirmacionAutomatica: true, ahora,
                cancellationToken);
            return;
        }

        await EnviarConfirmacionAsync(
            conversacion, campania, usuario, pregunta, numero, emisor, version.Texto, ahora,
            cancellationToken, prefijo);
        await _conversaciones.GuardarConversacionAsync(
            conversacion.AvanzarA(EstadoMaquinaConversacion.EsperandoRepregunta), cancellationToken);
    }

    /// <summary>
    /// I-19 §4.6: al cerrar una idea se activa la siguiente y se le pide su confirmación; si no queda
    /// ninguna abierta, la cola finaliza como siempre (I-18).
    /// </summary>
    private async Task ContinuarColaConsolidadaAsync(
        DominioConversacion conversacion,
        Campania campania,
        Usuario usuario,
        Pregunta pregunta,
        NumeroWhatsApp numero,
        string? emisor,
        string? prefijo,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        if (conversacion.CoachingIdeas!.Estado == EstadoCoachingIdeas.Finalizado)
        {
            await FinalizarColaAsync(
                conversacion, campania, usuario, pregunta, numero, emisor, prefijo, ahora, cancellationToken);
            return;
        }

        await PedirConfirmacionIdeaActivaAsync(
            conversacion, campania, usuario, pregunta, numero, emisor, prefijo, ahora, cancellationToken);
    }

    /// <summary>
    /// Cierra la idea activa con su resultado I-19 (§4.4/§4.5), finaliza su turno en la cola I-18 y
    /// continúa con la siguiente. Los aportes, versiones y evaluaciones se conservan para auditoría.
    /// </summary>
    private async Task CerrarIdeaActivaYContinuarAsync(
        DominioConversacion conversacion,
        Campania campania,
        Usuario usuario,
        Pregunta pregunta,
        NumeroWhatsApp numero,
        string? emisor,
        IdeaConsolidada? idea,
        EstadoResultadoIdeaConsolidada resultado,
        string? evaluacionId,
        string motivoCierre,
        MotivoFinalizacionIdea motivoCola,
        string? prefijo,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        if (idea is not null && idea.EstadoFlujo != EstadoFlujoIdeaConsolidada.Cerrada)
        {
            var cerrada = idea.Cerrar(resultado, evaluacionId, motivoCierre, ahora);
            await _respuestas.GuardarIdeaConsolidadaAsync(cerrada, cancellationToken);
            await RegistrarConsolidacionAsync(
                usuario, "cerrada", cerrada, null, motivoCierre, null, ahora, cancellationToken);
            // I-19 §10: el artefacto canónico refleja el estado final de la idea (madura/pendiente/rechazada).
            await _procesador.CompilarMarkdownIdeaAsync(campania.Id, idea.Id, cancellationToken);
        }

        var cola = _colaCoaching.FinalizarActiva(conversacion.CoachingIdeas!, motivoCola, ahora);
        conversacion = conversacion.ConCoachingIdeas(cola);
        await RegistrarCoachingAsync(
            usuario.Id,
            usuario.WhatsappNormalizado,
            motivoCola == MotivoFinalizacionIdea.Fallback ? "fallback" : "avance",
            cola,
            motivoCola,
            ahora,
            cancellationToken);
        await ContinuarColaConsolidadaAsync(
            conversacion, campania, usuario, pregunta, numero, emisor, prefijo, ahora, cancellationToken);
    }

    /// <summary>
    /// I-19 §4.2/§8.2: turno de una idea activa con referencias canónicas. El rechazo manda sobre todo;
    /// la salida "así está bien" solo corta cuando ya hay una versión confirmada; cualquier otro texto es
    /// aporte y genera una propuesta nueva. Nunca se evalúa el último mensaje suelto.
    /// </summary>
    private async Task ProcesarRevisionIdeaConsolidadaAsync(
        DominioConversacion conversacion,
        Campania campania,
        Usuario usuario,
        Pregunta pregunta,
        NumeroWhatsApp numero,
        string? emisor,
        string texto,
        string respuestaId,
        int revisionIndice,
        EstadoMaquinaConversacion estadoPrevio,
        IntencionControlPendiente? pendienteControl,
        bool turnosExcedidos,
        bool cupoLlamadasExcedido,
        bool presupuestoExcedido,
        DateTimeOffset ahora,
        ClasificacionIntencionPrevia? clasificacionPrevia,
        string? ideaIdConsultada,
        CancellationToken cancellationToken)
    {
        // I-19 §4.7: la respuesta a una lista de selección ya ofrecida se resuelve antes que nada; si no
        // es un número válido, la selección se cancela y el mensaje sigue como un turno normal.
        var seleccion = await ResolverSeleccionIdeaPendienteAsync(
            conversacion, campania, usuario, pregunta, numero, emisor, texto, estadoPrevio, ahora, cancellationToken);
        if (seleccion.Manejado)
        {
            return;
        }

        conversacion = seleccion.Conversacion;
        var activa = conversacion.CoachingIdeas!.IdeaActiva!;
        var idea = await _respuestas.ObtenerIdeaConsolidadaAsync(campania.Id, activa.IdeaId!, cancellationToken);
        if (idea is null || idea.EstadoFlujo == EstadoFlujoIdeaConsolidada.Cerrada)
        {
            await CerrarIdeaActivaYContinuarAsync(
                conversacion, campania, usuario, pregunta, numero, emisor, idea: null,
                EstadoResultadoIdeaConsolidada.Pendiente, null, "ideaNoDisponible",
                MotivoFinalizacionIdea.Fallback, EvaluadorLlm.RetroNeutra, ahora, cancellationToken);
            return;
        }

        // §4.5: "no lo guardes" cierra solo esta idea como rechazada, conservando su historial. Los
        // aportes I-19 nunca se sellan como maduros (la madurez vive en la idea), asi que no hay
        // respuestas que degradar como en el flujo I-17 §5.4.
        var detectores = await ResolverDetectoresAsync(conversacion, cancellationToken);
        if (detectores.RechazoIdea.Coincide(texto))
        {
            await CerrarIdeaActivaYContinuarAsync(
                conversacion, campania, usuario, pregunta, numero, emisor, idea,
                EstadoResultadoIdeaConsolidada.Rechazada, null, "rechazoParticipante",
                MotivoFinalizacionIdea.Rechazo,
                await TextoGlobalAsync(
                    conversacion,
                    "acuseRechazoGuardado",
                    TextoConfigurado(_mensajes.AcuseRechazoGuardado, OpcionesMensajesConversacion.AcuseRechazoGuardadoDefault),
                    cancellationToken),
                ahora,
                cancellationToken);
            return;
        }

        // §8.2 (idea en mejora): la petición de revisitar va justo después del rechazo, antes de la
        // salida "así está bien" y de tratar el mensaje como aporte.
        if (idea.EstadoFlujo != EstadoFlujoIdeaConsolidada.PendienteConfirmacion
            && await IntentarReaperturaIdeaAsync(
                conversacion, campania, usuario, pregunta, numero, emisor, texto, ahora, cancellationToken))
        {
            return;
        }

        var salidaPendiente = await ResolverSalidaPendienteAsync(
            campania, conversacion, pendienteControl, estadoPrevio, texto, numero, emisor, ahora, cancellationToken);
        if (salidaPendiente.Manejado)
        {
            return;
        }

        var decisionControl = salidaPendiente.Decision ?? await ResolverIntencionControlAsync(
            campania, usuario, conversacion, estadoPrevio, hayUnidadActiva: true,
            quedanUnidadesPendientes: conversacion.CoachingIdeas!.Ideas.Any(entrada => entrada.Estado == EstadoIdeaCoaching.Pendiente),
            texto, ahora, clasificacionPrevia,
            permitirConfirmarIdea: string.Equals(ideaIdConsultada, idea.Id, StringComparison.Ordinal), cancellationToken);
        if (await EjecutarControlColaAsync(
                decisionControl, conversacion, campania, usuario, pregunta, numero, emisor, idea, ahora,
                cancellationToken))
        {
            return;
        }

        var intencion = detectores.Transicion.Interpretar(
            EstadoMaquinaConversacion.EsperandoRepregunta, activa.RepreguntasUsadas, pregunta.MaxRepreguntas, texto);

        var confirmacionSemantica = decisionControl == DecisionIntencionControl.ConfirmarIdea;

        // §4.2: en pendienteConfirmacion "así está bien" primero confirma la versión y se evalúa una vez;
        // con una versión ya confirmada, la misma frase cierra la idea como pendiente.
        if ((intencion.DeseaContinuar || confirmacionSemantica)
            && idea.EstadoFlujo != EstadoFlujoIdeaConsolidada.PendienteConfirmacion)
        {
            await CerrarIdeaActivaYContinuarAsync(
                conversacion, campania, usuario, pregunta, numero, emisor, idea,
                EstadoResultadoIdeaConsolidada.Pendiente, null, "participante",
                MotivoFinalizacionIdea.Participante,
                await SeleccionarAcuseContinuarAsync(conversacion, cancellationToken),
                ahora,
                cancellationToken);
            return;
        }

        // Techos deterministas (10 §2 / D2): no se consolida ni se evalua, pero el aporte se conserva
        // (I-19 §12.3) y la idea queda pendiente con lo ya confirmado.
        if (turnosExcedidos || cupoLlamadasExcedido || presupuestoExcedido)
        {
            await GuardarAporteIdeaAsync(
                conversacion, campania, usuario, pregunta, idea, texto, respuestaId, revisionIndice,
                ahora, cancellationToken);
            var motivoTecho = turnosExcedidos
                ? "tope_turnos_hilo"
                : cupoLlamadasExcedido
                    ? "cupo_llamadas_llm_usuario"
                    : "presupuesto_tokens_campania";
            await RegistrarRateLimitAsync(usuario, motivoTecho, ahora, cancellationToken);
            await CerrarIdeaActivaYContinuarAsync(
                conversacion, campania, usuario, pregunta, numero, emisor, idea,
                EstadoResultadoIdeaConsolidada.Pendiente, null, "techoDeterminista",
                MotivoFinalizacionIdea.Fallback, EvaluadorLlm.RetroNeutra, ahora, cancellationToken);
            return;
        }

        if (idea.EstadoFlujo == EstadoFlujoIdeaConsolidada.PendienteConfirmacion
            && (confirmacionSemantica || detectores.Confirmacion.Coincide(texto)))
        {
            await ConfirmarYEvaluarIdeaActivaAsync(
                conversacion, campania, usuario, pregunta, numero, emisor, idea, intencion.DeseaContinuar,
                confirmacionImplicitaMejora: false, confirmacionAutomatica: false, ahora: ahora,
                cancellationToken: cancellationToken);
            return;
        }

        // P-24: una petición corta de ayuda no aporta hechos nuevos. Confirma implícitamente la propuesta
        // completa para diagnosticarla contra la rúbrica y abrir una sola pregunta socrática si hace falta.
        // El Mensaje entrante queda auditado por el pipeline, pero no se guarda como Respuesta ni versión.
        if (idea.EstadoFlujo == EstadoFlujoIdeaConsolidada.PendienteConfirmacion
            && detectores.SolicitarMejora.Coincide(texto))
        {
            await ConfirmarYEvaluarIdeaActivaAsync(
                conversacion, campania, usuario, pregunta, numero, emisor, idea, conforme: false,
                confirmacionImplicitaMejora: true, confirmacionAutomatica: false, ahora: ahora,
                cancellationToken: cancellationToken);
            return;
        }

        // §8.2 (versión propuesta): con una propuesta pendiente, la petición de revisitar se atiende
        // después de la confirmación y antes de tratar el mensaje como corrección.
        if (idea.EstadoFlujo == EstadoFlujoIdeaConsolidada.PendienteConfirmacion
            && await IntentarReaperturaIdeaAsync(
                conversacion, campania, usuario, pregunta, numero, emisor, texto, ahora, cancellationToken))
        {
            return;
        }

        await ProponerVersionComplementariaAsync(
            conversacion, campania, usuario, pregunta, numero, emisor, idea, texto, respuestaId,
            revisionIndice, ahora, cancellationToken);
    }

    /// <summary>
    /// I-19 §4.3: la versión confirmada es la unidad que se evalúa completa. El resultado decide, de forma
    /// determinista, si la idea madura, si sigue en coaching socrático (I-18) o si queda pendiente.
    /// </summary>
    private async Task ConfirmarYEvaluarIdeaActivaAsync(
        DominioConversacion conversacion,
        Campania campania,
        Usuario usuario,
        Pregunta pregunta,
        NumeroWhatsApp numero,
        string? emisor,
        IdeaConsolidada idea,
        bool conforme,
        bool confirmacionImplicitaMejora,
        bool confirmacionAutomatica,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        var activa = conversacion.CoachingIdeas!.IdeaActiva!;
        var version = await ObtenerVersionAsync(campania.Id, idea.VersionPropuestaRef, cancellationToken);
        if (version is null)
        {
            await CerrarIdeaActivaYContinuarAsync(
                conversacion, campania, usuario, pregunta, numero, emisor, idea,
                EstadoResultadoIdeaConsolidada.Pendiente, null, "versionNoDisponible",
                MotivoFinalizacionIdea.Fallback, EvaluadorLlm.RetroNeutra, ahora, cancellationToken);
            return;
        }

        version = version.Confirmar(ahora);
        idea = idea.ConfirmarVersion(version.Id, ahora);
        await _respuestas.GuardarVersionIdeaAsync(version, cancellationToken);
        await _respuestas.GuardarIdeaConsolidadaAsync(idea, cancellationToken);
        await RegistrarConsolidacionAsync(
            usuario,
            confirmacionAutomatica
                ? "confirmadaAutomatica"
                : confirmacionImplicitaMejora
                    ? "confirmadaImplicitaMejora"
                    : "confirmada",
            idea,
            version.NumeroVersion, null, null, ahora, cancellationToken);
        // La respuesta vigente sigue siendo el último aporte; la versión confirmada es lo que se evalúa.
        conversacion = conversacion.ConCoachingIdeas(
            _colaCoaching.ActualizarVersionIdeaVigente(conversacion.CoachingIdeas!, idea.Id, version.Id));

        var contextoDisponible = await ConstruirContextoAsync(
            campania, pregunta, usuario, conversacion.Id, idea.RespuestaRaizId, version.Texto, cancellationToken);
        if (contextoDisponible.Contexto is null)
        {
            await RegistrarConfiguracionNoDisponibleAsync(
                usuario, contextoDisponible.Motivo ?? "configuracion_no_disponible", ahora, cancellationToken);
            await CerrarIdeaActivaYContinuarAsync(
                conversacion, campania, usuario, pregunta, numero, emisor, idea,
                EstadoResultadoIdeaConsolidada.Pendiente, null, "configuracionNoDisponible",
                MotivoFinalizacionIdea.Fallback, EvaluadorLlm.RetroNeutra, ahora, cancellationToken);
            return;
        }

        var contexto = contextoDisponible.Contexto with
        {
            IdeaId = idea.Id,
            VersionIdeaId = version.Id,
            RespuestaTexto = version.Texto,
            HistorialReciente = await ConstruirHistorialIdeaAsync(campania.Id, idea.RespuestaRaizId, cancellationToken),
            // El prompt socrático de I-18 solo se pide si su gate está activo; sin él, la idea se evalúa
            // una vez y se cierra.
            CoachingSecuencialIdeas = CoachingEfectivo(campania),
            SolicitarParafraseo = false,
        };
        var resultado = ConProcedenciaIdea(
            await _evaluador.EvaluarAsync(contexto, cancellationToken), idea.Id, version.Id);
        await _respuestas.GuardarEvaluacionAsync(resultado.Evaluacion, cancellationToken);
        await RegistrarConsolidacionAsync(
            usuario,
            resultado is ResultadoEvaluacion.Fallback ? "fallback" : "evaluada",
            idea,
            version.NumeroVersion,
            resultado is ResultadoEvaluacion.Fallback fallidaCola ? fallidaCola.Motivo : null,
            resultado.Evaluacion.UsoTokens,
            ahora,
            cancellationToken);

        var esFallback = resultado is ResultadoEvaluacion.Fallback;
        var madura = !esFallback
            && _limites.UmbralAlcanzado(
                resultado.Evaluacion.CalificacionTotal,
                contexto.RubricaSnapshot.Escala,
                _limites.ResolverUmbralBase(campania, pregunta));
        MotivoFinalizacionIdea? motivoCola = madura
            ? MotivoFinalizacionIdea.Umbral
            : esFallback
                ? MotivoFinalizacionIdea.Fallback
                : conforme
                    ? MotivoFinalizacionIdea.Participante
                    // Sin el acompañamiento I-18 no hay pregunta socrática que ofrecer: la idea se
                    // evalúa una vez y queda pendiente.
                    : activa.RepreguntasUsadas >= pregunta.MaxRepreguntas || !CoachingEfectivo(campania)
                        ? MotivoFinalizacionIdea.MaxRevisiones
                        : null;

        if (motivoCola is null)
        {
            // Sigue el acompañamiento I-18 sobre esta misma idea: una sola pregunta socrática por turno.
            // El artefacto se regenera con la versión confirmada y su evaluación (I-19 §10).
            await _procesador.CompilarMarkdownIdeaAsync(campania.Id, idea.Id, cancellationToken);
            await EnviarPreguntaCoachingAsync(
                conversacion, campania, usuario.Id, numero, emisor, resultado.Evaluacion, ahora, cancellationToken);
            return;
        }

        await CerrarIdeaActivaYContinuarAsync(
            conversacion,
            campania,
            usuario,
            pregunta,
            numero,
            emisor,
            idea,
            madura ? EstadoResultadoIdeaConsolidada.Madura : EstadoResultadoIdeaConsolidada.Pendiente,
            resultado.Evaluacion.Id,
            madura ? "umbral" : esFallback ? "fallbackEvaluacion" : conforme ? "participante" : "maxRevisiones",
            motivoCola.Value,
            resultado.Evaluacion.RetroalimentacionEnviada,
            ahora,
            cancellationToken);
    }

    /// <summary>
    /// I-19 §4.2/§4.3: una corrección, un complemento o una mejora se guardan como aporte nuevo y vuelven
    /// a proponer la versión completa. El ciclo no avanza hasta que el participante confirme.
    /// </summary>
    private async Task ProponerVersionComplementariaAsync(
        DominioConversacion conversacion,
        Campania campania,
        Usuario usuario,
        Pregunta pregunta,
        NumeroWhatsApp numero,
        string? emisor,
        IdeaConsolidada idea,
        string texto,
        string respuestaId,
        int revisionIndice,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        var tipoAporte = idea.EstadoFlujo == EstadoFlujoIdeaConsolidada.PendienteConfirmacion
            ? TipoAporteIdea.Correccion
            : TipoAporteIdea.Complemento;
        await GuardarAporteIdeaAsync(
            conversacion, campania, usuario, pregunta, idea, texto, respuestaId, revisionIndice,
            ahora, cancellationToken, tipoAporte);

        var contextoDisponible = await ConstruirContextoAsync(
            campania, pregunta, usuario, conversacion.Id, respuestaId, texto, cancellationToken);
        if (contextoDisponible.Contexto is null)
        {
            await RegistrarConfiguracionNoDisponibleAsync(
                usuario, contextoDisponible.Motivo ?? "configuracion_no_disponible", ahora, cancellationToken);
            await CerrarIdeaActivaYContinuarAsync(
                conversacion, campania, usuario, pregunta, numero, emisor, idea,
                EstadoResultadoIdeaConsolidada.Pendiente, null, "configuracionNoDisponible",
                MotivoFinalizacionIdea.Fallback, EvaluadorLlm.RetroNeutra, ahora, cancellationToken);
            return;
        }

        var propuesta = await ProponerVersionAsync(
            campania, pregunta, conversacion.Idioma, contextoDisponible.Contexto.ConfigLlmSnapshot, idea,
            await ObtenerVersionVigenteAsync(campania.Id, idea, cancellationToken),
            respuestaId, texto, tipoAporte, ahora, cancellationToken);
        if (propuesta.PreguntaAclaracion is not null)
        {
            await PedirAclaracionAsync(
                conversacion, campania, pregunta, usuario, numero, emisor, idea, propuesta, ahora,
                cancellationToken);
            return;
        }

        await _respuestas.GuardarVersionIdeaAsync(propuesta.Version, cancellationToken);
        idea = idea.ConPropuesta(propuesta.Version.Id, ahora);
        await _respuestas.GuardarIdeaConsolidadaAsync(idea, cancellationToken);
        await RegistrarPropuestaAsync(usuario, idea, propuesta, "corregida", ahora, cancellationToken);
        conversacion = conversacion.ConCoachingIdeas(
            _colaCoaching.ActualizarVersionIdeaVigente(
                _colaCoaching.ActualizarRespuestaVigente(conversacion.CoachingIdeas!, respuestaId),
                idea.Id,
                propuesta.Version.Id));

        // I-19 §4.6: lo pertinente ya alimentó la idea activa; la idea nueva se encola aparte y se
        // trabajará después, sin mezclar los contenidos ni interrumpir la confirmación en curso.
        conversacion = await EncolarIdeasNuevasAsync(
            conversacion, campania, usuario, pregunta, contextoDisponible.Contexto.ConfigLlmSnapshot,
            IdeasNuevasAdmisibles(propuesta.NuevasIdeas, texto, propuesta.Version.Texto), respuestaId,
            ahora, cancellationToken);
        await PedirConfirmacionIdeaActivaAsync(
            conversacion, campania, usuario, pregunta, numero, emisor, prefijo: null, ahora, cancellationToken);
    }

    /// <summary>
    /// I-19 §4.6 con cola I-18: añade cada idea nueva al final de la cola, con su propio aporte y su
    /// propuesta. El servidor impone el tope, el orden, la idempotencia y la regla de una sola activa;
    /// la idea activa no se toca y ninguna idea nueva se anuncia hasta que llega su turno.
    /// </summary>
    private async Task<DominioConversacion> EncolarIdeasNuevasAsync(
        DominioConversacion conversacion,
        Campania campania,
        Usuario usuario,
        Pregunta pregunta,
        ConfigLlm configLlm,
        IReadOnlyList<string> nuevasIdeas,
        string aporteOrigenId,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        var orden = 0;
        foreach (var texto in nuevasIdeas)
        {
            var cola = conversacion.CoachingIdeas!;
            if (!_colaCoaching.PuedeAgregarIdea(cola, _maxIdeasPorMensaje))
            {
                break;
            }

            orden++;
            var respuestaId = $"{aporteOrigenId}_n{orden}";
            var indice = _colaCoaching.SiguienteIndice(cola);
            var version = await CrearIdeaNuevaAsync(
                conversacion, campania, usuario, pregunta, configLlm, texto, respuestaId, indice,
                cola.RespuestaPadreId, ahora, cancellationToken);
            conversacion = conversacion.ConCoachingIdeas(
                _colaCoaching.AgregarIdeaPendiente(
                    cola,
                    new RaizIdeaCoaching(indice, respuestaId, null, "idea_" + respuestaId, version.Id),
                    _maxIdeasPorMensaje));
        }

        return conversacion;
    }

    /// <summary>Persiste un aporte de la idea activa sin evaluarlo (I-19 §8.1).</summary>
    private Task GuardarAporteIdeaAsync(
        DominioConversacion conversacion,
        Campania campania,
        Usuario usuario,
        Pregunta pregunta,
        IdeaConsolidada idea,
        string texto,
        string respuestaId,
        int revisionIndice,
        DateTimeOffset ahora,
        CancellationToken cancellationToken,
        TipoAporteIdea tipoAporte = TipoAporteIdea.Complemento)
    {
        var cola = conversacion.CoachingIdeas!;
        var activa = cola.IdeaActiva!;
        return _procesador.GuardarRespuestaAsync(
            respuestaId,
            campania.Id,
            usuario,
            pregunta,
            conversacion.Id,
            texto,
            esRepregunta: true,
            EstadoRespuesta.Recibida,
            ahora,
            cancellationToken,
            activa.IdeaIndice,
            cola.RespuestaPadreId,
            NivelMadurez.Incubacion,
            activa.RespuestaRaizId,
            activa.RespuestaVigenteId,
            revisionIndice,
            idea.Id,
            tipoAporte);
    }

    private static ResultadoEvaluacion ConProcedenciaIdea(
        ResultadoEvaluacion resultado, string ideaId, string versionIdeaId)
    {
        var evaluacion = resultado.Evaluacion.ConProcedenciaIdea(ideaId, versionIdeaId);
        return resultado switch
        {
            ResultadoEvaluacion.Exito => new ResultadoEvaluacion.Exito(evaluacion),
            ResultadoEvaluacion.Fallback fallback => new ResultadoEvaluacion.Fallback(evaluacion, fallback.Motivo),
            _ => throw new InvalidOperationException("Resultado de evaluación no soportado."),
        };
    }

    private async Task ProcesarRevisionCoachingAsync(
        DominioConversacion conversacion,
        Campania campania,
        Usuario usuario,
        ParticipanteResuelto participante,
        Pregunta pregunta,
        NumeroWhatsApp numero,
        string? emisor,
        MensajeEntrante mensaje,
        DateTimeOffset ahora,
        ClasificacionIntencionPrevia? clasificacionPrevia,
        string? ideaIdConsultada,
        CancellationToken cancellationToken)
    {
        var cola = conversacion.CoachingIdeas!;
        var activa = cola.IdeaActiva!;
        var estadoPrevio = conversacion.EstadoMaquina;
        var pendienteControl = conversacion.IntencionControlPendiente;
        var turnosExcedidos = await TurnosHiloExcedidosAsync(conversacion, cancellationToken);
        var cupoLlamadasExcedido = _cuposHabilitados
            && await CupoLlamadasLlmExcedidoAsync(campania, usuario.Id, ahora, cancellationToken);
        var presupuestoExcedido = _cuposHabilitados
            && !cupoLlamadasExcedido
            && await PresupuestoTokensExcedidoAsync(campania, cancellationToken);
        await GuardarMensajeAsync(
            conversacion,
            DireccionMensaje.In,
            mensaje.Texto,
            mensaje.WhatsappMessageId,
            mensaje.Timestamp,
            cancellationToken);
        await MarcarParticipanteRespondioAsync(participante.Participante, ahora, cancellationToken);
        conversacion = conversacion.RegistrarEntrante(mensaje.Timestamp).AvanzarA(EstadoMaquinaConversacion.Evaluando);

        var anterior = await _respuestas.ObtenerRespuestaAsync(campania.Id, activa.RespuestaVigenteId, cancellationToken);
        if (anterior is null)
        {
            cola = _colaCoaching.FinalizarTodasAbiertas(cola, MotivoFinalizacionIdea.Fallback, ahora);
            await FinalizarColaAsync(
                conversacion.ConCoachingIdeas(cola),
                campania,
                usuario,
                pregunta,
                numero,
                emisor,
                EvaluadorLlm.RetroNeutra,
                ahora,
                cancellationToken);
            return;
        }

        // Fuera de la ventana de servicio, el timeout activa la siguiente idea sin enviar texto libre.
        // El primer entrante posterior solo dispara su turno de coaching; no se atribuye como revision.
        if (activa.RepreguntasUsadas == 0)
        {
            var evaluacionPendiente = await _respuestas.ObtenerEvaluacionPorRespuestaAsync(
                campania.Id,
                activa.RespuestaVigenteId,
                cancellationToken);
            if (evaluacionPendiente is not null)
            {
                await EnviarPreguntaCoachingAsync(
                    conversacion,
                    campania,
                    usuario.Id,
                    numero,
                    emisor,
                    evaluacionPendiente,
                    ahora,
                    cancellationToken);
                return;
            }
        }

        var revisionIndice = (anterior.RevisionIndice ?? 0) + 1;
        var respuestaId = $"{activa.RespuestaRaizId}_rev_{revisionIndice}";
        // Apagar el gate I-18 desactiva el acompañamiento, no la cola de ideas: una cola con referencias
        // canónicas (I-19) sigue su ciclo aunque el coaching esté apagado. El apagado en caliente solo
        // finaliza las colas legacy, que sí existían únicamente por I-18.
        var colaConsolidada = ConsolidacionIdeasActiva && !string.IsNullOrWhiteSpace(activa.IdeaId);
        var gatesActivos = CoachingEfectivo(campania);
        if (!gatesActivos && !colaConsolidada)
        {
            await _procesador.GuardarRespuestaAsync(
                respuestaId,
                campania.Id,
                usuario,
                pregunta,
                conversacion.Id,
                mensaje.Texto,
                esRepregunta: true,
                EstadoRespuesta.Recibida,
                ahora,
                cancellationToken,
                activa.IdeaIndice,
                cola.RespuestaPadreId,
                NivelMadurez.Incubacion,
                activa.RespuestaRaizId,
                activa.RespuestaVigenteId,
                revisionIndice);
            cola = _colaCoaching.ActualizarRespuestaVigente(cola, respuestaId);
            cola = _colaCoaching.FinalizarTodasAbiertas(cola, MotivoFinalizacionIdea.Desactivacion, ahora);
            await RegistrarCoachingAsync(
                usuario.Id,
                usuario.WhatsappNormalizado,
                "finalizada",
                cola,
                MotivoFinalizacionIdea.Desactivacion,
                ahora,
                cancellationToken);
            await FinalizarColaAsync(
                conversacion.ConCoachingIdeas(cola),
                campania,
                usuario,
                pregunta,
                numero,
                emisor,
                prefijo: null,
                ahora,
                cancellationToken);
            return;
        }

        // I-19 §15 paso 5: si la idea activa ya tiene referencias canónicas, su turno sigue el ciclo
        // consolidado (propuesta → confirmación → evaluación de la versión completa).
        if (ConsolidacionIdeasActiva && !string.IsNullOrWhiteSpace(activa.IdeaId))
        {
            await ProcesarRevisionIdeaConsolidadaAsync(
                conversacion,
                campania,
                usuario,
                pregunta,
                numero,
                emisor,
                mensaje.Texto,
                respuestaId,
                revisionIndice,
                estadoPrevio,
                pendienteControl,
                turnosExcedidos,
                cupoLlamadasExcedido,
                presupuestoExcedido,
                ahora,
                clasificacionPrevia,
                ideaIdConsultada,
                cancellationToken);
            return;
        }

        var detectores = await ResolverDetectoresAsync(conversacion, cancellationToken);
        var intencion = detectores.Transicion.Interpretar(
            EstadoMaquinaConversacion.EsperandoRepregunta,
            activa.RepreguntasUsadas,
            pregunta.MaxRepreguntas,
            mensaje.Texto);
        if (!intencion.DeseaRechazarGuardado)
        {
            var salidaPendiente = await ResolverSalidaPendienteAsync(
                campania, conversacion, pendienteControl, estadoPrevio, mensaje.Texto, numero, emisor, ahora,
                cancellationToken);
            if (salidaPendiente.Manejado)
            {
                return;
            }

            var decisionControl = salidaPendiente.Decision ?? await ResolverIntencionControlAsync(
                campania, usuario, conversacion, estadoPrevio, hayUnidadActiva: true,
                quedanUnidadesPendientes: cola.Ideas.Any(entrada => entrada.Estado == EstadoIdeaCoaching.Pendiente),
                mensaje.Texto, ahora, clasificacionPrevia, permitirConfirmarIdea: false, cancellationToken);
            if (await EjecutarControlColaAsync(
                    decisionControl, conversacion, campania, usuario, pregunta, numero, emisor, idea: null, ahora,
                    cancellationToken))
            {
                return;
            }
        }

        if (intencion.DeseaContinuar || intencion.DeseaRechazarGuardado)
        {
            if (intencion.DeseaRechazarGuardado && anterior.NivelMadurez == NivelMadurez.Maduro)
            {
                await _procesador.ReclasificarComoIncubacionAsync(
                    campania,
                    usuario,
                    pregunta,
                    new[] { anterior },
                    ahora,
                    cancellationToken);
            }

            var motivo = intencion.DeseaRechazarGuardado
                ? MotivoFinalizacionIdea.Rechazo
                : MotivoFinalizacionIdea.Participante;
            cola = _colaCoaching.FinalizarActiva(cola, motivo, ahora);
            conversacion = conversacion.ConCoachingIdeas(cola);
            await RegistrarCoachingAsync(
                usuario.Id,
                usuario.WhatsappNormalizado,
                "avance",
                cola,
                motivo,
                ahora,
                cancellationToken);
            var acuse = intencion.DeseaRechazarGuardado
                ? await TextoGlobalAsync(
                    conversacion,
                    "acuseRechazoGuardado",
                    TextoConfigurado(_mensajes.AcuseRechazoGuardado, OpcionesMensajesConversacion.AcuseRechazoGuardadoDefault),
                    cancellationToken)
                : await SeleccionarAcuseContinuarAsync(conversacion, cancellationToken);
            await ContinuarOFinalizarColaAsync(
                conversacion,
                campania,
                usuario,
                pregunta,
                numero,
                emisor,
                acuse,
                ahora,
                cancellationToken);
            return;
        }

        if (turnosExcedidos || cupoLlamadasExcedido || presupuestoExcedido)
        {
            await _procesador.GuardarRespuestaAsync(
                respuestaId,
                campania.Id,
                usuario,
                pregunta,
                conversacion.Id,
                mensaje.Texto,
                esRepregunta: true,
                EstadoRespuesta.Recibida,
                ahora,
                cancellationToken,
                activa.IdeaIndice,
                cola.RespuestaPadreId,
                NivelMadurez.Incubacion,
                activa.RespuestaRaizId,
                activa.RespuestaVigenteId,
                revisionIndice);
            cola = _colaCoaching.ActualizarRespuestaVigente(cola, respuestaId);
            cola = _colaCoaching.FinalizarActiva(cola, MotivoFinalizacionIdea.Fallback, ahora);
            var motivoCupo = turnosExcedidos
                ? "tope_turnos_hilo"
                : cupoLlamadasExcedido
                    ? "cupo_llamadas_llm_usuario"
                    : "presupuesto_tokens_campania";
            await RegistrarRateLimitAsync(usuario, motivoCupo, ahora, cancellationToken);
            await ContinuarOFinalizarColaAsync(
                conversacion.ConCoachingIdeas(cola),
                campania,
                usuario,
                pregunta,
                numero,
                emisor,
                EvaluadorLlm.RetroNeutra,
                ahora,
                cancellationToken);
            return;
        }

        var contextoDisponible = await ConstruirContextoAsync(
            campania,
            pregunta,
            usuario,
            conversacion.Id,
            respuestaId,
            mensaje.Texto,
            cancellationToken);
        if (contextoDisponible.Contexto is null)
        {
            cola = _colaCoaching.FinalizarActiva(cola, MotivoFinalizacionIdea.Fallback, ahora);
            await ContinuarOFinalizarColaAsync(
                conversacion.ConCoachingIdeas(cola),
                campania,
                usuario,
                pregunta,
                numero,
                emisor,
                EvaluadorLlm.RetroNeutra,
                ahora,
                cancellationToken);
            return;
        }

        var contexto = contextoDisponible.Contexto with
        {
            HistorialReciente = await ConstruirHistorialIdeaAsync(campania.Id, activa.RespuestaRaizId, cancellationToken),
            CoachingSecuencialIdeas = true,
            SolicitarParafraseo = false,
        };
        var resultado = await _evaluador.EvaluarAsync(contexto, cancellationToken);
        await _procesador.PersistirRespuestaEvaluadaAsync(
            resultado,
            campania,
            pregunta,
            usuario,
            conversacion.Id,
            respuestaId,
            mensaje.Texto,
            esRepregunta: true,
            contexto.RubricaSnapshot.Escala,
            ahora,
            cancellationToken,
            activa.IdeaIndice,
            cola.RespuestaPadreId,
            activa.RespuestaRaizId,
            activa.RespuestaVigenteId,
            revisionIndice);
        cola = _colaCoaching.ActualizarRespuestaVigente(cola, respuestaId);

        MotivoFinalizacionIdea? motivoFinalizacion = resultado is ResultadoEvaluacion.Fallback
            ? MotivoFinalizacionIdea.Fallback
            : _limites.UmbralAlcanzado(
                resultado.Evaluacion.CalificacionTotal,
                contexto.RubricaSnapshot.Escala,
                _limites.ResolverUmbralBase(campania, pregunta))
                ? MotivoFinalizacionIdea.Umbral
                : activa.RepreguntasUsadas >= pregunta.MaxRepreguntas
                    ? MotivoFinalizacionIdea.MaxRevisiones
                    : null;

        if (motivoFinalizacion.HasValue)
        {
            cola = _colaCoaching.FinalizarActiva(cola, motivoFinalizacion.Value, ahora);
            conversacion = conversacion.ConCoachingIdeas(cola);
            await RegistrarCoachingAsync(
                usuario.Id,
                usuario.WhatsappNormalizado,
                motivoFinalizacion == MotivoFinalizacionIdea.Fallback ? "fallback" : "avance",
                cola,
                motivoFinalizacion,
                ahora,
                cancellationToken);
            await ContinuarOFinalizarColaAsync(
                conversacion,
                campania,
                usuario,
                pregunta,
                numero,
                emisor,
                resultado.Evaluacion.RetroalimentacionEnviada,
                ahora,
                cancellationToken);
            return;
        }

        conversacion = conversacion.ConCoachingIdeas(cola);
        await EnviarPreguntaCoachingAsync(
            conversacion,
            campania,
            usuario.Id,
            numero,
            emisor,
            resultado.Evaluacion,
            ahora,
            cancellationToken);
    }

    private async Task ContinuarOFinalizarColaAsync(
        DominioConversacion conversacion,
        Campania campania,
        Usuario usuario,
        Pregunta pregunta,
        NumeroWhatsApp numero,
        string? emisor,
        string? prefijo,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        if (conversacion.CoachingIdeas!.Estado == EstadoCoachingIdeas.Finalizado)
        {
            await FinalizarColaAsync(
                conversacion,
                campania,
                usuario,
                pregunta,
                numero,
                emisor,
                prefijo,
                ahora,
                cancellationToken);
            return;
        }

        var activa = conversacion.CoachingIdeas.IdeaActiva!;
        var evaluacion = await _respuestas.ObtenerEvaluacionPorRespuestaAsync(
            campania.Id,
            activa.RespuestaVigenteId,
            cancellationToken);
        if (evaluacion is null)
        {
            var cola = _colaCoaching.FinalizarActiva(
                conversacion.CoachingIdeas,
                MotivoFinalizacionIdea.Fallback,
                ahora);
            await ContinuarOFinalizarColaAsync(
                conversacion.ConCoachingIdeas(cola),
                campania,
                usuario,
                pregunta,
                numero,
                emisor,
                EvaluadorLlm.RetroNeutra,
                ahora,
                cancellationToken);
            return;
        }

        await EnviarPreguntaCoachingAsync(
            conversacion,
            campania,
            usuario.Id,
            numero,
            emisor,
            evaluacion,
            ahora,
            cancellationToken,
            prefijo);
    }

    private async Task EnviarPreguntaCoachingAsync(
        DominioConversacion conversacion,
        Campania campania,
        string usuarioId,
        NumeroWhatsApp numero,
        string? emisor,
        ElTejido.Domain.Evaluacion.Evaluacion evaluacion,
        DateTimeOffset ahora,
        CancellationToken cancellationToken,
        string? prefijo = null)
    {
        var cola = conversacion.CoachingIdeas!;
        var activa = cola.IdeaActiva!;
        var preguntaCoaching = string.IsNullOrWhiteSpace(evaluacion.RepreguntaSugerida)
            ? EvaluadorLlm.RepreguntaNeutra
            : evaluacion.RepreguntaSugerida.Trim();
        // I-20 §3.2: una sola intervención — reconoce el avance y hace la pregunta de foco **ya
        // aprobada** por I-03. La retro validada es el cuerpo; el respaldo es la concatenación de hoy.
        var preguntaHilo = campania.Preguntas.FirstOrDefault(p => p.Id == conversacion.PreguntaId);
        var redactado = await ComponerTurnoAsync(
            campania, preguntaHilo, usuarioId, numero, ActoConversacional.Mejorar,
            respaldo: CombinarSinDuplicar(evaluacion.RetroalimentacionEnviada, preguntaCoaching),
            ahora, cancellationToken,
            cuerpo: evaluacion.RetroalimentacionEnviada,
            retroalimentacionValidada: evaluacion.RetroalimentacionEnviada,
            preguntaAprobada: preguntaCoaching,
            idioma: conversacion.Idioma);
        var turno = Combinar(prefijo, redactado);
        await EnviarAsync(conversacion, numero, turno, TipoEnvioMensaje.Repregunta, emisor, ahora, cancellationToken);

        cola = _colaCoaching.RegistrarRepregunta(cola);
        conversacion = conversacion
            .ConCoachingIdeas(cola)
            .AvanzarA(EstadoMaquinaConversacion.EsperandoRepregunta);
        await _conversaciones.GuardarConversacionAsync(conversacion, cancellationToken);
        await RegistrarCoachingAsync(usuarioId, numero, "repregunta", cola, null, ahora, cancellationToken);
    }

    private async Task FinalizarColaAsync(
        DominioConversacion conversacion,
        Campania campania,
        Usuario usuario,
        Pregunta pregunta,
        NumeroWhatsApp numero,
        string? emisor,
        string? prefijo,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        // I-20: el cierre se redacta sobre el mensaje configurado de la campaña, que sigue siendo el
        // respaldo exacto. Es un acto sin pregunta (§4.1). DT-P32-03 §3.1: ese respaldo se resuelve por
        // idioma antes de llamar al redactor, para que el hilo nunca reciba el cierre de otro idioma.
        var cierreCampania = await ResolverMensajeCierreAsync(
            conversacion, campania, numero, emisor, "cierreColaCoaching", ahora, cancellationToken);
        if (cierreCampania is null)
        {
            return;
        }

        var cierre = await ComponerTurnoAsync(
            campania, pregunta, usuario.Id, usuario.WhatsappNormalizado, ActoConversacional.Cerrar,
            respaldo: cierreCampania, ahora, cancellationToken, idioma: conversacion.Idioma);
        var texto = Combinar(prefijo, cierre);
        await EnviarAsync(conversacion, numero, texto, TipoEnvioMensaje.Cierre, emisor, ahora, cancellationToken);
        conversacion = conversacion.Cerrar(ahora);
        await _conversaciones.GuardarConversacionAsync(conversacion, cancellationToken);
        await RegistrarCoachingAsync(
            usuario.Id,
            usuario.WhatsappNormalizado,
            "finalizada",
            conversacion.CoachingIdeas!,
            null,
            ahora,
            cancellationToken);
        await EnviarSiguientePreguntaPendienteAsync(
            campania,
            usuario,
            pregunta,
            numero,
            emisor,
            ahora,
            cancellationToken);
    }

    private async Task<IReadOnlyList<string>> ConstruirHistorialIdeaAsync(
        string campaniaId,
        string ideaRaizId,
        CancellationToken cancellationToken)
    {
        var respuestas = await _respuestas.ListarRespuestasAsync(campaniaId, cancellationToken);
        return respuestas
            .Where(respuesta => respuesta.IdeaRaizId == ideaRaizId)
            .OrderBy(respuesta => respuesta.RevisionIndice)
            .TakeLast(8)
            .Select(respuesta => "Participante: " + Acotar(respuesta.Texto, 300))
            .ToArray();
    }

    /// <summary>
    /// I-19 §11.1: la consolidación no tiene opt-in por campaña; solo depende del kill-switch global y de
    /// que el consolidador esté inyectado.
    /// </summary>
    private bool ConsolidacionIdeasActiva => _consolidacionProgresivaHabilitada && _consolidadorIdeas is not null;

    private bool CoachingEfectivo(Campania campania)
        => _segmentacionIdeasHabilitada
            && campania.ConfigConversacional.SegmentacionIdeas
            && _coachingSecuencialIdeasHabilitado
            && campania.ConfigConversacional.CoachingSecuencialIdeas;

    private Task RegistrarCoachingAsync(
        string usuarioId,
        NumeroWhatsApp numero,
        string accion,
        CoachingIdeas cola,
        MotivoFinalizacionIdea? motivo,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        var ideaIndice = cola.IdeaActivaIndice ?? cola.Ideas.LastOrDefault()?.IdeaIndice ?? 0;
        var revision = cola.IdeaActiva?.RepreguntasUsadas ?? 0;
        var detalle = FormattableString.Invariant(
            $"accion:{accion};ideaIndice:{ideaIndice};ideasTotal:{cola.Ideas.Count};revision:{revision};motivo:{(motivo is null ? "ninguno" : MinusculaInicial(motivo.Value.ToString()))}");
        return _logSeguridad.RegistrarAsync(
            LogSeguridad.Crear(
                "log_" + Guid.NewGuid().ToString("N"),
                TipoEventoSeguridad.CoachingSecuencialIdeas,
                usuarioId,
                numero.Valor,
                accion,
                detalle,
                _correlacion.CorrelationIdActual,
                ahora),
            cancellationToken);
    }

    private static string MinusculaInicial(string valor)
        => char.ToLowerInvariant(valor[0]) + valor[1..];

    private async Task<IdeasResueltas> ResolverIdeasAsync(
        ContextoEvaluacion contexto,
        string textoOriginal,
        CancellationToken cancellationToken)
    {
        ResultadoSegmentacionIdeas resultado;
        try
        {
            resultado = await _segmentadorIdeas.SegmentarAsync(
                new ContextoSegmentacionIdeas(
                    contexto.Campania,
                    contexto.Pregunta,
                    textoOriginal,
                    contexto.HistorialReciente,
                    contexto.ConfigLlmSnapshot)
                {
                    Idioma = contexto.Idioma,
                    TextoPreguntaEfectivo = contexto.TextoPreguntaEfectivo,
                },
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return IdeasResueltas.CrearFallback(textoOriginal, "error_segmentador", uso: null);
        }

        if (resultado is ResultadoSegmentacionIdeas.Fallback fallback)
        {
            return IdeasResueltas.CrearFallback(textoOriginal, fallback.Motivo, fallback.Uso);
        }

        var exito = (ResultadoSegmentacionIdeas.Exito)resultado;
        var textosVistos = new HashSet<string>(StringComparer.Ordinal);
        var ideasValidas = exito.Ideas
            .Select(idea => idea.Texto.Trim())
            .Where(texto => texto.Length >= _longitudMinimaIdea)
            .Where(texto => textosVistos.Add(NormalizarTextoIdea(texto)))
            .ToArray();
        if (ideasValidas.Length == 0)
        {
            return IdeasResueltas.CrearFallback(textoOriginal, "sin_ideas_validas", exito.Uso);
        }

        var truncada = ideasValidas.Length > _maxIdeasPorMensaje;
        var ideas = ideasValidas
            .Take(_maxIdeasPorMensaje)
            .Select((texto, indice) => new IdeaSegmentada(indice + 1, texto, Resumen: null))
            .ToArray();
        return new IdeasResueltas(ideas, true, false, truncada, null, exito.Uso);
    }

    private Task RegistrarSegmentacionAsync(
        Usuario usuario,
        IdeasResueltas resolucion,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        var uso = resolucion.Uso;
        var detalle = FormattableString.Invariant(
            $"ideas:{resolucion.Ideas.Count};fallback:{resolucion.Fallback};truncada:{resolucion.Truncada};motivo:{resolucion.Motivo ?? "ninguno"};promptTokens:{uso?.PromptTokens ?? 0};completionTokens:{uso?.CompletionTokens ?? 0}");
        return _logSeguridad.RegistrarAsync(
            LogSeguridad.Crear(
                "log_" + Guid.NewGuid().ToString("N"),
                TipoEventoSeguridad.SegmentacionIdeas,
                usuario.Id,
                usuario.WhatsappNormalizado.Valor,
                resolucion.Fallback ? "fallback" : "segmentada",
                detalle,
                _correlacion.CorrelationIdActual,
                ahora),
            cancellationToken);
    }

    private static string NormalizarTextoIdea(string texto)
        => string.Join(' ', texto.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();

    private static string CrearRespuestaIdIdea(string respuestaPadreId, int ideaIndice)
    {
        var normalizado = new string(respuestaPadreId
            .Select(caracter => char.IsAsciiLetterOrDigit(caracter) ? char.ToLowerInvariant(caracter) : '_')
            .ToArray())
            .Trim('_');
        return "resp_" + (normalizado.Length == 0 ? "mensaje" : normalizado) + "_" + ideaIndice;
    }

    private static string ConfirmacionIdeas(int cantidad)
        => cantidad == 1 ? "Registramos tu idea." : $"Registramos {cantidad} ideas de tu mensaje.";

    private async Task<HiloTrabajo?> ResolverHiloTrabajoAsync(
        Campania campania,
        string usuarioId,
        Pregunta preguntaFallback,
        CancellationToken cancellationToken)
    {
        var preguntas = PreguntasActivasOrdenadas(campania);
        if (preguntas.Count == 0)
        {
            return null;
        }

        var conversaciones = await _conversaciones.ListarConversacionesAsync(campania.Id, cancellationToken);
        var conversacionesUsuario = conversaciones
            .Where(conversacion => conversacion.UsuarioId == usuarioId)
            .GroupBy(conversacion => conversacion.PreguntaId, StringComparer.Ordinal)
            .ToDictionary(
                grupo => grupo.Key,
                grupo => grupo.OrderByDescending(conversacion => conversacion.FechaInicio).First(),
                StringComparer.Ordinal);

        foreach (var pregunta in preguntas)
        {
            if (!conversacionesUsuario.TryGetValue(pregunta.Id, out var conversacion))
            {
                return new HiloTrabajo(pregunta, CrearConversacionId(campania.Id, usuarioId, pregunta.Id), null);
            }

            if (conversacion.Estado != EstadoConversacion.Cerrada)
            {
                return new HiloTrabajo(pregunta, conversacion.Id, conversacion);
            }
        }

        if (preguntas.Any(pregunta => pregunta.Id == preguntaFallback.Id))
        {
            return null;
        }

        return new HiloTrabajo(preguntaFallback, CrearConversacionId(campania.Id, usuarioId, preguntaFallback.Id), null);
    }

    private async Task ResponderPrimerContactoAsync(
        string conversacionId,
        Campania campania,
        Usuario usuario,
        Pregunta pregunta,
        NumeroWhatsApp numero,
        MensajeEntrante mensaje,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        // Crea el hilo y lo deja en esperandoRespuestaInicial (no avanza a Evaluando) renovando la ventana.
        var conversacion = DominioConversacion
            .Iniciar(conversacionId, campania.Id, usuario.Id, pregunta.Id, Canal, null, ahora, idioma: usuario.Idioma)
            .RegistrarEntrante(mensaje.Timestamp);

        await GuardarMensajeAsync(conversacion, DireccionMensaje.In, mensaje.Texto, mensaje.WhatsappMessageId, mensaje.Timestamp, cancellationToken);

        var contenido = ResolverContenidoCampania(campania, conversacion.IdiomaInterno, preguntaId: pregunta.Id);
        var texto = Combinar(
            await ResolverSaludoPrimerContactoAsync(campania, usuario, conversacion, contenido, cancellationToken),
            TextoPregunta(contenido, pregunta.Id));
        await EnviarAsync(conversacion, numero, texto, TipoEnvioMensaje.Inicial, mensaje.PhoneNumberIdDestino, ahora, cancellationToken);

        await _conversaciones.GuardarConversacionAsync(conversacion, cancellationToken);
    }

    /// <summary>
    /// Saludo del primer entrante: el <see cref="MensajeInicial"/> activo guardado en la BD de la
    /// campania (renderizado con las variables del usuario). Si la campania no tiene mensaje inicial
    /// activo, cae al texto configurable <c>Conversacion:Mensajes:SaludoPrimerContacto</c> para no
    /// enviar un saludo vacio. La plantilla de Meta del primer contacto proactivo es independiente.
    /// </summary>
    private async Task<string> ResolverSaludoPrimerContactoAsync(
        Campania campania,
        Usuario usuario,
        DominioConversacion conversacion,
        ContenidoCampaniaEfectivo? contenido,
        CancellationToken cancellationToken)
    {
        var mensajeInicial = RenderizadorMensaje.MensajeInicialActivo(campania);
        if (mensajeInicial is not null
            && contenido?.MensajesIniciales.TryGetValue(mensajeInicial.Id, out var mensajeEfectivo) == true)
        {
            var texto = RenderizadorMensaje.Reemplazar(
                mensajeEfectivo.Texto,
                RenderizadorMensaje.ConstruirVariables(usuario, campania, contenido.Nombre));
            if (!string.IsNullOrWhiteSpace(texto))
            {
                return texto.Trim();
            }
        }

        return await TextoGlobalAsync(
            conversacion,
            "saludoPrimerContacto",
            TextoConfigurado(_mensajes.SaludoPrimerContacto, OpcionesMensajesConversacion.SaludoPrimerContactoDefault),
            cancellationToken);
    }

    private async Task EnviarSiguientePreguntaPendienteAsync(
        Campania campania,
        Usuario usuario,
        Pregunta preguntaActual,
        NumeroWhatsApp numero,
        string? emisor,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        var siguiente = await ResolverSiguientePreguntaSinHiloAsync(campania, usuario.Id, preguntaActual, cancellationToken);
        if (siguiente is null)
        {
            return;
        }

        var conversacionId = CrearConversacionId(campania.Id, usuario.Id, siguiente.Id);
        var conversacion = DominioConversacion.Iniciar(
            conversacionId,
            campania.Id,
            usuario.Id,
            siguiente.Id,
            Canal,
            null,
            ahora,
            idioma: usuario.Idioma);
        var contenido = ResolverContenidoCampania(campania, conversacion.IdiomaInterno, preguntaId: siguiente.Id);
        var texto = Combinar(
            await TextoGlobalAsync(
                conversacion,
                "saludoSiguientePregunta",
                TextoConfigurado(_mensajes.SaludoSiguientePregunta, OpcionesMensajesConversacion.SaludoSiguientePreguntaDefault),
                cancellationToken),
            TextoPregunta(contenido, siguiente.Id));

        await EnviarAsync(conversacion, numero, texto, TipoEnvioMensaje.Inicial, emisor, ahora, cancellationToken);
        await _conversaciones.GuardarConversacionAsync(conversacion, cancellationToken);
    }

    private async Task<Pregunta?> ResolverSiguientePreguntaSinHiloAsync(
        Campania campania,
        string usuarioId,
        Pregunta preguntaActual,
        CancellationToken cancellationToken)
    {
        var preguntas = PreguntasActivasOrdenadas(campania);
        var indiceActual = preguntas.FindIndex(pregunta => pregunta.Id == preguntaActual.Id);
        if (indiceActual < 0 || indiceActual == preguntas.Count - 1)
        {
            return null;
        }

        var conversaciones = await _conversaciones.ListarConversacionesAsync(campania.Id, cancellationToken);
        var preguntasConHilo = conversaciones
            .Where(conversacion => conversacion.UsuarioId == usuarioId)
            .Select(conversacion => conversacion.PreguntaId)
            .ToHashSet(StringComparer.Ordinal);

        return preguntas
            .Skip(indiceActual + 1)
            .FirstOrDefault(pregunta => !preguntasConHilo.Contains(pregunta.Id));
    }

    private async Task CerrarConAgradecimientoAsync(
        DominioConversacion conversacion,
        NumeroWhatsApp numero,
        Campania campania,
        string? acusePrevio,
        string? emisor,
        DateTimeOffset ahora,
        CancellationToken cancellationToken,
        bool omitirIdeaVisible = false)
    {
        var cierreCampania = await ResolverMensajeCierreAsync(
            conversacion, campania, numero, emisor, "cierreConAgradecimiento", ahora, cancellationToken);
        if (cierreCampania is null)
        {
            return;
        }

        var texto = string.IsNullOrWhiteSpace(acusePrevio)
            ? cierreCampania
            : Combinar(acusePrevio, cierreCampania);
        if (!omitirIdeaVisible)
        {
            texto = await AgregarIdeaVisibleAlCierreAsync(conversacion, campania, texto, cancellationToken);
        }
        await EnviarAsync(conversacion, numero, texto, TipoEnvioMensaje.Cierre, emisor, ahora, cancellationToken);

        var cerrada = conversacion.Cerrar(ahora);
        await _conversaciones.GuardarConversacionAsync(cerrada, cancellationToken);
    }

    private async Task<string> AgregarIdeaVisibleAlCierreAsync(
        DominioConversacion conversacion,
        Campania campania,
        string cierre,
        CancellationToken cancellationToken)
    {
        if (!_visibilidadIdeaParticipanteHabilitada
            || !_consolidacionProgresivaHabilitada
            || !campania.ConfigConversacional.MostrarIdeaAlCerrar)
        {
            return cierre;
        }

        var idea = (await _respuestas.ListarIdeasConsolidadasAsync(campania.Id, cancellationToken))
            .Where(x => x.ConversacionId == conversacion.Id
                && x.EstadoResultado != EstadoResultadoIdeaConsolidada.Rechazada)
            .OrderByDescending(x => x.ActualizadaEn)
            .FirstOrDefault();
        var versionId = idea?.VersionConfirmadaRef ?? idea?.VersionPropuestaRef;
        if (string.IsNullOrWhiteSpace(versionId))
        {
            return cierre;
        }

        var version = await _respuestas.ObtenerVersionIdeaAsync(campania.Id, versionId, cancellationToken);
        if (version is null)
        {
            return cierre;
        }

        var encabezado = await TextoGlobalAsync(
            conversacion, "encabezadoCierreIdea",
            TextoConfigurado(_mensajes.EncabezadoCierreIdea, OpcionesMensajesConversacion.EncabezadoCierreIdeaDefault), cancellationToken);
        return Combinar(Combinar(encabezado, version.Texto), cierre);
    }

    // P-15 (CAL-001): la resolución del umbral (base/cierre/origen), la clasificación de madurez, el
    // valor de corte y la elegibilidad de mejora viven ahora en PoliticaLimitesConversacion (colaborador
    // determinista sin E/S); el orquestador solo la coordina vía _limites.

    // P-15 (CAL-001) Corte 3: la persistencia de evaluacion/respuesta, la compilacion de Markdown y los
    // registros de calibracion (madurez I-17, cierre por umbral I-01, reclasificacion por rechazo I-17
    // §5.4) viven ahora en ProcesadorResultadoEvaluacion; el orquestador los coordina via _procesador.

    /// <summary>
    /// I-09 tejido colectivo (05 §4.8, 08 §3.2): recupera aportes anonimizados de otros participantes,
    /// arma el bloque de dato no confiable (sanitizado + presupuestado) y lo adjunta al contexto. La
    /// recuperacion <b>nunca</b> bloquea el hilo: ante error o sin aportes devuelve el contexto sin
    /// tejido (conversacion autocontenida). Registra telemetria de aportes/latencia/degradacion y, si
    /// un aporte traia un patron de inyeccion, <c>PromptInjectionSospechoso</c> (08 §5.9).
    /// </summary>
    private async Task<ContextoEvaluacion> AplicarTejidoColectivoAsync(
        ContextoEvaluacion contexto,
        Usuario usuario,
        string conversacionId,
        string textoConsulta,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        var inicio = _tiempo.GetTimestamp();
        IReadOnlyList<AporteRelevante> aportes;
        try
        {
            aportes = await _baseConocimiento.RecuperarAsync(
                contexto.Campania.Id,
                textoConsulta,
                usuario.Tags,
                usuario.Id,
                conversacionId,
                _topKAportes,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // Degradacion limpia: un fallo de recuperacion no rompe el hilo (05 §4.8).
            await RegistrarTejidoAsync(usuario, recuperados: 0, tejidos: 0, error: true, LatenciaMs(inicio), ahora, cancellationToken);
            return contexto;
        }

        var bloque = ConstructorBloqueAportes.Construir(aportes, _presupuestoTokensTejido);
        if (bloque.InyeccionSospechosa)
        {
            await RegistrarPromptInjectionTejidoAsync(usuario, ahora, cancellationToken);
        }

        await RegistrarTejidoAsync(usuario, aportes.Count, bloque.Lineas.Count, error: false, LatenciaMs(inicio), ahora, cancellationToken);

        return bloque.TieneAportes ? contexto with { AportesComunidad = bloque.Lineas } : contexto;
    }

    private long LatenciaMs(long inicio)
        => (long)_tiempo.GetElapsedTime(inicio).TotalMilliseconds;

    // I-09: telemetria operativa del tejido (10 §6.2). El detalle NO contiene resumenes ni texto:
    // solo conteos (recuperados/tejidos), degradacion, error y latencia de recuperacion, para medir
    // el criterio de salida (costo/latencia por conversacion) en staging bajo flag.
    private Task RegistrarTejidoAsync(
        Usuario usuario,
        int recuperados,
        int tejidos,
        bool error,
        long latenciaMs,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
        => _logSeguridad.RegistrarAsync(
            LogSeguridad.Crear(
                "log_" + Guid.NewGuid().ToString("N"),
                TipoEventoSeguridad.TejidoColectivo,
                usuario.Id,
                usuario.WhatsappNormalizado.Valor,
                error ? "error" : tejidos > 0 ? "tejida" : "autocontenida",
                FormattableString.Invariant(
                    $"recuperados:{recuperados};tejidos:{tejidos};error:{error};latenciaMs:{latenciaMs}"),
                _correlacion.CorrelationIdActual,
                ahora),
            cancellationToken);

    private Task RegistrarPromptInjectionTejidoAsync(
        Usuario usuario,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
        => _logSeguridad.RegistrarAsync(
            LogSeguridad.Crear(
                "log_" + Guid.NewGuid().ToString("N"),
                TipoEventoSeguridad.PromptInjectionSospechoso,
                usuario.Id,
                usuario.WhatsappNormalizado.Valor,
                "neutralizado",
                "tejido_colectivo:aporte_con_patron_inyeccion",
                _correlacion.CorrelationIdActual,
                ahora),
            cancellationToken);

    /// <summary>
    /// DT-P32-03 §3.1: única puerta al cierre configurado de la campaña. Devuelve el texto resuelto o
    /// <c>null</c> cuando la localización del idioma del hilo está incompleta; en ese caso la ruta ya
    /// quedó cerrada con el manejo tipificado de configuración no disponible y el llamador debe salir
    /// sin componer el mensaje ni avanzar a la siguiente pregunta.
    /// </summary>
    private async Task<string?> ResolverMensajeCierreAsync(
        DominioConversacion conversacion,
        Campania campania,
        NumeroWhatsApp numero,
        string? emisor,
        string ruta,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        var resultado = _resolutorContenidoCampania.Resolver(
            new ContextoLocalizacion(campania, conversacion.IdiomaInterno, _opcionesCatalogoTextos.Habilitado)
            {
                CorrelationId = _correlacion.CorrelationIdActual,
            });
        if (resultado is ResultadoContenidoCampania.Disponible disponible)
        {
            return disponible.Contenido.MensajeCierre;
        }

        var noDisponible = (ResultadoContenidoCampania.NoDisponible)resultado;
        await RegistrarCierreLocalizadoNoDisponibleAsync(
            conversacion, numero, noDisponible, ruta, ahora, cancellationToken);
        await CerrarPorConfiguracionNoDisponibleAsync(conversacion, numero, emisor, ahora, cancellationToken);
        return null;
    }

    /// <summary>
    /// DT-P32-03 §5: deja rastro de campaña, idioma, ruta y código. Nunca copia el texto de cierre ni
    /// valores de configuración.
    /// </summary>
    private Task RegistrarCierreLocalizadoNoDisponibleAsync(
        DominioConversacion conversacion,
        NumeroWhatsApp numero,
        ResultadoContenidoCampania.NoDisponible resultado,
        string ruta,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
        => _logSeguridad.RegistrarAsync(
            LogSeguridad.Crear(
                "log_" + Guid.NewGuid().ToString("N"),
                TipoEventoSeguridad.AnomaliaLlm,
                conversacion.UsuarioId,
                numero.Valor,
                "fallback",
                // Conserva el contrato observable de DT-P32-03: cualquier ausencia de cierre para el
                // hilo se registra como localización incompleta, aunque el resolutor transversal
                // distinga internamente idioma no habilitado de contenido incompleto.
                $"cierre_localizado:{ResolutorMensajeCierreCampania.CodigoLocalizacionIncompleta}:idioma={resultado.Idioma.Codigo}:ruta={ruta}",
                _correlacion.CorrelationIdActual,
                ahora,
                conversacion.CampaniaId),
            cancellationToken);

    private async Task CerrarPorConfiguracionNoDisponibleAsync(
        DominioConversacion conversacion,
        NumeroWhatsApp numero,
        string? emisor,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        await EnviarAsync(
            conversacion,
            numero,
            await TextoGlobalAsync(
                conversacion,
                "mensajeConfiguracionNoDisponible",
                TextoConfigurado(_mensajes.MensajeConfiguracionNoDisponible, OpcionesMensajesConversacion.MensajeConfiguracionNoDisponibleDefault),
                cancellationToken),
            TipoEnvioMensaje.Cierre,
            emisor,
            ahora,
            cancellationToken);

        var cerrada = conversacion.Cerrar(ahora);
        await _conversaciones.GuardarConversacionAsync(cerrada, cancellationToken);
    }

    private async Task CerrarNeutroAsync(
        DominioConversacion conversacion,
        NumeroWhatsApp numero,
        Campania campania,
        string? emisor,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        var cierreCampania = await ResolverMensajeCierreAsync(
            conversacion, campania, numero, emisor, "cierreNeutro", ahora, cancellationToken);
        if (cierreCampania is null)
        {
            return;
        }

        // DT-P32-04: el cierre neutro ya viene localizado por idioma del hilo; anteponer la constante
        // espanola RetroNeutra producia mezcla de idiomas en hilos `en` (hallazgo del piloto 2026-08-20).
        var texto = cierreCampania;
        await EnviarAsync(conversacion, numero, texto, TipoEnvioMensaje.Cierre, emisor, ahora, cancellationToken);

        var cerrada = conversacion.Cerrar(ahora);
        await _conversaciones.GuardarConversacionAsync(cerrada, cancellationToken);
    }

    private async Task<ContextoDisponible> ConstruirContextoAsync(
        Campania campania,
        Pregunta pregunta,
        Usuario usuario,
        string conversacionId,
        string respuestaId,
        string texto,
        CancellationToken cancellationToken)
    {
        var rubricaRef = pregunta.RubricaRef ?? campania.RubricaRef;
        var promptRef = ResolverPromptRef(pregunta.PromptRefs, campania.PromptRefs, "evaluar");
        if (string.IsNullOrWhiteSpace(rubricaRef) || string.IsNullOrWhiteSpace(promptRef) || string.IsNullOrWhiteSpace(campania.ConfigLlmRef))
        {
            return ContextoDisponible.NoDisponible("referencias_configuracion_incompletas");
        }

        var rubrica = await _configuracion.ObtenerUltimaRubricaAsync(rubricaRef, cancellationToken);
        // DT-I20-02 §5.4: la version mas nueva ACTIVA Y APROBADA de la familia, no la ultima por
        // numero. Asi, inactivar la ultima version devuelve el flujo a la anterior vigente y el
        // rollback del runbook es confiable; los motivos de diagnostico no cambian.
        var promptRuntime = await _configuracion.ObtenerPromptVigenteAsync(promptRef, cancellationToken);
        var configLlm = await _configuracion.ObtenerConfigLlmAsync(campania.ConfigLlmRef, cancellationToken);
        if (rubrica is null)
        {
            return ContextoDisponible.NoDisponible("rubrica_no_encontrada");
        }

        if (rubrica.Estado != EstadoRubrica.Activa)
        {
            return ContextoDisponible.NoDisponible("rubrica_no_activa");
        }

        if (promptRuntime.Prompt is not { } prompt)
        {
            return ContextoDisponible.NoDisponible(
                promptRuntime.Motivo ?? ResolucionPromptRuntime.MotivoNoEncontrado);
        }

        if (configLlm is null)
        {
            return ContextoDisponible.NoDisponible("config_llm_no_encontrada");
        }

        if (configLlm.Estado != EstadoRegistro.Activo)
        {
            return ContextoDisponible.NoDisponible("config_llm_no_activa");
        }

        var idiomaConversacion = IdiomaConversacion.Espanol;
        if (_opcionesCatalogoTextos.Habilitado)
        {
            var conversacion = await _conversaciones.ObtenerConversacionAsync(
                campania.Id, conversacionId, cancellationToken);
            idiomaConversacion = conversacion?.IdiomaInterno ?? usuario.IdiomaInterno;
        }

        var resultadoContenido = _resolutorContenidoCampania.Resolver(
            new ContextoLocalizacion(campania, idiomaConversacion, _opcionesCatalogoTextos.Habilitado)
            {
                PreguntaId = pregunta.Id,
                CorrelationId = _correlacion.CorrelationIdActual,
            });
        if (resultadoContenido is not ResultadoContenidoCampania.Disponible contenidoDisponible
            || !contenidoDisponible.Contenido.Preguntas.TryGetValue(pregunta.Id, out var preguntaEfectiva))
        {
            return ContextoDisponible.NoDisponible("localizacion_campania_incompleta");
        }

        var contenido = contenidoDisponible.Contenido;
        var historial = await ConstruirHistorialAsync(campania.Id, conversacionId, cancellationToken);

        return ContextoDisponible.Disponible(
            new ContextoEvaluacion(
                campania,
                pregunta,
                usuario,
                respuestaId,
                texto,
                historial,
                rubrica,
                prompt,
                configLlm)
            {
                ContenidoCampaniaEfectivo = contenido,
                Idioma = contenido.Idioma.Codigo,
                NombreCampaniaEfectivo = contenido.Nombre,
                ObjetivoCampaniaEfectivo = contenido.Objetivo,
                TextoPreguntaEfectivo = preguntaEfectiva.Texto,
                InstruccionPreguntaEfectiva = preguntaEfectiva.Instruccion,
            });
    }

    private ContenidoCampaniaEfectivo? ResolverContenidoCampania(
        Campania campania,
        IdiomaConversacion idioma,
        string? preguntaId = null,
        string? mensajeInicialId = null)
    {
        var resultado = _resolutorContenidoCampania.Resolver(
            new ContextoLocalizacion(campania, idioma, _opcionesCatalogoTextos.Habilitado)
            {
                PreguntaId = preguntaId,
                MensajeInicialId = mensajeInicialId,
                CorrelationId = _correlacion.CorrelationIdActual,
            });
        return resultado is ResultadoContenidoCampania.Disponible disponible
            ? disponible.Contenido
            : null;
    }

    private static string TextoPregunta(ContenidoCampaniaEfectivo? contenido, string preguntaId)
        => contenido?.Preguntas.TryGetValue(preguntaId, out var pregunta) == true
            ? pregunta.Texto
            : string.Empty;

    /// <summary>
    /// Historial reciente del hilo (turnos previos persistidos) para que el LLM vea la conversacion y
    /// no repita preguntas/retro ni entre en bucles. Se excluye el ultimo entrante (la respuesta que se
    /// esta evaluando ahora, que ya viaja como <c>RESPUESTA_DEL_USUARIO</c>) y se acota en turnos y largo.
    /// </summary>
    private async Task<IReadOnlyList<string>> ConstruirHistorialAsync(
        string campaniaId,
        string conversacionId,
        CancellationToken cancellationToken)
    {
        const int maxTurnos = 8;
        const int maxCaracteresPorTurno = 300;

        var mensajes = await _conversaciones.ListarMensajesAsync(campaniaId, conversacionId, cancellationToken);
        if (mensajes.Count == 0)
        {
            return Array.Empty<string>();
        }

        var ordenados = mensajes.OrderBy(mensaje => mensaje.Timestamp).ToList();
        var ultimoEntrante = ordenados.FindLastIndex(mensaje => mensaje.Direccion == DireccionMensaje.In);
        if (ultimoEntrante >= 0)
        {
            ordenados.RemoveAt(ultimoEntrante);
        }

        return ordenados
            .TakeLast(maxTurnos)
            .Select(mensaje =>
                (mensaje.Direccion == DireccionMensaje.In ? "Participante: " : "El Tejido: ")
                + Acotar(mensaje.Texto, maxCaracteresPorTurno))
            .ToList();
    }

    /// <summary>
    /// ¿El usuario ya consumio su cupo de mensajes entrantes en la campania?
    /// (<c>Campania.ConfigSeguridad.MaxMensajesPorUsuario</c>, 10 §2). Cuenta los <c>Mensaje(in)</c>
    /// ya persistidos en los hilos del usuario; el entrante actual (aun sin persistir) seria el excedente.
    /// </summary>
    private async Task<bool> CupoMensajesExcedidoAsync(
        Campania campania,
        string usuarioId,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        var maximo = campania.ConfigSeguridad.MaxMensajesPorUsuario;
        if (maximo <= 0)
        {
            return false;
        }

        // P-26 §9: en campaña continua el cupo por participante mira una ventana movil de 24 h (los
        // ciclos y preguntas de la campania la comparten); sin continuidad conserva el acumulado.
        var desde = VentanaCuposDesde(campania, ahora);
        var conversaciones = await _conversaciones.ListarConversacionesAsync(campania.Id, cancellationToken);
        var total = 0;
        foreach (var conversacion in conversaciones.Where(c => c.UsuarioId == usuarioId))
        {
            var mensajes = await _conversaciones.ListarMensajesAsync(campania.Id, conversacion.Id, cancellationToken);
            total += mensajes.Count(m =>
                m.Direccion == DireccionMensaje.In && (desde is null || m.Timestamp >= desde.Value));
            if (total >= maximo)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// ¿El hilo ya alcanzo el techo duro de turnos entrantes (<c>Conversacion:MaxTurnosPorHilo</c>)?
    /// Garantiza la terminacion de cualquier hilo con independencia del LLM. 0 o negativo desactiva.
    /// </summary>
    private async Task<bool> TurnosHiloExcedidosAsync(
        DominioConversacion conversacion,
        CancellationToken cancellationToken)
    {
        if (_maxTurnosPorHilo <= 0)
        {
            return false;
        }

        var mensajes = await _conversaciones.ListarMensajesAsync(conversacion.CampaniaId, conversacion.Id, cancellationToken);
        return mensajes.Count(m => m.Direccion == DireccionMensaje.In) >= _maxTurnosPorHilo;
    }

    /// <summary>
    /// ¿El usuario ya consumio su cupo de llamadas al LLM en la campania?
    /// (<c>Campania.ConfigSeguridad.MaxLlamadasLlmPorUsuario</c>, 10 §2). Evaluación, consolidación y
    /// clasificación P-27 dejan un rastro persistente propio, por lo que las tres clases consumen el
    /// mismo cupo sin documentos contadores nuevos.
    /// </summary>
    private async Task<bool> CupoLlamadasLlmExcedidoAsync(
        Campania campania,
        string usuarioId,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
        => await _guardaCuposLlm.CupoLlamadasExcedidoAsync(
            campania, usuarioId, ahora, ConsolidacionIdeasActiva, cancellationToken);

    /// <summary>
    /// P-26 §9 — inicio de la ventana movil de cupos por participante: 24 h atras en campañas con
    /// <c>participacionContinua=true</c>; <c>null</c> (acumulado historico, comportamiento actual) en
    /// las demas. La ventana es movil: no se reinicia a medianoche y la comparten ciclos y preguntas
    /// de la misma campaña. No aplica a <c>presupuestoTokensCampania</c> ni a <c>MaxTurnosPorHilo</c>.
    /// </summary>
    private static DateTimeOffset? VentanaCuposDesde(Campania campania, DateTimeOffset ahora)
        => campania.ConfigConversacional.ParticipacionContinua
            ? ahora.ToUniversalTime().AddHours(-HorasVentanaCuposContinua)
            : null;

    /// <summary>
    /// P-10 — ¿La campania ya consumio su presupuesto de tokens LLM?
    /// (<c>Campania.ConfigSeguridad.PresupuestoTokensCampania</c>, 10 §2). El acumulado se deriva de la
    /// suma de tokens de evaluaciones y de clasificaciones P-27 (sin documentos contadores nuevos).
    /// 0 = desactivado.
    /// </summary>
    private async Task<bool> PresupuestoTokensExcedidoAsync(Campania campania, CancellationToken cancellationToken)
        => await _guardaCuposLlm.PresupuestoTokensExcedidoAsync(campania, cancellationToken);

    private Task RegistrarRateLimitAsync(
        Usuario usuario,
        string motivo,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
        => _logSeguridad.RegistrarAsync(
            LogSeguridad.Crear(
                "log_" + Guid.NewGuid().ToString("N"),
                TipoEventoSeguridad.RateLimit,
                usuario.Id,
                usuario.WhatsappNormalizado.Valor,
                "rechazado",
                motivo,
                _correlacion.CorrelationIdActual,
                ahora),
            cancellationToken);

    /// <summary>
    /// P-26 §10: telemetría del enrutamiento emitida desde el orquestador (<c>cicloNuevo</c> y
    /// <c>reapertura</c>), complemento de la que emite el servicio de enrutamiento. Solo ids y
    /// conteos: nunca el texto del participante ni nombres de campaña/pregunta.
    /// </summary>
    private Task RegistrarEnrutamientoAsync(
        Usuario usuario,
        string accion,
        string detalle,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
        => _logSeguridad.RegistrarAsync(
            LogSeguridad.Crear(
                "log_" + Guid.NewGuid().ToString("N"),
                TipoEventoSeguridad.EnrutamientoParticipacion,
                usuario.Id,
                usuario.WhatsappNormalizado.Valor,
                accion,
                detalle,
                _correlacion.CorrelationIdActual,
                ahora),
            cancellationToken);

    private Task RegistrarDespertarProactivoAsync(
        Usuario usuario,
        string resultado,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
        => _logSeguridad.RegistrarAsync(
            LogSeguridad.Crear(
                "log_" + Guid.NewGuid().ToString("N"),
                TipoEventoSeguridad.DespertarProactivo,
                usuario.Id,
                usuario.WhatsappNormalizado.Valor,
                resultado,
                "accion:reactivacion",
                _correlacion.CorrelationIdActual,
                ahora),
            cancellationToken);

    private Task RegistrarConfiguracionNoDisponibleAsync(
        Usuario usuario,
        string motivo,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
        => _logSeguridad.RegistrarAsync(
            LogSeguridad.Crear(
                "log_" + Guid.NewGuid().ToString("N"),
                TipoEventoSeguridad.AnomaliaLlm,
                usuario.Id,
                usuario.WhatsappNormalizado.Valor,
                "fallback",
                motivo,
                _correlacion.CorrelationIdActual,
                ahora),
            cancellationToken);

    private async Task<EnvioResultado> EnviarAsync(
        DominioConversacion conversacion,
        NumeroWhatsApp numero,
        string texto,
        TipoEnvioMensaje tipo,
        string? emisor,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        // MVP: dentro de la ventana de servicio (siempre abierta tras un entrante) se usa texto libre (05 §2.2).
        var resultado = await _gateway.EnviarTextoAsync(numero.Valor, texto, tipo, cancellationToken, emisor);

        await GuardarMensajeAsync(conversacion, DireccionMensaje.Out, texto, resultado.WhatsappMessageId, ahora, cancellationToken);

        var envio = EnvioMensaje.Crear(
            "env_" + Guid.NewGuid().ToString("N"),
            conversacion.CampaniaId,
            conversacion.UsuarioId,
            mensajeInicialId: null,
            numero,
            resultado.Exito ? EstadoEnvio.Enviado : EstadoEnvio.Error,
            tipo,
            resultado.WhatsappMessageId,
            ahora,
            resultado.Error);
        await _participantes.RegistrarEnvioAsync(envio, cancellationToken);
        return resultado;
    }

    private async Task<string> GuardarMensajeAsync(
        DominioConversacion conversacion,
        DireccionMensaje direccion,
        string texto,
        string? whatsappMessageId,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        var mensajePersistido = Mensaje.Crear(
            "msg_" + Guid.NewGuid().ToString("N"),
            conversacion.CampaniaId,
            conversacion.Id,
            direccion,
            texto,
            whatsappMessageId,
            timestamp);
        await _conversaciones.GuardarMensajeAsync(mensajePersistido, cancellationToken);
        return mensajePersistido.Id;
    }

    private Task MarcarParticipanteRespondioAsync(
        ParticipanteCampania participante,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        var actualizado = ParticipanteCampania.Crear(
            participante.Id,
            participante.CampaniaId,
            participante.UsuarioId,
            participante.WhatsappNormalizado,
            participante.Estado,
            participante.EstadoEnvio,
            EstadoRespuestaParticipante.Respondio,
            participante.FechaInclusion,
            participante.FechaPrimerEnvio,
            ahora);
        return _participantes.GuardarParticipanteAsync(actualizado, cancellationToken);
    }

    private static string? ResolverPromptRef(
        IReadOnlyDictionary<string, string>? preguntaRefs,
        IReadOnlyDictionary<string, string>? campaniaRefs,
        string tipo)
    {
        if (preguntaRefs is not null && preguntaRefs.TryGetValue(tipo, out var refPregunta) && !string.IsNullOrWhiteSpace(refPregunta))
        {
            return refPregunta;
        }

        if (campaniaRefs is not null && campaniaRefs.TryGetValue(tipo, out var refCampania) && !string.IsNullOrWhiteSpace(refCampania))
        {
            return refCampania;
        }

        return null;
    }

    /// <summary>
    /// Arma la invitacion a mejorar de forma conversacional y variada (Opcion B): el nucleo es la
    /// <c>RepreguntaSugerida</c> del LLM cuando existe (natural y distinta cada turno) y, si no, una
    /// variante de respaldo rotada; siempre se anexa una coletilla rotada que ensena la salida del "no
    /// quiero mejorar" (ej. "asi esta bien"), para que el participante nunca quede atrapado.
    /// </summary>
    private async Task<string> ConstruirInvitacionMejoraAsync(
        DominioConversacion conversacion,
        string? repreguntaSugerida,
        CancellationToken cancellationToken)
    {
        var semilla = SemillaVariante(conversacion);
        var nucleo = string.IsNullOrWhiteSpace(repreguntaSugerida)
            ? await SeleccionarInvitacionMejoraRespaldoAsync(conversacion, semilla, cancellationToken)
            : repreguntaSugerida!.Trim();

        var coletilla = SeleccionarVariante(
            await FrasesGlobalesAsync(
                conversacion,
                "invitacionContinuarVariantes",
                _mensajes.InvitacionContinuarVariantes,
                OpcionesMensajesConversacion.InvitacionContinuarVariantesDefault,
                cancellationToken),
            semilla);

        return string.IsNullOrWhiteSpace(coletilla) ? nucleo : nucleo + "\n\n" + coletilla;
    }

    private async Task<string> SeleccionarInvitacionMejoraRespaldoAsync(
        DominioConversacion conversacion,
        int semilla,
        CancellationToken cancellationToken)
    {
        var elegido = SeleccionarVariante(
            await FrasesGlobalesAsync(
                conversacion,
                "invitacionMejoraVariantes",
                _mensajes.InvitacionMejoraVariantes,
                new[] { TextoConfigurado(_mensajes.InvitacionMejora, OpcionesMensajesConversacion.InvitacionMejoraDefault) },
                cancellationToken),
            semilla);
        return string.IsNullOrWhiteSpace(elegido)
            ? await TextoGlobalAsync(
                conversacion,
                "invitacionMejora",
                TextoConfigurado(_mensajes.InvitacionMejora, OpcionesMensajesConversacion.InvitacionMejoraDefault),
                cancellationToken)
            : elegido!;
    }

    private async Task<string> SeleccionarAcuseContinuarAsync(
        DominioConversacion conversacion,
        CancellationToken cancellationToken)
    {
        var elegido = SeleccionarVariante(
            await FrasesGlobalesAsync(
                conversacion,
                "acuseContinuarVariantes",
                _mensajes.AcuseContinuarVariantes,
                new[] { TextoConfigurado(_mensajes.AcuseContinuar, OpcionesMensajesConversacion.AcuseContinuarDefault) },
                cancellationToken),
            SemillaVariante(conversacion));
        return string.IsNullOrWhiteSpace(elegido)
            ? await TextoGlobalAsync(
                conversacion,
                "acuseContinuar",
                TextoConfigurado(_mensajes.AcuseContinuar, OpcionesMensajesConversacion.AcuseContinuarDefault),
                cancellationToken)
            : elegido!;
    }

    private async Task<string> TextoGlobalAsync(
        DominioConversacion conversacion,
        string clave,
        string legado,
        CancellationToken cancellationToken)
    {
        if (_resolutorTextos is null)
        {
            return legado;
        }

        var textos = await _resolutorTextos.ResolverAsync(conversacion, cancellationToken);
        return textos.Mensajes.TryGetValue(clave, out var texto) && !string.IsNullOrWhiteSpace(texto)
            ? texto.Trim()
            : legado;
    }

    private async Task<string> TextoGlobalParaIdiomaAsync(
        string idioma,
        string clave,
        string legado,
        CancellationToken cancellationToken)
    {
        if (_resolutorTextos is null)
        {
            return legado;
        }

        var textos = await _resolutorTextos.ResolverParaIdiomaAsync(idioma, cancellationToken);
        return textos.Mensajes.TryGetValue(clave, out var texto) && !string.IsNullOrWhiteSpace(texto)
            ? texto.Trim()
            : legado;
    }

    private async Task<IReadOnlyList<string>> FrasesGlobalesAsync(
        DominioConversacion conversacion,
        string clave,
        IReadOnlyList<string>? configuradas,
        IReadOnlyList<string> porDefecto,
        CancellationToken cancellationToken)
    {
        if (_resolutorTextos is null)
        {
            return configuradas is { Count: > 0 } ? configuradas : porDefecto;
        }

        var textos = await _resolutorTextos.ResolverAsync(conversacion, cancellationToken);
        return textos.Frases.TryGetValue(clave, out var frases) && frases.Count > 0
            ? frases.ToArray()
            : configuradas is { Count: > 0 }
                ? configuradas
                : porDefecto;
    }

    /// <summary>
    /// P-32: agrupa los detectores de un turno bajo el snapshot de idioma del hilo. Si el adaptador
    /// no está conectado o un doble de prueba aporta un catálogo parcial, se conserva exactamente la
    /// política heredada; un catálogo activo real ya se valida completo antes de llegar aquí.
    /// </summary>
    private async Task<DetectoresConversacion> ResolverDetectoresAsync(
        DominioConversacion conversacion,
        CancellationToken cancellationToken)
    {
        var legado = new DetectoresConversacion(
            _transicion,
            _intencionConfirmacion,
            _intencionSolicitarMejora,
            _intencionRechazoIdea,
            _intencionRevisitarAnterior,
            _intencionRevisitarIdea,
            _politicaIntencionControl);
        if (_resolutorTextos is null)
        {
            return legado;
        }

        var textos = await _resolutorTextos.ResolverAsync(conversacion, cancellationToken);
        if (!textos.Frases.TryGetValue("continuar", out var continuar)
            || continuar.Count == 0
            || !textos.Frases.TryGetValue("confirmar", out var confirmar)
            || confirmar.Count == 0
            || !textos.Frases.TryGetValue("solicitarMejora", out var solicitarMejora)
            || solicitarMejora.Count == 0
            || !textos.Frases.TryGetValue("rechazoGuardado", out var rechazoGuardado)
            || rechazoGuardado.Count == 0
            || !textos.Frases.TryGetValue("revisitarAnterior", out var revisitarAnterior)
            || revisitarAnterior.Count == 0
            || !textos.Frases.TryGetValue("revisitarIdea", out var revisitarIdea)
            || revisitarIdea.Count == 0
            || !textos.Frases.TryGetValue("finalizarIdea", out var finalizarIdea)
            || finalizarIdea.Count == 0
            || !textos.Frases.TryGetValue("finalizarParticipacion", out var finalizarParticipacion)
            || finalizarParticipacion.Count == 0)
        {
            return legado;
        }

        var detectorContinuar = new DetectorIntencionContinuar(continuar, _maxCaracteresIntencion);
        var detectorRechazo = new DetectorIntencionContinuar(rechazoGuardado, _maxCaracteresIntencion);
        return new DetectoresConversacion(
            new ResolvedorTransicionConversacion(detectorContinuar, detectorRechazo),
            new DetectorIntencionContinuar(confirmar, _maxCaracteresIntencion),
            new DetectorIntencionContinuar(solicitarMejora, _maxCaracteresIntencion),
            detectorRechazo,
            new DetectorIntencionContinuar(revisitarAnterior, _maxCaracteresIntencion),
            new DetectorIntencionContinuar(revisitarIdea, _maxCaracteresIntencion),
            new PoliticaIntencionControl(finalizarIdea, finalizarParticipacion, _maxCaracteresIntencionControl));
    }

    /// <summary>Elige una variante de la lista configurada o, si esta vacia, de la lista por defecto.</summary>
    private static string? SeleccionarVariante(IReadOnlyList<string>? variantes, IReadOnlyList<string> porDefecto, int semilla)
        => SeleccionarVariante(variantes is { Count: > 0 } ? variantes : porDefecto, semilla);

    /// <summary>Seleccion deterministica (reproducible y testeable) de una variante por la semilla del hilo.</summary>
    private static string? SeleccionarVariante(IReadOnlyList<string>? variantes, int semilla)
    {
        if (variantes is null || variantes.Count == 0)
        {
            return null;
        }

        var indice = ((semilla % variantes.Count) + variantes.Count) % variantes.Count;
        return variantes[indice];
    }

    /// <summary>
    /// Semilla determinista para rotar variantes: combina el id del hilo (varia entre participantes/
    /// preguntas) y las repreguntas usadas (varia entre turnos del mismo hilo).
    /// </summary>
    private static int SemillaVariante(DominioConversacion conversacion)
        => HashEstable(conversacion.Id) + conversacion.RepreguntasUsadas;

    private static int HashEstable(string texto)
    {
        unchecked
        {
            var hash = 17;
            foreach (var caracter in texto)
            {
                hash = (hash * 31) + caracter;
            }

            return hash & 0x7fffffff;
        }
    }

    private static string Acotar(string texto, int maximo)
        => texto.Length > maximo ? texto[..maximo] : texto;

    private static string Combinar(string? primero, string segundo)
        => string.IsNullOrWhiteSpace(primero) ? segundo : primero.Trim() + "\n\n" + segundo;

    /// <summary>
    /// DT-I20-01 §4.3: al ensamblar dos fragmentos visibles que vienen del LLM (retro, repregunta,
    /// invitación), el segundo se omite si repite una oración del primero — en ese caso su contenido ya
    /// está a la vista. Conservador: nunca descarta el fragmento validado, solo el añadido redundante.
    /// </summary>
    private static string CombinarSinDuplicar(string? primero, string segundo)
        => FiltroDuplicacionTurno.RepiteUnaOracion(segundo, primero)
            ? primero!.Trim()
            : Combinar(primero, segundo);

    private static string TextoConfigurado(string? valor, string fallback)
        => string.IsNullOrWhiteSpace(valor) ? fallback : valor.Trim();

    private static List<Pregunta> PreguntasActivasOrdenadas(Campania campania)
        => campania.Preguntas
            .Where(pregunta => pregunta.Estado == EstadoRegistro.Activo)
            .OrderBy(pregunta => pregunta.Orden)
            .ThenBy(pregunta => pregunta.Id, StringComparer.Ordinal)
            .ToList();

    private static string CrearConversacionId(string campaniaId, string usuarioId, string preguntaId)
        => $"conv_{campaniaId}_{usuarioId}_{preguntaId}";

    /// <summary>
    /// P-26 §5.8: ¿el mensaje es una petición explícita de complementar/revisitar una idea que vive en
    /// el hilo ya cerrado? Solo entonces se reabre ese hilo en vez de abrir un ciclo nuevo. Exige que
    /// la consolidación I-19 esté activa y que existan ideas cerradas reabribles: sin candidatas la
    /// frase no tendría a qué volver y el aporte sigue el camino normal (ciclo nuevo).
    /// </summary>
    private async Task<bool> PideReaperturaEnHiloCerradoAsync(
        DominioConversacion cerrada,
        Campania campania,
        string texto,
        CancellationToken cancellationToken)
    {
        if (!ConsolidacionIdeasActiva || campania.Estado != EstadoCampania.Activa)
        {
            return false;
        }

        var detectores = await ResolverDetectoresAsync(cerrada, cancellationToken);
        if (!detectores.RevisitarAnterior.Coincide(texto) && !detectores.RevisitarIdea.Coincide(texto))
        {
            return false;
        }

        var candidatas = await CandidatasReaperturaAsync(cerrada, campania.Id, cancellationToken);
        return candidatas.Count > 0;
    }

    /// <summary>
    /// P-26 §5.7: id determinista de un ciclo posterior, derivado tambien del mensaje raiz (hash
    /// corto, porque el wamid de Meta puede traer caracteres invalidos para un id de Cosmos). Un
    /// reintento con el mismo mensaje produce el mismo id y no duplica el ciclo.
    /// </summary>
    private static string CrearConversacionIdCiclo(
        string campaniaId,
        string usuarioId,
        string preguntaId,
        string mensajeRaizId)
    {
        var hash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(mensajeRaizId)))[..12]
            .ToLowerInvariant();
        return $"{CrearConversacionId(campaniaId, usuarioId, preguntaId)}_c{hash}";
    }

    /// <summary>Resultado de intentar resolver una lista de selección ya ofrecida (I-19 §4.7).</summary>
    private sealed record ResultadoSeleccionIdea(bool Manejado, DominioConversacion Conversacion);

    private sealed record DetectoresConversacion(
        ResolvedorTransicionConversacion Transicion,
        DetectorIntencionContinuar Confirmacion,
        DetectorIntencionContinuar SolicitarMejora,
        DetectorIntencionContinuar RechazoIdea,
        DetectorIntencionContinuar RevisitarAnterior,
        DetectorIntencionContinuar RevisitarIdea,
        PoliticaIntencionControl PoliticaIntencionControl);

    /// <summary>
    /// Versión propuesta para una idea, más las ideas nuevas que el consolidador separó (I-19 §4.6) y la
    /// telemetría de esa llamada: si degradó a fallback y qué tokens consumió (§12.2/§12.3).
    /// </summary>
    private sealed record PropuestaConsolidada(
        VersionIdeaConsolidada Version,
        IReadOnlyList<NuevaIdeaDetectada> NuevasIdeas,
        bool EsFallback,
        UsoTokensLlm? Uso,
        /// <summary>§4.2: si el aporte fue ambiguo, la pregunta breve que hay que hacer en vez de adivinar.</summary>
        string? PreguntaAclaracion);

    private sealed record HiloTrabajo(Pregunta Pregunta, string ConversacionId, DominioConversacion? Conversacion);

    private sealed record IdeasResueltas(
        IReadOnlyList<IdeaSegmentada> Ideas,
        bool FueSegmentada,
        bool Fallback,
        bool Truncada,
        string? Motivo,
        UsoTokensLlm? Uso)
    {
        public static IdeasResueltas CrearFallback(string texto, string motivo, UsoTokensLlm? uso)
            => new(new[] { new IdeaSegmentada(1, texto, Resumen: null) }, false, true, false, motivo, uso);
    }

    private sealed record ContextoDisponible(ContextoEvaluacion? Contexto, string? Motivo)
    {
        public static ContextoDisponible Disponible(ContextoEvaluacion contexto) => new(contexto, null);

        public static ContextoDisponible NoDisponible(string motivo) => new(null, motivo);
    }

}
