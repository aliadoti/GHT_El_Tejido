using ElTejido.Application.Configuracion;
using ElTejido.Application.Evaluacion;
using ElTejido.Application.Identidad;
using ElTejido.Application.Markdown;
using ElTejido.Application.Participantes;
using ElTejido.Application.Respuestas;
using ElTejido.Application.WhatsApp;
using ElTejido.Domain.Campanas;
using ElTejido.Domain.Conversaciones;
using ElTejido.Domain.Identidad;
using ElTejido.Domain.Participantes;
using ElTejido.Domain.Respuestas;
using ElTejido.Domain.Usuarios;
using DominioConversacion = ElTejido.Domain.Conversaciones.Conversacion;
using RespuestaUsuario = ElTejido.Domain.Respuestas.Respuesta;

namespace ElTejido.Application.Conversacion;

/// <summary>
/// Orquestador conversacional (05 §4): gobierna la maquina de estados de un hilo a partir de un
/// mensaje entrante. Persiste Mensaje y Respuesta, evalua con el LLM (08), aplica el tope de
/// <b>1 repregunta</b> del MVP (05 §4.4) y, al cerrar, envia retro+cierre y compila el Markdown (09).
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
    private readonly TimeProvider _tiempo;

    public OrquestadorConversacion(
        IRepositorioConversaciones conversaciones,
        IRepositorioRespuestas respuestas,
        IRepositorioParticipantes participantes,
        IRepositorioConfiguracion configuracion,
        IEvaluadorLlm evaluador,
        ICompiladorMarkdown compilador,
        IWhatsAppGateway gateway,
        TimeProvider tiempo)
    {
        _conversaciones = conversaciones;
        _respuestas = respuestas;
        _participantes = participantes;
        _configuracion = configuracion;
        _evaluador = evaluador;
        _compilador = compilador;
        _gateway = gateway;
        _tiempo = tiempo;
    }

    public async Task ProcesarMensajeEntranteAsync(
        ParticipanteResuelto participante,
        MensajeEntrante mensaje,
        CancellationToken cancellationToken)
    {
        var usuario = participante.Usuario;
        var campania = participante.Campania;
        var pregunta = participante.PreguntaVigente;
        var numero = usuario.WhatsappNormalizado;
        var ahora = _tiempo.GetUtcNow();

        var conversacionId = $"conv_{campania.Id}_{usuario.Id}_{pregunta.Id}";
        var conversacion = await _conversaciones.ObtenerConversacionAsync(campania.Id, conversacionId, cancellationToken);
        if (conversacion is { Estado: EstadoConversacion.Cerrada })
        {
            // MVP: una conversacion por (usuario, campania, pregunta); cerrada no acepta mas mensajes.
            return;
        }

        conversacion ??= DominioConversacion.Iniciar(conversacionId, campania.Id, usuario.Id, pregunta.Id, Canal, null, ahora);
        var esRepregunta = conversacion.EstadoMaquina == EstadoMaquinaConversacion.EsperandoRepregunta;

        await GuardarMensajeAsync(conversacion, DireccionMensaje.In, mensaje.Texto, mensaje.WhatsappMessageId, mensaje.Timestamp, cancellationToken);

        conversacion = conversacion.RegistrarEntrante(mensaje.Timestamp).AvanzarA(EstadoMaquinaConversacion.Evaluando);
        await _conversaciones.GuardarConversacionAsync(conversacion, cancellationToken);

        await MarcarParticipanteRespondioAsync(participante.Participante, ahora, cancellationToken);

        var respuestaId = "resp_" + Guid.NewGuid().ToString("N");
        var contexto = await ConstruirContextoAsync(campania, pregunta, usuario, respuestaId, mensaje.Texto, cancellationToken);

        if (contexto is null)
        {
            // Sin configuracion completa (rubrica/prompt/configLLM) no se puede evaluar: cierre neutro.
            await GuardarRespuestaAsync(respuestaId, campania.Id, usuario, pregunta, conversacionId, mensaje.Texto, esRepregunta, EstadoRespuesta.EvaluacionPendiente, ahora, cancellationToken);
            await CerrarNeutroAsync(conversacion, numero, campania, ahora, cancellationToken);
            return;
        }

        var resultado = await _evaluador.EvaluarAsync(contexto, cancellationToken);
        await _respuestas.GuardarEvaluacionAsync(resultado.Evaluacion, cancellationToken);

        var esFallback = resultado is ResultadoEvaluacion.Fallback;
        await GuardarRespuestaAsync(
            respuestaId, campania.Id, usuario, pregunta, conversacionId, mensaje.Texto, esRepregunta,
            esFallback ? EstadoRespuesta.EvaluacionPendiente : EstadoRespuesta.Evaluada, ahora, cancellationToken);

        var evaluacion = resultado.Evaluacion;
        var puedeRepreguntar = resultado is ResultadoEvaluacion.Exito
            && evaluacion.Recomendacion == Domain.Evaluacion.RecomendacionEvaluacion.Repreguntar
            && conversacion.RepreguntasUsadas < pregunta.MaxRepreguntas
            && !string.IsNullOrWhiteSpace(evaluacion.RepreguntaSugerida);

        if (puedeRepreguntar)
        {
            var texto = Combinar(evaluacion.RetroalimentacionEnviada, evaluacion.RepreguntaSugerida!);
            await EnviarAsync(conversacion, numero, texto, TipoEnvioMensaje.Repregunta, ahora, cancellationToken);

            conversacion = conversacion.RegistrarRepregunta();
            await _conversaciones.GuardarConversacionAsync(conversacion, cancellationToken);
            return;
        }

        // Cierre: retro + agradecimiento en un solo mensaje (tipo Cierre); compila Markdown si hubo evaluacion valida.
        var cierre = Combinar(evaluacion.RetroalimentacionEnviada, campania.ConfigConversacional.MensajeCierre);
        await EnviarAsync(conversacion, numero, cierre, TipoEnvioMensaje.Cierre, ahora, cancellationToken);

        if (!esFallback)
        {
            await CompilarMarkdownAsync(campania.Id, pregunta, usuario.Id, respuestaId, cancellationToken);
        }

        conversacion = conversacion.Cerrar(ahora);
        await _conversaciones.GuardarConversacionAsync(conversacion, cancellationToken);
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

    private async Task<ContextoEvaluacion?> ConstruirContextoAsync(
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
            return null;
        }

        var rubrica = await _configuracion.ObtenerUltimaRubricaAsync(rubricaRef, cancellationToken);
        var prompt = await _configuracion.ObtenerUltimoPromptAsync(promptRef, cancellationToken);
        var configLlm = await _configuracion.ObtenerConfigLlmAsync(campania.ConfigLlmRef, cancellationToken);
        if (rubrica is null || prompt is null || configLlm is null)
        {
            return null;
        }

        return new ContextoEvaluacion(
            campania,
            pregunta,
            usuario,
            respuestaId,
            texto,
            Array.Empty<string>(),
            rubrica,
            prompt,
            configLlm);
    }

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
}
