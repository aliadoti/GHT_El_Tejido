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
    private readonly ICompiladorMarkdown _compilador;
    private readonly IWhatsAppGateway _gateway;
    private readonly IRepositorioLogSeguridad _logSeguridad;
    private readonly IProveedorCorrelacion _correlacion;
    private readonly OpcionesMensajesConversacion _mensajes;
    private readonly DetectorIntencionContinuar _intencionContinuar;
    private readonly double _umbralCierreAnticipado;
    private readonly TimeProvider _tiempo;

    public OrquestadorConversacion(
        IRepositorioConversaciones conversaciones,
        IRepositorioRespuestas respuestas,
        IRepositorioParticipantes participantes,
        IRepositorioConfiguracion configuracion,
        IEvaluadorLlm evaluador,
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
        _compilador = compilador;
        _gateway = gateway;
        _logSeguridad = logSeguridad;
        _correlacion = correlacion;
        _mensajes = opciones.Mensajes;
        _umbralCierreAnticipado = opciones.UmbralCierreAnticipado;
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

        await GuardarMensajeAsync(conversacion, DireccionMensaje.In, mensaje.Texto, mensaje.WhatsappMessageId, mensaje.Timestamp, cancellationToken);

        await MarcarParticipanteRespondioAsync(participante.Participante, ahora, cancellationToken);

        var respuestaId = "resp_" + Guid.NewGuid().ToString("N");

        if (revisionesAgotadas || deseaContinuar)
        {
            // Se agoto el cupo de revisiones, o el participante pidio continuar: se registra sin evaluar
            // y se cierra. Si pidio continuar, se antepone un acuse calido para que no se sienta cortante.
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
            var acuse = deseaContinuar
                ? TextoConfigurado(_mensajes.AcuseContinuar, OpcionesMensajesConversacion.AcuseContinuarDefault)
                : null;
            await CerrarConAgradecimientoAsync(conversacion, numero, campania, acuse, ahora, cancellationToken);
            await EnviarSiguientePreguntaPendienteAsync(campania, usuario, pregunta, numero, ahora, cancellationToken);
            return;
        }

        conversacion = conversacion.RegistrarEntrante(mensaje.Timestamp).AvanzarA(EstadoMaquinaConversacion.Evaluando);
        await _conversaciones.GuardarConversacionAsync(conversacion, cancellationToken);

        var contexto = await ConstruirContextoAsync(campania, pregunta, usuario, respuestaId, mensaje.Texto, cancellationToken);

        if (contexto.Contexto is null)
        {
            // Sin configuracion completa (rubrica/prompt/configLLM) no se puede evaluar: se informa
            // un problema operativo al participante y se cierra sin llamar al LLM.
            await RegistrarConfiguracionNoDisponibleAsync(usuario, contexto.Motivo ?? "configuracion_no_disponible", ahora, cancellationToken);
            await GuardarRespuestaAsync(respuestaId, campania.Id, usuario, pregunta, conversacionId, mensaje.Texto, esRepregunta, EstadoRespuesta.EvaluacionPendiente, ahora, cancellationToken);
            await CerrarPorConfiguracionNoDisponibleAsync(conversacion, numero, ahora, cancellationToken);
            return;
        }

        var resultado = await _evaluador.EvaluarAsync(contexto.Contexto, cancellationToken);
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
            var invitacionMejora = TextoConfigurado(_mensajes.InvitacionMejora, OpcionesMensajesConversacion.InvitacionMejoraDefault);
            var invitacion = string.IsNullOrWhiteSpace(evaluacion.RepreguntaSugerida)
                ? invitacionMejora
                : evaluacion.RepreguntaSugerida!.Trim() + "\n\n" + invitacionMejora;
            var texto = Combinar(evaluacion.RetroalimentacionEnviada, invitacion);
            await EnviarAsync(conversacion, numero, texto, TipoEnvioMensaje.Repregunta, ahora, cancellationToken);

            conversacion = conversacion.RegistrarRepregunta();
            await _conversaciones.GuardarConversacionAsync(conversacion, cancellationToken);
            return;
        }

        // Cierre: retro + agradecimiento en un solo mensaje (tipo Cierre). Si cerro por calificacion
        // alta se intercala una felicitacion para que el corte temprano se sienta natural.
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

        var texto = Combinar(TextoConfigurado(_mensajes.SaludoPrimerContacto, OpcionesMensajesConversacion.SaludoPrimerContactoDefault), pregunta.Texto);
        await EnviarAsync(conversacion, numero, texto, TipoEnvioMensaje.Inicial, ahora, cancellationToken);

        await _conversaciones.GuardarConversacionAsync(conversacion, cancellationToken);
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

        var fraccion = (decimal)Math.Min(_umbralCierreAnticipado, 1.0);
        var valorUmbral = escala.Min + (fraccion * (escala.Max - escala.Min));
        return calificacionTotal >= valorUmbral;
    }

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

        return ContextoDisponible.Disponible(
            new ContextoEvaluacion(
                campania,
                pregunta,
                usuario,
                respuestaId,
                texto,
                Array.Empty<string>(),
                rubrica,
                prompt,
                configLlm));
    }

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

    private Task GuardarMensajeAsync(
        DominioConversacion conversacion,
        DireccionMensaje direccion,
        string texto,
        string? whatsappMessageId,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
        => _conversaciones.GuardarMensajeAsync(
            Mensaje.Crear(
                "msg_" + Guid.NewGuid().ToString("N"),
                conversacion.CampaniaId,
                conversacion.Id,
                direccion,
                texto,
                whatsappMessageId,
                timestamp),
            cancellationToken);

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
        CancellationToken cancellationToken)
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
                usuario.Tags),
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

    private sealed record ContextoDisponible(ContextoEvaluacion? Contexto, string? Motivo)
    {
        public static ContextoDisponible Disponible(ContextoEvaluacion contexto) => new(contexto, null);

        public static ContextoDisponible NoDisponible(string motivo) => new(null, motivo);
    }
}
