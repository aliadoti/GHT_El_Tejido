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

    private readonly IRepositorioConversaciones _conversaciones;
    private readonly IRepositorioRespuestas _respuestas;
    private readonly IRepositorioParticipantes _participantes;
    private readonly IRepositorioConfiguracion _configuracion;
    private readonly IEvaluadorLlm _evaluador;
    private readonly ISegmentadorIdeas _segmentadorIdeas;
    private readonly IBaseConocimientoCampania _baseConocimiento;
    private readonly ICompiladorMarkdown _compilador;
    private readonly IWhatsAppGateway _gateway;
    private readonly IRepositorioLogSeguridad _logSeguridad;
    private readonly IProveedorCorrelacion _correlacion;
    private readonly OpcionesMensajesConversacion _mensajes;
    private readonly DetectorIntencionContinuar _intencionContinuar;
    private readonly double _umbralCierreAnticipado;
    private readonly bool _cuposHabilitados;
    private readonly int _maxTurnosPorHilo;
    private readonly bool _segmentacionIdeasHabilitada;
    private readonly int _maxIdeasPorMensaje;
    private readonly int _longitudMinimaIdea;
    private readonly bool _tejidoColectivoHabilitado;
    private readonly int _topKAportes;
    private readonly int _presupuestoTokensTejido;
    private readonly TimeProvider _tiempo;

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
        TimeProvider tiempo)
    {
        _conversaciones = conversaciones;
        _respuestas = respuestas;
        _participantes = participantes;
        _configuracion = configuracion;
        _evaluador = evaluador;
        _segmentadorIdeas = segmentadorIdeas;
        _baseConocimiento = baseConocimiento;
        _compilador = compilador;
        _gateway = gateway;
        _logSeguridad = logSeguridad;
        _correlacion = correlacion;
        _mensajes = opciones.Mensajes;
        _umbralCierreAnticipado = opciones.UmbralCierreAnticipado;
        _cuposHabilitados = opciones.CuposHabilitados;
        _maxTurnosPorHilo = opciones.MaxTurnosPorHilo;
        _segmentacionIdeasHabilitada = opciones.SegmentacionIdeas;
        _maxIdeasPorMensaje = Math.Max(1, opciones.MaxIdeasPorMensaje);
        _longitudMinimaIdea = Math.Max(1, opciones.LongitudMinimaIdea);
        _tejidoColectivoHabilitado = opciones.TejidoColectivo;
        _topKAportes = Math.Max(1, opciones.TopKAportes);
        _presupuestoTokensTejido = opciones.PresupuestoTokensTejido;
        IEnumerable<string> frases = opciones.FrasesContinuar is { Count: > 0 }
            ? opciones.FrasesContinuar
            : DetectorIntencionContinuar.FrasesPorDefecto;
        _intencionContinuar = new DetectorIntencionContinuar(frases, opciones.MaxCaracteresIntencionContinuar);
        _tiempo = tiempo;
    }

    public async Task ProcesarMensajeEntranteAsync(
        ParticipanteResuelto participante,
        MensajeEntrante mensaje,
        CancellationToken cancellationToken)
    {
        var usuario = participante.Usuario;
        var campania = participante.Campania;
        var numero = usuario.WhatsappNormalizado;
        var ahora = _tiempo.GetUtcNow();

        // Cupo de mensajes por usuario/campania (10 §2, Campania.ConfigSeguridad): al exceder, el
        // entrante se descarta con rechazo neutral silencioso (como una conversacion cerrada) y el
        // motivo queda solo en LogSeguridad. Gateado por Conversacion:CuposHabilitados (default off).
        if (_cuposHabilitados && await CupoMensajesExcedidoAsync(campania, usuario.Id, cancellationToken))
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

        conversacion ??= DominioConversacion.Iniciar(conversacionId, campania.Id, usuario.Id, pregunta.Id, Canal, null, ahora);
        var esRepregunta = conversacion.EstadoMaquina == EstadoMaquinaConversacion.EsperandoRepregunta;
        var revisionesAgotadas = esRepregunta && conversacion.RepreguntasUsadas >= pregunta.MaxRepreguntas;
        // Salida conversacional (05 §4.4): solo cuando ya ofrecimos una mejora (esperandoRepregunta) y el
        // participante senala que esta conforme; el primer mensaje (su respuesta real) nunca se interpreta asi.
        var deseaContinuar = esRepregunta && _intencionContinuar.DeseaContinuar(mensaje.Texto);

        // Techos deterministas (10 §2 / D2): el tope duro de turnos por hilo garantiza terminacion
        // aunque otras reglas pidan seguir; el cupo de llamadas LLM por usuario/campania evita costo
        // sin limite. Ambos cierran elegante con lo aportado (mismo camino que revisiones agotadas).
        var turnosExcedidos = !revisionesAgotadas && !deseaContinuar
            && await TurnosHiloExcedidosAsync(conversacion, cancellationToken);
        // Cupo LLM de la campania: por usuario (llamadas) o por presupuesto de tokens (P-10). Ambos
        // cierran la campania para el hilo (no se abre la siguiente pregunta); el motivo se distingue
        // en LogSeguridad. Gateados por Conversacion:CuposHabilitados.
        var evaluarCupoLlm = !revisionesAgotadas && !deseaContinuar && !turnosExcedidos && _cuposHabilitados;
        var cupoLlamadasUsuarioExcedido = evaluarCupoLlm
            && await CupoLlamadasLlmExcedidoAsync(campania, usuario.Id, cancellationToken);
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

        var respuestaId = "resp_" + Guid.NewGuid().ToString("N");

        if (revisionesAgotadas || deseaContinuar || turnosExcedidos || cupoLlmExcedido)
        {
            // Se agoto el cupo de revisiones/turnos/LLM, o el participante pidio continuar: se registra
            // sin evaluar y se cierra. Si pidio continuar, se antepone un acuse calido para que no se
            // sienta cortante. Los techos deterministas dejan ademas rastro RateLimit en LogSeguridad.
            conversacion = conversacion.RegistrarEntrante(mensaje.Timestamp);
            await GuardarRespuestaAsync(
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
                var motivo = turnosExcedidos
                    ? "tope_turnos_hilo"
                    : cupoLlamadasUsuarioExcedido ? "cupo_llamadas_llm_usuario" : "presupuesto_tokens_campania";
                await RegistrarRateLimitAsync(usuario, motivo, ahora, cancellationToken);
            }

            var acuse = deseaContinuar
                ? SeleccionarAcuseContinuar(conversacion)
                : null;
            await CerrarConAgradecimientoAsync(conversacion, numero, campania, acuse, ahora, cancellationToken);
            if (!cupoLlmExcedido)
            {
                // Con el cupo LLM de la campania agotado no tiene sentido abrir la siguiente pregunta
                // (tampoco podria evaluarse); en los demas cierres se avanza como siempre.
                await EnviarSiguientePreguntaPendienteAsync(campania, usuario, pregunta, numero, ahora, cancellationToken);
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
            await GuardarRespuestaAsync(respuestaId, campania.Id, usuario, pregunta, conversacionId, mensaje.Texto, esRepregunta, EstadoRespuesta.EvaluacionPendiente, ahora, cancellationToken);
            await CerrarPorConfiguracionNoDisponibleAsync(conversacion, numero, ahora, cancellationToken);
            return;
        }

        // I-09 tejido colectivo (05 §4.8): si la campania lo activa y el kill-switch global no lo apaga,
        // se enriquece el contexto con aportes anonimizados de otros participantes ANTES de evaluar. La
        // recuperacion nunca bloquea el hilo: sin aportes o ante error degrada a autocontenido.
        var contextoEval = contexto.Contexto;
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
                contextoEval,
                mensaje.Texto,
                respuestaPadreId,
                esRepregunta,
                ahora,
                cancellationToken);
            return;
        }

        var resultado = await _evaluador.EvaluarAsync(contextoEval, cancellationToken);
        await _respuestas.GuardarEvaluacionAsync(resultado.Evaluacion, cancellationToken);

        var esFallback = resultado is ResultadoEvaluacion.Fallback;
        await GuardarRespuestaAsync(
            respuestaId, campania.Id, usuario, pregunta, conversacionId, mensaje.Texto, esRepregunta,
            esFallback ? EstadoRespuesta.EvaluacionPendiente : EstadoRespuesta.Evaluada, ahora, cancellationToken);

        var evaluacion = resultado.Evaluacion;

        // El Markdown se compila por cada evaluacion valida (cada intento queda con su artefacto;
        // el ultimo es el definitivo). En fallback no se compila (08 §6).
        if (!esFallback)
        {
            await CompilarMarkdownAsync(campania.Id, pregunta, usuario.Id, respuestaId, cancellationToken);
        }

        // Cierre anticipado por calificacion alta (05 §4.4): si la calificacion supera el umbral
        // configurado, no se insiste con una revision aunque queden repreguntas; se felicita y cierra.
        var calificacionAlta = !esFallback && UmbralAlcanzado(evaluacion.CalificacionTotal, contexto.Contexto.RubricaSnapshot.Escala);

        // Mejora deterministica (05 §4.4): tras una evaluacion valida se ofrece una revision
        // (hasta MaxRepreguntas, default 1) con la retro como base. Si el siguiente mensaje llega
        // con el cupo agotado, se registra sin evaluarlo y se cierra con agradecimiento.
        var ofrecerMejora = !esFallback && !calificacionAlta && conversacion.RepreguntasUsadas < pregunta.MaxRepreguntas;
        if (ofrecerMejora)
        {
            var invitacion = ConstruirInvitacionMejora(conversacion, evaluacion.RepreguntaSugerida);
            var texto = Combinar(evaluacion.RetroalimentacionEnviada, invitacion);
            await EnviarAsync(conversacion, numero, texto, TipoEnvioMensaje.Repregunta, ahora, cancellationToken);

            conversacion = conversacion.RegistrarRepregunta();
            await _conversaciones.GuardarConversacionAsync(conversacion, cancellationToken);
            return;
        }

        // Cierre: retro + agradecimiento en un solo mensaje (tipo Cierre). Si cerro por calificacion
        // alta se intercala una felicitacion para que el corte temprano se sienta natural.
        if (calificacionAlta)
        {
            var escala = contexto.Contexto.RubricaSnapshot.Escala;
            await RegistrarCierreUmbralAsync(
                usuario, evaluacion.CalificacionTotal, ValorUmbral(escala), escala, ahora, cancellationToken);
        }

        var cierreFinal = calificacionAlta
            ? Combinar(
                TextoConfigurado(_mensajes.MensajeCalificacionAlta, OpcionesMensajesConversacion.MensajeCalificacionAltaDefault),
                campania.ConfigConversacional.MensajeCierre)
            : campania.ConfigConversacional.MensajeCierre;
        var cierre = Combinar(evaluacion.RetroalimentacionEnviada, cierreFinal);
        await EnviarAsync(conversacion, numero, cierre, TipoEnvioMensaje.Cierre, ahora, cancellationToken);

        conversacion = conversacion.Cerrar(ahora);
        await _conversaciones.GuardarConversacionAsync(conversacion, cancellationToken);

        await EnviarSiguientePreguntaPendienteAsync(campania, usuario, pregunta, numero, ahora, cancellationToken);
    }

    private async Task ProcesarIdeasSegmentadasAsync(
        DominioConversacion conversacion,
        Campania campania,
        Usuario usuario,
        Pregunta pregunta,
        NumeroWhatsApp numero,
        ContextoEvaluacion contextoBase,
        string textoOriginal,
        string respuestaPadreId,
        bool esRepregunta,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        var resolucion = await ResolverIdeasAsync(contextoBase, textoOriginal, cancellationToken);
        await RegistrarSegmentacionAsync(usuario, resolucion, ahora, cancellationToken);

        var resultados = new List<(ResultadoEvaluacion Resultado, ContextoEvaluacion Contexto)>();
        foreach (var idea in resolucion.Ideas)
        {
            var respuestaId = resolucion.FueSegmentada
                ? CrearRespuestaIdIdea(respuestaPadreId, idea.Indice)
                : "resp_" + Guid.NewGuid().ToString("N");
            var contexto = contextoBase with { RespuestaId = respuestaId, RespuestaTexto = idea.Texto };
            var resultado = await _evaluador.EvaluarAsync(contexto, cancellationToken);
            await _respuestas.GuardarEvaluacionAsync(resultado.Evaluacion, cancellationToken);

            var esFallback = resultado is ResultadoEvaluacion.Fallback;
            await GuardarRespuestaAsync(
                respuestaId,
                campania.Id,
                usuario,
                pregunta,
                conversacion.Id,
                idea.Texto,
                esRepregunta,
                esFallback ? EstadoRespuesta.EvaluacionPendiente : EstadoRespuesta.Evaluada,
                ahora,
                cancellationToken,
                resolucion.FueSegmentada ? idea.Indice : null,
                resolucion.FueSegmentada ? respuestaPadreId : null);

            if (!esFallback)
            {
                await CompilarMarkdownAsync(campania.Id, pregunta, usuario.Id, respuestaId, cancellationToken);
            }

            resultados.Add((resultado, contexto));
        }

        if (resultados.Any(resultado => resultado.Resultado is ResultadoEvaluacion.Fallback))
        {
            await CerrarNeutroAsync(conversacion, numero, campania, ahora, cancellationToken);
            return;
        }

        foreach (var resultado in resultados)
        {
            if (UmbralAlcanzado(resultado.Resultado.Evaluacion.CalificacionTotal, resultado.Contexto.RubricaSnapshot.Escala))
            {
                await RegistrarCierreUmbralAsync(
                    usuario,
                    resultado.Resultado.Evaluacion.CalificacionTotal,
                    ValorUmbral(resultado.Contexto.RubricaSnapshot.Escala),
                    resultado.Contexto.RubricaSnapshot.Escala,
                    ahora,
                    cancellationToken);
            }
        }

        // Una respuesta al participante por turno: las evaluaciones y Markdown quedan individualizados
        // para resultados, pero el hilo conserva su limite de repreguntas por pregunta.
        var calificacionAlta = resultados.All(resultado =>
            UmbralAlcanzado(resultado.Resultado.Evaluacion.CalificacionTotal, resultado.Contexto.RubricaSnapshot.Escala));
        var confirmacion = ConfirmacionIdeas(resolucion.Ideas.Count);
        var ofrecerMejora = !calificacionAlta && conversacion.RepreguntasUsadas < pregunta.MaxRepreguntas;
        if (ofrecerMejora)
        {
            var texto = Combinar(confirmacion, ConstruirInvitacionMejora(conversacion, repreguntaSugerida: null));
            await EnviarAsync(conversacion, numero, texto, TipoEnvioMensaje.Repregunta, ahora, cancellationToken);
            conversacion = conversacion.RegistrarRepregunta();
            await _conversaciones.GuardarConversacionAsync(conversacion, cancellationToken);
            return;
        }

        var cierreFinal = calificacionAlta
            ? Combinar(
                TextoConfigurado(_mensajes.MensajeCalificacionAlta, OpcionesMensajesConversacion.MensajeCalificacionAltaDefault),
                campania.ConfigConversacional.MensajeCierre)
            : campania.ConfigConversacional.MensajeCierre;
        await EnviarAsync(conversacion, numero, Combinar(confirmacion, cierreFinal), TipoEnvioMensaje.Cierre, ahora, cancellationToken);

        conversacion = conversacion.Cerrar(ahora);
        await _conversaciones.GuardarConversacionAsync(conversacion, cancellationToken);
        await EnviarSiguientePreguntaPendienteAsync(campania, usuario, pregunta, numero, ahora, cancellationToken);
    }

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
                    contexto.ConfigLlmSnapshot),
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
            .Iniciar(conversacionId, campania.Id, usuario.Id, pregunta.Id, Canal, null, ahora)
            .RegistrarEntrante(mensaje.Timestamp);

        await GuardarMensajeAsync(conversacion, DireccionMensaje.In, mensaje.Texto, mensaje.WhatsappMessageId, mensaje.Timestamp, cancellationToken);

        var texto = Combinar(ResolverSaludoPrimerContacto(campania, usuario), pregunta.Texto);
        await EnviarAsync(conversacion, numero, texto, TipoEnvioMensaje.Inicial, ahora, cancellationToken);

        await _conversaciones.GuardarConversacionAsync(conversacion, cancellationToken);
    }

    /// <summary>
    /// Saludo del primer entrante: el <see cref="MensajeInicial"/> activo guardado en la BD de la
    /// campania (renderizado con las variables del usuario). Si la campania no tiene mensaje inicial
    /// activo, cae al texto configurable <c>Conversacion:Mensajes:SaludoPrimerContacto</c> para no
    /// enviar un saludo vacio. La plantilla de Meta del primer contacto proactivo es independiente.
    /// </summary>
    private string ResolverSaludoPrimerContacto(Campania campania, Usuario usuario)
    {
        var mensajeInicial = RenderizadorMensaje.MensajeInicialActivo(campania);
        if (mensajeInicial is not null)
        {
            var texto = RenderizadorMensaje.Renderizar(mensajeInicial, usuario, campania);
            if (!string.IsNullOrWhiteSpace(texto))
            {
                return texto.Trim();
            }
        }

        return TextoConfigurado(_mensajes.SaludoPrimerContacto, OpcionesMensajesConversacion.SaludoPrimerContactoDefault);
    }

    private async Task EnviarSiguientePreguntaPendienteAsync(
        Campania campania,
        Usuario usuario,
        Pregunta preguntaActual,
        NumeroWhatsApp numero,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        var siguiente = await ResolverSiguientePreguntaSinHiloAsync(campania, usuario.Id, preguntaActual, cancellationToken);
        if (siguiente is null)
        {
            return;
        }

        var conversacionId = CrearConversacionId(campania.Id, usuario.Id, siguiente.Id);
        var conversacion = DominioConversacion.Iniciar(conversacionId, campania.Id, usuario.Id, siguiente.Id, Canal, null, ahora);
        var texto = Combinar(TextoConfigurado(_mensajes.SaludoSiguientePregunta, OpcionesMensajesConversacion.SaludoSiguientePreguntaDefault), siguiente.Texto);

        await EnviarAsync(conversacion, numero, texto, TipoEnvioMensaje.Inicial, ahora, cancellationToken);
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
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        var texto = string.IsNullOrWhiteSpace(acusePrevio)
            ? campania.ConfigConversacional.MensajeCierre
            : Combinar(acusePrevio, campania.ConfigConversacional.MensajeCierre);
        await EnviarAsync(conversacion, numero, texto, TipoEnvioMensaje.Cierre, ahora, cancellationToken);

        var cerrada = conversacion.Cerrar(ahora);
        await _conversaciones.GuardarConversacionAsync(cerrada, cancellationToken);
    }

    /// <summary>
    /// ¿La calificacion total alcanza el umbral de cierre anticipado? El umbral es una fraccion de la
    /// escala de la rubrica en [0,1]; <c>&lt;= 0</c> lo desactiva (default).
    /// </summary>
    private bool UmbralAlcanzado(decimal calificacionTotal, EscalaRubrica escala)
    {
        if (_umbralCierreAnticipado <= 0)
        {
            return false;
        }

        return calificacionTotal >= ValorUmbral(escala);
    }

    /// <summary>Valor absoluto del umbral en la escala de la rubrica (fraccion acotada a [0,1]).</summary>
    private decimal ValorUmbral(EscalaRubrica escala)
    {
        var fraccion = (decimal)Math.Min(_umbralCierreAnticipado, 1.0);
        return escala.Min + (fraccion * (escala.Max - escala.Min));
    }

    // I-01: telemetria de calibracion del cierre anticipado. Se registra en LogSeguridad (consultable,
    // 10 §6.2/§6.4) cada vez que el umbral dispara, con el score y el valor de corte (sin PII de texto).
    // Permite dimensionar el umbral en staging: cuantos cierres tempranos y a que calificacion.
    private Task RegistrarCierreUmbralAsync(
        Usuario usuario,
        decimal calificacionTotal,
        decimal valorUmbral,
        EscalaRubrica escala,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
        => _logSeguridad.RegistrarAsync(
            LogSeguridad.Crear(
                "log_" + Guid.NewGuid().ToString("N"),
                TipoEventoSeguridad.CierreUmbralAnticipado,
                usuario.Id,
                usuario.WhatsappNormalizado.Valor,
                "cierre_anticipado",
                FormattableString.Invariant(
                    $"umbral:{_umbralCierreAnticipado:0.###};score:{calificacionTotal};valor:{valorUmbral};escala:{escala.Min}-{escala.Max}"),
                _correlacion.CorrelationIdActual,
                ahora),
            cancellationToken);

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

    private async Task CerrarPorConfiguracionNoDisponibleAsync(
        DominioConversacion conversacion,
        NumeroWhatsApp numero,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        await EnviarAsync(
            conversacion,
            numero,
            TextoConfigurado(
                _mensajes.MensajeConfiguracionNoDisponible,
                OpcionesMensajesConversacion.MensajeConfiguracionNoDisponibleDefault),
            TipoEnvioMensaje.Cierre,
            ahora,
            cancellationToken);

        var cerrada = conversacion.Cerrar(ahora);
        await _conversaciones.GuardarConversacionAsync(cerrada, cancellationToken);
    }

    private async Task CerrarNeutroAsync(
        DominioConversacion conversacion,
        NumeroWhatsApp numero,
        Campania campania,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        var texto = Combinar(EvaluadorLlm.RetroNeutra, campania.ConfigConversacional.MensajeCierre);
        await EnviarAsync(conversacion, numero, texto, TipoEnvioMensaje.Cierre, ahora, cancellationToken);

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
        var prompt = await _configuracion.ObtenerUltimoPromptAsync(promptRef, cancellationToken);
        var configLlm = await _configuracion.ObtenerConfigLlmAsync(campania.ConfigLlmRef, cancellationToken);
        if (rubrica is null)
        {
            return ContextoDisponible.NoDisponible("rubrica_no_encontrada");
        }

        if (rubrica.Estado != EstadoRubrica.Activa)
        {
            return ContextoDisponible.NoDisponible("rubrica_no_activa");
        }

        if (prompt is null)
        {
            return ContextoDisponible.NoDisponible("prompt_no_encontrado");
        }

        if (prompt.Estado != EstadoPrompt.Activo)
        {
            return ContextoDisponible.NoDisponible("prompt_no_activo");
        }

        if (string.IsNullOrWhiteSpace(prompt.AprobadoPor) || prompt.FechaAprobacion is null)
        {
            return ContextoDisponible.NoDisponible("prompt_no_aprobado");
        }

        if (configLlm is null)
        {
            return ContextoDisponible.NoDisponible("config_llm_no_encontrada");
        }

        if (configLlm.Estado != EstadoRegistro.Activo)
        {
            return ContextoDisponible.NoDisponible("config_llm_no_activa");
        }

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
                configLlm));
    }

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
        CancellationToken cancellationToken)
    {
        var maximo = campania.ConfigSeguridad.MaxMensajesPorUsuario;
        if (maximo <= 0)
        {
            return false;
        }

        var conversaciones = await _conversaciones.ListarConversacionesAsync(campania.Id, cancellationToken);
        var total = 0;
        foreach (var conversacion in conversaciones.Where(c => c.UsuarioId == usuarioId))
        {
            var mensajes = await _conversaciones.ListarMensajesAsync(campania.Id, conversacion.Id, cancellationToken);
            total += mensajes.Count(m => m.Direccion == DireccionMensaje.In);
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
    /// (<c>Campania.ConfigSeguridad.MaxLlamadasLlmPorUsuario</c>, 10 §2). Cada llamada persiste una
    /// <c>Evaluacion</c> (valida o fallback), asi que el conteo de evaluaciones es el contador.
    /// </summary>
    private async Task<bool> CupoLlamadasLlmExcedidoAsync(
        Campania campania,
        string usuarioId,
        CancellationToken cancellationToken)
    {
        var maximo = campania.ConfigSeguridad.MaxLlamadasLlmPorUsuario;
        if (maximo <= 0)
        {
            return false;
        }

        var evaluaciones = await _respuestas.ContarEvaluacionesUsuarioAsync(campania.Id, usuarioId, cancellationToken);
        return evaluaciones >= maximo;
    }

    /// <summary>
    /// P-10 — ¿La campania ya consumio su presupuesto de tokens LLM?
    /// (<c>Campania.ConfigSeguridad.PresupuestoTokensCampania</c>, 10 §2). El acumulado se deriva de la
    /// suma de tokens de las evaluaciones (sin documentos contadores nuevos). 0 = desactivado.
    /// </summary>
    private async Task<bool> PresupuestoTokensExcedidoAsync(Campania campania, CancellationToken cancellationToken)
    {
        var presupuesto = campania.ConfigSeguridad.PresupuestoTokensCampania;
        if (presupuesto <= 0)
        {
            return false;
        }

        var consumidos = await _respuestas.SumarTokensCampaniaAsync(campania.Id, cancellationToken);
        return consumidos >= presupuesto;
    }

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

    private async Task CompilarMarkdownAsync(
        string campaniaId,
        Pregunta pregunta,
        string usuarioId,
        string respuestaId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _compilador.CompilarAsync(
                new SolicitudCompilacion(campaniaId, pregunta.ConfigMarkdown.TipoArtefacto, respuestaId, usuarioId, pregunta.Id),
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // El artefacto es regenerable desde datos (REQ §22.4.6); un fallo de compilacion no rompe el hilo.
        }
    }

    private async Task<EnvioResultado> EnviarAsync(
        DominioConversacion conversacion,
        NumeroWhatsApp numero,
        string texto,
        TipoEnvioMensaje tipo,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        // MVP: dentro de la ventana de servicio (siempre abierta tras un entrante) se usa texto libre (05 §2.2).
        var resultado = await _gateway.EnviarTextoAsync(numero.Valor, texto, tipo, cancellationToken);

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

    private Task GuardarRespuestaAsync(
        string respuestaId,
        string campaniaId,
        Usuario usuario,
        Pregunta pregunta,
        string conversacionId,
        string texto,
        bool esRepregunta,
        EstadoRespuesta estado,
        DateTimeOffset ahora,
        CancellationToken cancellationToken,
        int? ideaIndice = null,
        string? respuestaPadreId = null)
        => _respuestas.GuardarRespuestaAsync(
            RespuestaUsuario.Crear(
                respuestaId,
                campaniaId,
                usuario.Id,
                pregunta.Id,
                conversacionId,
                texto,
                Canal,
                esRepregunta,
                estado,
                ahora,
                usuario.Tags,
                ideaIndice,
                respuestaPadreId),
            cancellationToken);

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
    private string ConstruirInvitacionMejora(DominioConversacion conversacion, string? repreguntaSugerida)
    {
        var semilla = SemillaVariante(conversacion);
        var nucleo = string.IsNullOrWhiteSpace(repreguntaSugerida)
            ? SeleccionarInvitacionMejoraRespaldo(semilla)
            : repreguntaSugerida!.Trim();

        var coletilla = SeleccionarVariante(
            _mensajes.InvitacionContinuarVariantes,
            OpcionesMensajesConversacion.InvitacionContinuarVariantesDefault,
            semilla);

        return string.IsNullOrWhiteSpace(coletilla) ? nucleo : nucleo + "\n\n" + coletilla;
    }

    private string SeleccionarInvitacionMejoraRespaldo(int semilla)
    {
        var elegido = SeleccionarVariante(_mensajes.InvitacionMejoraVariantes, semilla);
        return string.IsNullOrWhiteSpace(elegido)
            ? TextoConfigurado(_mensajes.InvitacionMejora, OpcionesMensajesConversacion.InvitacionMejoraDefault)
            : elegido!;
    }

    private string SeleccionarAcuseContinuar(DominioConversacion conversacion)
    {
        var elegido = SeleccionarVariante(_mensajes.AcuseContinuarVariantes, SemillaVariante(conversacion));
        return string.IsNullOrWhiteSpace(elegido)
            ? TextoConfigurado(_mensajes.AcuseContinuar, OpcionesMensajesConversacion.AcuseContinuarDefault)
            : elegido!;
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
