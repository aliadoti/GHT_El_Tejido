using ElTejido.Application.Common;
using ElTejido.Application.Configuracion;
using ElTejido.Application.Conversacion;
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
using ElTejido.UnitTests.Soporte;
using FluentAssertions;
using NSubstitute;
using DominioConversacion = ElTejido.Domain.Conversaciones.Conversacion;
using DominioEvaluacion = ElTejido.Domain.Evaluacion.Evaluacion;

namespace ElTejido.UnitTests.Conversacion;

public sealed class OrquestadorConversacionTests
{
    private const string Numero = "573001112233";
    private static readonly DateTimeOffset Epoca = DateTimeOffset.UnixEpoch;

    private readonly FakeConversaciones _conversaciones = new();
    private readonly IRepositorioRespuestas _respuestas = Substitute.For<IRepositorioRespuestas>();
    private readonly IRepositorioParticipantes _participantes = Substitute.For<IRepositorioParticipantes>();
    private readonly IRepositorioConfiguracion _configuracion = Substitute.For<IRepositorioConfiguracion>();
    private readonly IEvaluadorLlm _evaluador = Substitute.For<IEvaluadorLlm>();
    private readonly ISegmentadorIdeas _segmentadorIdeas = Substitute.For<ISegmentadorIdeas>();
    private readonly IBaseConocimientoCampania _baseConocimiento = Substitute.For<IBaseConocimientoCampania>();
    private readonly ICompiladorMarkdown _compilador = Substitute.For<ICompiladorMarkdown>();
    private readonly IWhatsAppGateway _gateway = Substitute.For<IWhatsAppGateway>();
    private readonly IRepositorioLogSeguridad _logSeguridad = Substitute.For<IRepositorioLogSeguridad>();
    private readonly IProveedorCorrelacion _correlacion = Substitute.For<IProveedorCorrelacion>();
    private readonly RelojFijo _reloj = new(Epoca);

    public OrquestadorConversacionTests()
    {
        _configuracion.ObtenerUltimaRubricaAsync("rub_1", Arg.Any<CancellationToken>()).Returns(CrearRubrica());
        _configuracion.ObtenerUltimoPromptAsync("pr_eval", Arg.Any<CancellationToken>()).Returns(CrearPrompt());
        _configuracion.ObtenerConfigLlmAsync("llm_1", Arg.Any<CancellationToken>()).Returns(CrearConfig());
        _correlacion.CorrelationIdActual.Returns("corr_test");
        _gateway.EnviarTextoAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TipoEnvioMensaje>(), Arg.Any<CancellationToken>())
            .Returns(EnvioResultado.Ok("wamid.out"));
    }

    [Fact]
    public async Task Procesar_PrimerTurnoExito_CompilaOfreceMejoraYNoCierra()
    {
        // Aunque el LLM recomiende cerrar, la primera evaluacion valida SIEMPRE ofrece una mejora.
        _evaluador.EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Exito(CrearEvaluacion(RecomendacionEvaluacion.Cerrar, null)));
        await PrepararConversacionAsync();

        await Construir().ProcesarMensajeEntranteAsync(Participante(), Mensaje("Mi idea"), CancellationToken.None);

        await _gateway.Received(1).EnviarTextoAsync(Numero, Arg.Any<string>(), TipoEnvioMensaje.Repregunta, Arg.Any<CancellationToken>());
        await _gateway.DidNotReceive().EnviarTextoAsync(Numero, Arg.Any<string>(), TipoEnvioMensaje.Cierre, Arg.Any<CancellationToken>());
        await _compilador.Received(1).CompilarAsync(Arg.Any<SolicitudCompilacion>(), Arg.Any<CancellationToken>());
        _conversaciones.Ultima!.Estado.Should().Be(EstadoConversacion.Abierta);
        _conversaciones.Ultima!.EstadoMaquina.Should().Be(EstadoMaquinaConversacion.EsperandoRepregunta);
        _conversaciones.Ultima!.RepreguntasUsadas.Should().Be(1);
        await _respuestas.Received().GuardarRespuestaAsync(
            Arg.Is<Respuesta>(r => r.Estado == EstadoRespuesta.Evaluada), Arg.Any<CancellationToken>());
        await _participantes.Received().GuardarParticipanteAsync(
            Arg.Is<ParticipanteCampania>(p => p.EstadoRespuesta == EstadoRespuestaParticipante.Respondio), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Procesar_RespuestaDespuesDeRevisionAgotada_RegistraSinEvaluarYCierraConAgradecimiento()
    {
        _evaluador.EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Exito(CrearEvaluacion(RecomendacionEvaluacion.Cerrar, null)));
        await PrepararConversacionAsync();
        var orquestador = Construir();

        await orquestador.ProcesarMensajeEntranteAsync(Participante(), Mensaje("Idea"), CancellationToken.None);
        await orquestador.ProcesarMensajeEntranteAsync(Participante(), Mensaje("Idea mejorada"), CancellationToken.None);

        // Turno 1 ofrece mejora; turno 2 se registra sin evaluar porque el cupo (1) ya se agoto.
        await _gateway.Received(1).EnviarTextoAsync(Numero, Arg.Any<string>(), TipoEnvioMensaje.Repregunta, Arg.Any<CancellationToken>());
        await _gateway.Received(1).EnviarTextoAsync(
            Numero,
            Arg.Is<string>(texto => texto == "Gracias por participar."),
            TipoEnvioMensaje.Cierre,
            Arg.Any<CancellationToken>());
        _conversaciones.Ultima!.Estado.Should().Be(EstadoConversacion.Cerrada);
        await _evaluador.Received(1).EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>());
        await _compilador.Received(1).CompilarAsync(Arg.Any<SolicitudCompilacion>(), Arg.Any<CancellationToken>());
        await _respuestas.Received(1).GuardarRespuestaAsync(
            Arg.Is<Respuesta>(r => r.Estado == EstadoRespuesta.Evaluada), Arg.Any<CancellationToken>());
        await _respuestas.Received(1).GuardarRespuestaAsync(
            Arg.Is<Respuesta>(r => r.Estado == EstadoRespuesta.Recibida && r.EsRepregunta), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Procesar_MaxRepreguntasDos_EvaluaDosVersionesYLaFinalSoloAgradece()
    {
        _evaluador.EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Exito(CrearEvaluacion(RecomendacionEvaluacion.Cerrar, null)));
        await PrepararConversacionAsync();
        var orquestador = Construir();
        var participante = Participante(maxRepreguntas: 2);

        await orquestador.ProcesarMensajeEntranteAsync(participante, Mensaje("Idea"), CancellationToken.None);
        await orquestador.ProcesarMensajeEntranteAsync(participante, Mensaje("Idea mejorada"), CancellationToken.None);
        await orquestador.ProcesarMensajeEntranteAsync(participante, Mensaje("Idea final"), CancellationToken.None);

        await _evaluador.Received(2).EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>());
        await _gateway.Received(2).EnviarTextoAsync(Numero, Arg.Any<string>(), TipoEnvioMensaje.Repregunta, Arg.Any<CancellationToken>());
        await _gateway.Received(1).EnviarTextoAsync(
            Numero,
            Arg.Is<string>(texto => texto == "Gracias por participar."),
            TipoEnvioMensaje.Cierre,
            Arg.Any<CancellationToken>());
        await _respuestas.Received(2).GuardarRespuestaAsync(
            Arg.Is<Respuesta>(r => r.Estado == EstadoRespuesta.Evaluada), Arg.Any<CancellationToken>());
        await _respuestas.Received(1).GuardarRespuestaAsync(
            Arg.Is<Respuesta>(r => r.Estado == EstadoRespuesta.Recibida && r.Texto == "Idea final"), Arg.Any<CancellationToken>());
        _conversaciones.Ultima!.Estado.Should().Be(EstadoConversacion.Cerrada);
    }

    [Fact]
    public async Task Procesar_Fallback_CierraNeutroYRespuestaPendiente()
    {
        _evaluador.EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Fallback(CrearEvaluacion(RecomendacionEvaluacion.Cerrar, null, EvaluadorLlm.RetroNeutra), "error_proveedor"));
        await PrepararConversacionAsync();

        await Construir().ProcesarMensajeEntranteAsync(Participante(), Mensaje("Mi idea"), CancellationToken.None);

        await _gateway.Received(1).EnviarTextoAsync(Numero, Arg.Any<string>(), TipoEnvioMensaje.Cierre, Arg.Any<CancellationToken>());
        await _compilador.DidNotReceiveWithAnyArgs().CompilarAsync(default!, default);
        _conversaciones.Ultima!.Estado.Should().Be(EstadoConversacion.Cerrada);
        await _respuestas.Received().GuardarRespuestaAsync(
            Arg.Is<Respuesta>(r => r.Estado == EstadoRespuesta.EvaluacionPendiente), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Procesar_ConversacionCerrada_Ignora()
    {
        var cerrada = DominioConversacion.Iniciar("conv_c_1_u_1_p_1", "c_1", "u_1", "p_1", "whatsapp", null, Epoca).Cerrar(Epoca);
        await _conversaciones.GuardarConversacionAsync(cerrada, CancellationToken.None);

        await Construir().ProcesarMensajeEntranteAsync(Participante(), Mensaje("tarde"), CancellationToken.None);

        await _gateway.DidNotReceiveWithAnyArgs().EnviarTextoAsync(default!, default!, default, default);
        await _evaluador.DidNotReceiveWithAnyArgs().EvaluarAsync(default!, default);
    }

    [Fact]
    public async Task Procesar_ConfigIncompleta_CierraNeutroSinEvaluar()
    {
        _configuracion.ObtenerUltimoPromptAsync("pr_eval", Arg.Any<CancellationToken>()).Returns((Prompt?)null);
        await PrepararConversacionAsync();
        var opciones = new OpcionesConversacion
        {
            Mensajes = new OpcionesMensajesConversacion
            {
                MensajeConfiguracionNoDisponible = "Mensaje configurable para admin.",
            },
        };

        await Construir(opciones).ProcesarMensajeEntranteAsync(Participante(), Mensaje("Mi idea"), CancellationToken.None);

        await _evaluador.DidNotReceiveWithAnyArgs().EvaluarAsync(default!, default);
        await _gateway.Received(1).EnviarTextoAsync(
            Numero,
            Arg.Is<string>(texto => texto == "Mensaje configurable para admin."),
            TipoEnvioMensaje.Cierre,
            Arg.Any<CancellationToken>());
        _conversaciones.Ultima!.Estado.Should().Be(EstadoConversacion.Cerrada);
    }

    [Fact]
    public async Task Procesar_TextosConfigurados_UsaOpcionesDeConversacion()
    {
        _evaluador.EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Exito(CrearEvaluacion(RecomendacionEvaluacion.Cerrar, null)));
        var orquestador = Construir(new OpcionesConversacion
        {
            Mensajes = new OpcionesMensajesConversacion
            {
                SaludoPrimerContacto = "Saludo configurable",
                InvitacionMejora = "Invitacion configurable",
                MensajeConfiguracionNoDisponible = "Config incompleta configurable",
            },
        });

        await orquestador.ProcesarMensajeEntranteAsync(ParticipanteFrio(), Mensaje("Hola"), CancellationToken.None);
        await orquestador.ProcesarMensajeEntranteAsync(ParticipanteFrio(), Mensaje("Mi idea"), CancellationToken.None);

        await _gateway.Received(1).EnviarTextoAsync(
            Numero,
            Arg.Is<string>(texto => texto.Contains("Saludo configurable", StringComparison.Ordinal)),
            TipoEnvioMensaje.Inicial,
            Arg.Any<CancellationToken>());
        await _gateway.Received(1).EnviarTextoAsync(
            Numero,
            Arg.Is<string>(texto => texto.Contains("Invitacion configurable", StringComparison.Ordinal)),
            TipoEnvioMensaje.Repregunta,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Procesar_ConfigLlmInactiva_CierraNeutroSinEvaluarYRegistraFallback()
    {
        _configuracion.ObtenerConfigLlmAsync("llm_1", Arg.Any<CancellationToken>())
            .Returns(CrearConfig(EstadoRegistro.Inactivo));
        await PrepararConversacionAsync();

        await Construir().ProcesarMensajeEntranteAsync(Participante(), Mensaje("Mi idea"), CancellationToken.None);

        await _evaluador.DidNotReceiveWithAnyArgs().EvaluarAsync(default!, default);
        await _gateway.Received(1).EnviarTextoAsync(Numero, Arg.Any<string>(), TipoEnvioMensaje.Cierre, Arg.Any<CancellationToken>());
        await _logSeguridad.Received(1).RegistrarAsync(
            Arg.Is<LogSeguridad>(l =>
                l.TipoEvento == TipoEventoSeguridad.AnomaliaLlm
                && l.Resultado == "fallback"
                && l.Detalle == "config_llm_no_activa"
                && l.CorrelationId == "corr_test"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Procesar_PromptSinAprobacion_CierraNeutroSinEvaluar()
    {
        _configuracion.ObtenerUltimoPromptAsync("pr_eval", Arg.Any<CancellationToken>())
            .Returns(CrearPrompt(aprobado: false));
        await PrepararConversacionAsync();

        await Construir().ProcesarMensajeEntranteAsync(Participante(), Mensaje("Mi idea"), CancellationToken.None);

        await _evaluador.DidNotReceiveWithAnyArgs().EvaluarAsync(default!, default);
        await _logSeguridad.Received(1).RegistrarAsync(
            Arg.Is<LogSeguridad>(l => l.Detalle == "prompt_no_aprobado"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Procesar_RubricaInactiva_CierraNeutroSinEvaluar()
    {
        _configuracion.ObtenerUltimaRubricaAsync("rub_1", Arg.Any<CancellationToken>())
            .Returns(CrearRubrica(EstadoRubrica.Archivada));
        await PrepararConversacionAsync();

        await Construir().ProcesarMensajeEntranteAsync(Participante(), Mensaje("Mi idea"), CancellationToken.None);

        await _evaluador.DidNotReceiveWithAnyArgs().EvaluarAsync(default!, default);
        await _logSeguridad.Received(1).RegistrarAsync(
            Arg.Is<LogSeguridad>(l => l.Detalle == "rubrica_no_activa"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Procesar_PrimerContactoSinEnvioInicial_EnviaPreguntaYNoEvalua()
    {
        var pregunta = FabricasDominio.CrearPregunta("p_1", 1);

        await Construir().ProcesarMensajeEntranteAsync(ParticipanteFrio(), Mensaje("Hola"), CancellationToken.None);

        await _evaluador.DidNotReceiveWithAnyArgs().EvaluarAsync(default!, default);
        await _gateway.Received(1).EnviarTextoAsync(
            Numero, Arg.Is<string>(t => t.Contains(pregunta.Texto)), TipoEnvioMensaje.Inicial, Arg.Any<CancellationToken>());
        _conversaciones.Ultima!.Estado.Should().Be(EstadoConversacion.Abierta);
        _conversaciones.Ultima!.EstadoMaquina.Should().Be(EstadoMaquinaConversacion.EsperandoRespuestaInicial);
    }

    [Fact]
    public async Task Procesar_SegundoMensajeTrasPrimerContacto_EvaluaYOfreceMejora()
    {
        _evaluador.EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Exito(CrearEvaluacion(RecomendacionEvaluacion.Cerrar, null)));
        var orquestador = Construir();
        var frio = ParticipanteFrio();

        // 1) "Hola" -> pregunta; 2) respuesta -> evalua y ofrece mejora; 3) mejora -> registra y cierra sin LLM.
        await orquestador.ProcesarMensajeEntranteAsync(frio, Mensaje("Hola"), CancellationToken.None);
        await orquestador.ProcesarMensajeEntranteAsync(frio, Mensaje("Mi idea real"), CancellationToken.None);
        _conversaciones.Ultima!.EstadoMaquina.Should().Be(EstadoMaquinaConversacion.EsperandoRepregunta);

        await orquestador.ProcesarMensajeEntranteAsync(frio, Mensaje("Mi idea mejorada"), CancellationToken.None);

        await _evaluador.Received(1).EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>());
        await _gateway.Received(1).EnviarTextoAsync(Numero, Arg.Any<string>(), TipoEnvioMensaje.Inicial, Arg.Any<CancellationToken>());
        await _gateway.Received(1).EnviarTextoAsync(Numero, Arg.Any<string>(), TipoEnvioMensaje.Repregunta, Arg.Any<CancellationToken>());
        await _gateway.Received(1).EnviarTextoAsync(Numero, Arg.Any<string>(), TipoEnvioMensaje.Cierre, Arg.Any<CancellationToken>());
        _conversaciones.Ultima!.Estado.Should().Be(EstadoConversacion.Cerrada);
    }

    [Fact]
    public async Task Procesar_BusinessInitiatedPrimerEntrante_EnviaPreguntaLuegoEvaluaYCierra()
    {
        _evaluador.EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Exito(CrearEvaluacion(RecomendacionEvaluacion.Cerrar, null)));
        var orquestador = Construir();
        var participante = Participante();

        await orquestador.ProcesarMensajeEntranteAsync(participante, Mensaje("Hola"), CancellationToken.None);

        await _evaluador.DidNotReceiveWithAnyArgs().EvaluarAsync(default!, default);
        await _respuestas.DidNotReceiveWithAnyArgs().GuardarRespuestaAsync(default!, default);
        await _gateway.Received(1).EnviarTextoAsync(
            Numero,
            Arg.Is<string>(t => t.Contains(participante.PreguntaVigente.Texto, StringComparison.Ordinal)),
            TipoEnvioMensaje.Inicial,
            Arg.Any<CancellationToken>());
        _conversaciones.Ultima!.EstadoMaquina.Should().Be(EstadoMaquinaConversacion.EsperandoRespuestaInicial);

        await orquestador.ProcesarMensajeEntranteAsync(participante, Mensaje("Mi idea real"), CancellationToken.None);
        _conversaciones.Ultima!.EstadoMaquina.Should().Be(EstadoMaquinaConversacion.EsperandoRepregunta);

        await orquestador.ProcesarMensajeEntranteAsync(participante, Mensaje("Mi idea mejorada"), CancellationToken.None);

        await _evaluador.Received(1).EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>());
        await _gateway.Received(1).EnviarTextoAsync(Numero, Arg.Any<string>(), TipoEnvioMensaje.Repregunta, Arg.Any<CancellationToken>());
        await _gateway.Received(1).EnviarTextoAsync(Numero, Arg.Any<string>(), TipoEnvioMensaje.Cierre, Arg.Any<CancellationToken>());
        _conversaciones.Ultima!.Estado.Should().Be(EstadoConversacion.Cerrada);
    }

    [Fact]
    public async Task Procesar_CampaniaConDosPreguntas_CubreCicloCompletoPorPregunta()
    {
        _evaluador.EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Exito(CrearEvaluacion(RecomendacionEvaluacion.Cerrar, null)));
        var orquestador = Construir();
        var participante = ParticipanteMultipregunta();

        await orquestador.ProcesarMensajeEntranteAsync(participante, Mensaje("Hola"), CancellationToken.None);
        await orquestador.ProcesarMensajeEntranteAsync(participante, Mensaje("Respuesta p1"), CancellationToken.None);
        await orquestador.ProcesarMensajeEntranteAsync(participante, Mensaje("Mejora p1"), CancellationToken.None);
        await orquestador.ProcesarMensajeEntranteAsync(participante, Mensaje("Respuesta p2"), CancellationToken.None);
        await orquestador.ProcesarMensajeEntranteAsync(participante, Mensaje("Mejora p2"), CancellationToken.None);

        await _gateway.Received(2).EnviarTextoAsync(Numero, Arg.Any<string>(), TipoEnvioMensaje.Inicial, Arg.Any<CancellationToken>());
        await _gateway.Received(2).EnviarTextoAsync(Numero, Arg.Any<string>(), TipoEnvioMensaje.Repregunta, Arg.Any<CancellationToken>());
        await _gateway.Received(2).EnviarTextoAsync(Numero, Arg.Any<string>(), TipoEnvioMensaje.Cierre, Arg.Any<CancellationToken>());
        await _gateway.Received(1).EnviarTextoAsync(
            Numero,
            Arg.Is<string>(texto => texto.Contains("Pregunta 2", StringComparison.Ordinal)),
            TipoEnvioMensaje.Inicial,
            Arg.Any<CancellationToken>());
        await _evaluador.Received(2).EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>());
        await _respuestas.Received(4).GuardarRespuestaAsync(Arg.Any<Respuesta>(), Arg.Any<CancellationToken>());
        await _respuestas.Received(2).GuardarRespuestaAsync(
            Arg.Is<Respuesta>(respuesta => respuesta.PreguntaId == "p_2"),
            Arg.Any<CancellationToken>());

        _conversaciones.Conversaciones.Should().ContainSingle(c => c.PreguntaId == "p_1" && c.Estado == EstadoConversacion.Cerrada);
        _conversaciones.Conversaciones.Should().ContainSingle(c => c.PreguntaId == "p_2" && c.Estado == EstadoConversacion.Cerrada);
    }

    [Fact]
    public async Task Procesar_CalificacionAlta_NoOfreceMejoraYCierraConFelicitacion()
    {
        // Escala 1..5, umbral 0.85 -> 4.4; calificacion 5 lo supera. Aunque queda 1 repregunta (default),
        // no se ofrece mejora: se felicita y cierra.
        _evaluador.EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Exito(CrearEvaluacion(RecomendacionEvaluacion.Repreguntar, "Profundiza mas", "Buena idea", calificacionTotal: 5m)));
        var orquestador = Construir(new OpcionesConversacion { UmbralCierreAnticipado = 0.85 });
        var participante = Participante();

        await orquestador.ProcesarMensajeEntranteAsync(participante, Mensaje("Hola"), CancellationToken.None);
        await orquestador.ProcesarMensajeEntranteAsync(participante, Mensaje("Mi idea excelente"), CancellationToken.None);

        await _evaluador.Received(1).EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>());
        await _gateway.DidNotReceive().EnviarTextoAsync(Numero, Arg.Any<string>(), TipoEnvioMensaje.Repregunta, Arg.Any<CancellationToken>());
        await _gateway.Received(1).EnviarTextoAsync(
            Numero,
            Arg.Is<string>(texto => texto.Contains(OpcionesMensajesConversacion.MensajeCalificacionAltaDefault, StringComparison.Ordinal)),
            TipoEnvioMensaje.Cierre,
            Arg.Any<CancellationToken>());
        await _compilador.Received(1).CompilarAsync(Arg.Any<SolicitudCompilacion>(), Arg.Any<CancellationToken>());
        _conversaciones.Ultima!.Estado.Should().Be(EstadoConversacion.Cerrada);
        // I-01: telemetria de calibracion del cierre anticipado (10 §6.2/§6.4). Escala 1..5, umbral
        // 0.85 -> valor 4.4; el detalle lleva score y valor de corte, sin PII de texto.
        await _logSeguridad.Received(1).RegistrarAsync(
            Arg.Is<LogSeguridad>(l =>
                l.TipoEvento == TipoEventoSeguridad.CierreUmbralAnticipado
                && l.Resultado == "cierre_anticipado"
                && l.Detalle!.Contains("score:5", StringComparison.Ordinal)
                && l.Detalle!.Contains("valor:4.4", StringComparison.Ordinal)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Procesar_CalificacionBajoUmbral_OfreceMejoraComoSiempre()
    {
        // Misma escala/umbral, pero calificacion 3 < 4.4: el umbral no aplica y se ofrece la mejora.
        _evaluador.EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Exito(CrearEvaluacion(RecomendacionEvaluacion.Cerrar, null, "Buena idea", calificacionTotal: 3m)));
        var orquestador = Construir(new OpcionesConversacion { UmbralCierreAnticipado = 0.85 });
        var participante = Participante();

        await orquestador.ProcesarMensajeEntranteAsync(participante, Mensaje("Hola"), CancellationToken.None);
        await orquestador.ProcesarMensajeEntranteAsync(participante, Mensaje("Mi idea"), CancellationToken.None);

        await _gateway.Received(1).EnviarTextoAsync(Numero, Arg.Any<string>(), TipoEnvioMensaje.Repregunta, Arg.Any<CancellationToken>());
        await _gateway.DidNotReceive().EnviarTextoAsync(Numero, Arg.Any<string>(), TipoEnvioMensaje.Cierre, Arg.Any<CancellationToken>());
        _conversaciones.Ultima!.EstadoMaquina.Should().Be(EstadoMaquinaConversacion.EsperandoRepregunta);
        // I-01: por debajo del umbral no se emite la telemetria de cierre anticipado.
        await _logSeguridad.DidNotReceive().RegistrarAsync(
            Arg.Is<LogSeguridad>(l => l.TipoEvento == TipoEventoSeguridad.CierreUmbralAnticipado),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Procesar_IntencionContinuar_RegistraSinEvaluarYCierraConAcuse()
    {
        // MaxRepreguntas=2: tras ofrecer la mejora, queda cupo; el participante igual pide continuar.
        _evaluador.EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Exito(CrearEvaluacion(RecomendacionEvaluacion.Cerrar, null)));
        var orquestador = Construir();
        var participante = Participante(maxRepreguntas: 2);

        await orquestador.ProcesarMensajeEntranteAsync(participante, Mensaje("Hola"), CancellationToken.None);
        await orquestador.ProcesarMensajeEntranteAsync(participante, Mensaje("Mi idea real"), CancellationToken.None);
        _conversaciones.Ultima!.EstadoMaquina.Should().Be(EstadoMaquinaConversacion.EsperandoRepregunta);

        await orquestador.ProcesarMensajeEntranteAsync(participante, Mensaje("Asi esta bien, sigamos"), CancellationToken.None);

        // Solo se evaluo la primera respuesta; el "sigamos" no se manda al LLM.
        await _evaluador.Received(1).EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>());
        await _gateway.Received(1).EnviarTextoAsync(Numero, Arg.Any<string>(), TipoEnvioMensaje.Repregunta, Arg.Any<CancellationToken>());
        await _gateway.Received(1).EnviarTextoAsync(
            Numero,
            Arg.Is<string>(texto => texto.Contains(OpcionesMensajesConversacion.AcuseContinuarDefault, StringComparison.Ordinal)),
            TipoEnvioMensaje.Cierre,
            Arg.Any<CancellationToken>());
        await _respuestas.Received(1).GuardarRespuestaAsync(
            Arg.Is<Respuesta>(r => r.Estado == EstadoRespuesta.Recibida && r.Texto == "Asi esta bien, sigamos"), Arg.Any<CancellationToken>());
        _conversaciones.Ultima!.Estado.Should().Be(EstadoConversacion.Cerrada);
    }

    [Fact]
    public async Task Procesar_IntencionEnRespuestaInicial_SeEvaluaIgual()
    {
        // Una frase de continuar como PRIMERA respuesta no se interpreta como intencion: se evalua.
        _evaluador.EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Exito(CrearEvaluacion(RecomendacionEvaluacion.Cerrar, null)));
        var orquestador = Construir();
        var participante = Participante(maxRepreguntas: 2);

        await orquestador.ProcesarMensajeEntranteAsync(participante, Mensaje("Hola"), CancellationToken.None);
        await orquestador.ProcesarMensajeEntranteAsync(participante, Mensaje("sigamos"), CancellationToken.None);

        await _evaluador.Received(1).EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>());
        await _gateway.Received(1).EnviarTextoAsync(Numero, Arg.Any<string>(), TipoEnvioMensaje.Repregunta, Arg.Any<CancellationToken>());
        _conversaciones.Ultima!.EstadoMaquina.Should().Be(EstadoMaquinaConversacion.EsperandoRepregunta);
    }

    [Fact]
    public async Task Procesar_PrimerContacto_UsaMensajeInicialDeLaCampania()
    {
        // Bug #1: el saludo del primer entrante sale del MensajeInicial guardado en la campania
        // (con variables resueltas), no del texto de App Settings.
        var participante = ParticipanteConMensajeInicial("Bienvenido {{nombre}} a {{campania}}.");

        await Construir().ProcesarMensajeEntranteAsync(participante, Mensaje("Hola"), CancellationToken.None);

        await _evaluador.DidNotReceiveWithAnyArgs().EvaluarAsync(default!, default);
        await _gateway.Received(1).EnviarTextoAsync(
            Numero,
            Arg.Is<string>(texto =>
                texto.Contains("Bienvenido Admin a Campania c_1.", StringComparison.Ordinal)
                && texto.Contains(participante.PreguntaVigente.Texto, StringComparison.Ordinal)
                && !texto.Contains("{{", StringComparison.Ordinal)
                && !texto.Contains(OpcionesMensajesConversacion.SaludoPrimerContactoDefault, StringComparison.Ordinal)),
            TipoEnvioMensaje.Inicial,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Procesar_PrimerContactoSinMensajeInicial_CaeAlSaludoConfigurado()
    {
        // Bug #1 (fallback aprobado): sin MensajeInicial activo, usa Conversacion:Mensajes:SaludoPrimerContacto.
        var orquestador = Construir(new OpcionesConversacion
        {
            Mensajes = new OpcionesMensajesConversacion { SaludoPrimerContacto = "Saludo de respaldo" },
        });

        await orquestador.ProcesarMensajeEntranteAsync(ParticipanteFrio(), Mensaje("Hola"), CancellationToken.None);

        await _gateway.Received(1).EnviarTextoAsync(
            Numero,
            Arg.Is<string>(texto => texto.Contains("Saludo de respaldo", StringComparison.Ordinal)),
            TipoEnvioMensaje.Inicial,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Procesar_SegundoTurno_PasaHistorialAlEvaluador()
    {
        // Bug #2: la iteracion previa persistida se pasa al LLM (para no repetir/loopear); la respuesta
        // que se esta evaluando ahora no se duplica en el historial.
        ContextoEvaluacion? capturado = null;
        _evaluador.EvaluarAsync(Arg.Do<ContextoEvaluacion>(c => capturado = c), Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Exito(CrearEvaluacion(RecomendacionEvaluacion.Cerrar, null)));
        var orquestador = Construir();
        var participante = Participante();

        await orquestador.ProcesarMensajeEntranteAsync(participante, Mensaje("Hola"), CancellationToken.None);
        await orquestador.ProcesarMensajeEntranteAsync(participante, Mensaje("Mi idea real"), CancellationToken.None);

        capturado.Should().NotBeNull();
        capturado!.HistorialReciente.Should().Contain(t => t.Contains("Participante: Hola", StringComparison.Ordinal));
        capturado.HistorialReciente.Should().NotContain(t => t.Contains("Mi idea real", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Procesar_InvitacionMejora_EnsenaLaSalidaPorDefecto()
    {
        // Bug #2: la invitacion a mejorar siempre ensena la salida del "no quiero seguir" (coletilla).
        _evaluador.EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Exito(CrearEvaluacion(RecomendacionEvaluacion.Cerrar, null)));
        await PrepararConversacionAsync();

        await Construir().ProcesarMensajeEntranteAsync(Participante(), Mensaje("Mi idea"), CancellationToken.None);

        await _gateway.Received(1).EnviarTextoAsync(
            Numero,
            Arg.Is<string>(texto => OpcionesMensajesConversacion.InvitacionContinuarVariantesDefault
                .Any(variante => texto.Contains(variante, StringComparison.Ordinal))),
            TipoEnvioMensaje.Repregunta,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Procesar_ConRepreguntaSugerida_LaUsaComoInvitacionNatural()
    {
        // Bug #2 (Opcion B): si el LLM devuelve una repregunta natural, esa es el nucleo de la invitacion.
        _evaluador.EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Exito(
                CrearEvaluacion(RecomendacionEvaluacion.Repreguntar, "Podrias dar un ejemplo concreto?")));
        await PrepararConversacionAsync();

        await Construir().ProcesarMensajeEntranteAsync(Participante(), Mensaje("Mi idea"), CancellationToken.None);

        await _gateway.Received(1).EnviarTextoAsync(
            Numero,
            Arg.Is<string>(texto => texto.Contains("Podrias dar un ejemplo concreto?", StringComparison.Ordinal)),
            TipoEnvioMensaje.Repregunta,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Procesar_CupoMensajesUsuarioExcedido_DescartaSilenciosoYRegistraRateLimit()
    {
        // Cupos habilitados (10 §2): el usuario ya consumio su MaxMensajesPorUsuario (1) en la campania;
        // el siguiente entrante se descarta con rechazo neutral silencioso y solo queda LogSeguridad.
        await PrepararConversacionAsync();
        await SembrarEntranteAsync("hola previo");
        var opciones = new OpcionesConversacion { CuposHabilitados = true };

        await Construir(opciones).ProcesarMensajeEntranteAsync(
            ParticipanteConCupos(maxMensajesPorUsuario: 1, maxLlamadasLlm: 2), Mensaje("otro mensaje"), CancellationToken.None);

        await _gateway.DidNotReceive().EnviarTextoAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TipoEnvioMensaje>(), Arg.Any<CancellationToken>());
        await _evaluador.DidNotReceive().EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>());
        await _respuestas.DidNotReceive().GuardarRespuestaAsync(Arg.Any<Respuesta>(), Arg.Any<CancellationToken>());
        _conversaciones.MensajesGuardados.Should().Be(1, "el entrante excedente no se persiste");
        await _logSeguridad.Received(1).RegistrarAsync(
            Arg.Is<LogSeguridad>(l =>
                l.TipoEvento == TipoEventoSeguridad.RateLimit
                && l.Detalle == "cupo_mensajes_usuario"
                && l.UsuarioId == "u_1"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Procesar_TopeTurnosHiloAlcanzado_RegistraRecibidaYCierraElegante()
    {
        // Techo duro de turnos por hilo (D2): garantiza terminacion sin depender del LLM. Con
        // MaxTurnosPorHilo=1 y un entrante previo, este turno se registra sin evaluar y cierra.
        await PrepararConversacionAsync();
        await SembrarEntranteAsync("mi respuesta previa");
        var opciones = new OpcionesConversacion { MaxTurnosPorHilo = 1 };

        await Construir(opciones).ProcesarMensajeEntranteAsync(Participante(), Mensaje("otra idea"), CancellationToken.None);

        await _evaluador.DidNotReceive().EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>());
        await _respuestas.Received(1).GuardarRespuestaAsync(
            Arg.Is<Respuesta>(r => r.Estado == EstadoRespuesta.Recibida), Arg.Any<CancellationToken>());
        await _gateway.Received(1).EnviarTextoAsync(
            Numero,
            Arg.Is<string>(texto => texto == "Gracias por participar."),
            TipoEnvioMensaje.Cierre,
            Arg.Any<CancellationToken>());
        _conversaciones.Ultima!.Estado.Should().Be(EstadoConversacion.Cerrada);
        await _logSeguridad.Received(1).RegistrarAsync(
            Arg.Is<LogSeguridad>(l =>
                l.TipoEvento == TipoEventoSeguridad.RateLimit
                && l.Detalle == "tope_turnos_hilo"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Procesar_CupoLlamadasLlmExcedido_CierraSinEvaluarNiEnviarSiguientePregunta()
    {
        // Cupo de llamadas LLM por usuario/campania (10 §2): agotado el cupo no se llama al LLM; el
        // hilo cierra elegante y NO se abre la siguiente pregunta (tampoco podria evaluarse).
        await PrepararConversacionAsync();
        _respuestas.ContarEvaluacionesUsuarioAsync("c_1", "u_1", Arg.Any<CancellationToken>()).Returns(1);
        var opciones = new OpcionesConversacion { CuposHabilitados = true };

        await Construir(opciones).ProcesarMensajeEntranteAsync(
            ParticipanteConCupos(maxMensajesPorUsuario: 10, maxLlamadasLlm: 1, dosPreguntas: true),
            Mensaje("Mi idea"),
            CancellationToken.None);

        await _evaluador.DidNotReceive().EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>());
        await _respuestas.Received(1).GuardarRespuestaAsync(
            Arg.Is<Respuesta>(r => r.Estado == EstadoRespuesta.Recibida), Arg.Any<CancellationToken>());
        await _gateway.Received(1).EnviarTextoAsync(
            Numero, Arg.Any<string>(), TipoEnvioMensaje.Cierre, Arg.Any<CancellationToken>());
        await _gateway.DidNotReceive().EnviarTextoAsync(
            Numero, Arg.Any<string>(), TipoEnvioMensaje.Inicial, Arg.Any<CancellationToken>());
        _conversaciones.Ultima!.Estado.Should().Be(EstadoConversacion.Cerrada);
        await _logSeguridad.Received(1).RegistrarAsync(
            Arg.Is<LogSeguridad>(l =>
                l.TipoEvento == TipoEventoSeguridad.RateLimit
                && l.Detalle == "cupo_llamadas_llm_usuario"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Procesar_PresupuestoTokensCampaniaExcedido_CierraSinEvaluar()
    {
        // P-10: agotado el presupuesto de tokens de la campania, se cierra elegante sin llamar al LLM
        // y NO se abre la siguiente pregunta; queda rastro RateLimit con el motivo especifico.
        await PrepararConversacionAsync();
        _respuestas.ContarEvaluacionesUsuarioAsync("c_1", "u_1", Arg.Any<CancellationToken>()).Returns(0);
        _respuestas.SumarTokensCampaniaAsync("c_1", Arg.Any<CancellationToken>()).Returns(1200L);
        var opciones = new OpcionesConversacion { CuposHabilitados = true };

        await Construir(opciones).ProcesarMensajeEntranteAsync(
            ParticipanteConCupos(maxMensajesPorUsuario: 10, maxLlamadasLlm: 5, dosPreguntas: true, presupuestoTokens: 1000),
            Mensaje("Mi idea"),
            CancellationToken.None);

        await _evaluador.DidNotReceive().EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>());
        await _gateway.Received(1).EnviarTextoAsync(Numero, Arg.Any<string>(), TipoEnvioMensaje.Cierre, Arg.Any<CancellationToken>());
        await _gateway.DidNotReceive().EnviarTextoAsync(Numero, Arg.Any<string>(), TipoEnvioMensaje.Inicial, Arg.Any<CancellationToken>());
        await _logSeguridad.Received(1).RegistrarAsync(
            Arg.Is<LogSeguridad>(l =>
                l.TipoEvento == TipoEventoSeguridad.RateLimit
                && l.Detalle == "presupuesto_tokens_campania"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Procesar_PresupuestoTokensBajoElTecho_EvaluaNormalmente()
    {
        _evaluador.EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Exito(CrearEvaluacion(RecomendacionEvaluacion.Cerrar, null)));
        await PrepararConversacionAsync();
        await SembrarEntranteAsync("hola previo");
        _respuestas.ContarEvaluacionesUsuarioAsync("c_1", "u_1", Arg.Any<CancellationToken>()).Returns(0);
        _respuestas.SumarTokensCampaniaAsync("c_1", Arg.Any<CancellationToken>()).Returns(500L);
        var opciones = new OpcionesConversacion { CuposHabilitados = true };

        await Construir(opciones).ProcesarMensajeEntranteAsync(
            ParticipanteConCupos(maxMensajesPorUsuario: 10, maxLlamadasLlm: 5, presupuestoTokens: 1000),
            Mensaje("Mi idea"),
            CancellationToken.None);

        await _evaluador.Received(1).EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Procesar_CuposDeshabilitados_IgnoraLimitesDeCampania()
    {
        // Default off (D1): aunque los limites de la campania ya esten consumidos, sin
        // Conversacion:CuposHabilitados el flujo evalua como siempre (cero regresion).
        _evaluador.EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Exito(CrearEvaluacion(RecomendacionEvaluacion.Cerrar, null)));
        await PrepararConversacionAsync();
        await SembrarEntranteAsync("hola previo");
        _respuestas.ContarEvaluacionesUsuarioAsync("c_1", "u_1", Arg.Any<CancellationToken>()).Returns(5);

        await Construir().ProcesarMensajeEntranteAsync(
            ParticipanteConCupos(maxMensajesPorUsuario: 1, maxLlamadasLlm: 1), Mensaje("Mi idea"), CancellationToken.None);

        await _evaluador.Received(1).EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>());
        await _logSeguridad.DidNotReceive().RegistrarAsync(
            Arg.Is<LogSeguridad>(l => l.TipoEvento == TipoEventoSeguridad.RateLimit), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Procesar_SegmentacionActiva_PersisteEvaluaYCompilaUnaVezPorIdea()
    {
        var respuestas = new List<Respuesta>();
        _respuestas.GuardarRespuestaAsync(Arg.Do<Respuesta>(respuesta => respuestas.Add(respuesta)), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _segmentadorIdeas.SegmentarAsync(Arg.Any<ContextoSegmentacionIdeas>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoSegmentacionIdeas.Exito(
                new[]
                {
                    new IdeaSegmentada(1, "Primera idea suficientemente larga para ser procesada.", null),
                    new IdeaSegmentada(2, "Segunda idea suficientemente larga para ser procesada.", null),
                },
                UsoTokensLlm.Crear(11, 4)));
        _evaluador.EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Exito(CrearEvaluacion(RecomendacionEvaluacion.Cerrar, null)));
        await PrepararConversacionAsync();

        await Construir().ProcesarMensajeEntranteAsync(
            ParticipanteConSegmentacion(),
            new MensajeEntrante(Numero, "Dos ideas", "wamid.multi-idea", Epoca),
            CancellationToken.None);

        await _segmentadorIdeas.Received(1).SegmentarAsync(Arg.Any<ContextoSegmentacionIdeas>(), Arg.Any<CancellationToken>());
        await _evaluador.Received(2).EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>());
        await _compilador.Received(2).CompilarAsync(Arg.Any<SolicitudCompilacion>(), Arg.Any<CancellationToken>());
        respuestas.Should().HaveCount(2);
        respuestas.Select(respuesta => respuesta.IdeaIndice).Should().Equal(1, 2);
        respuestas.Select(respuesta => respuesta.RespuestaPadreId).Should().OnlyContain(id => id!.StartsWith("wamid.", StringComparison.Ordinal));
        respuestas.Select(respuesta => respuesta.Id).Should().Equal("resp_wamid_multi_idea_1", "resp_wamid_multi_idea_2");
        await _gateway.Received(1).EnviarTextoAsync(
            Numero,
            Arg.Is<string>(texto => texto.Contains("Registramos 2 ideas", StringComparison.Ordinal)),
            TipoEnvioMensaje.Repregunta,
            Arg.Any<CancellationToken>());
        await _logSeguridad.Received(1).RegistrarAsync(
            Arg.Is<LogSeguridad>(log => log.TipoEvento == TipoEventoSeguridad.SegmentacionIdeas
                && log.Resultado == "segmentada"
                && log.Detalle!.Contains("ideas:2", StringComparison.Ordinal)
                && log.Detalle.Contains("promptTokens:11", StringComparison.Ordinal)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Procesar_KillSwitchSegmentacionApagado_MantieneFlujoUnaIdea()
    {
        _evaluador.EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Exito(CrearEvaluacion(RecomendacionEvaluacion.Cerrar, null)));
        await PrepararConversacionAsync();

        await Construir(new OpcionesConversacion { SegmentacionIdeas = false })
            .ProcesarMensajeEntranteAsync(ParticipanteConSegmentacion(), Mensaje("Una idea"), CancellationToken.None);

        await _segmentadorIdeas.DidNotReceiveWithAnyArgs().SegmentarAsync(default!, default);
        await _evaluador.Received(1).EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Procesar_SegmentacionInvalida_CaeAMensajeCompletoSinTrazabilidadDeIdea()
    {
        var respuestas = new List<Respuesta>();
        _respuestas.GuardarRespuestaAsync(Arg.Do<Respuesta>(respuesta => respuestas.Add(respuesta)), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _segmentadorIdeas.SegmentarAsync(Arg.Any<ContextoSegmentacionIdeas>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoSegmentacionIdeas.Fallback("salida_invalida:no_json", UsoTokensLlm.Crear(3, 2)));
        _evaluador.EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Exito(CrearEvaluacion(RecomendacionEvaluacion.Cerrar, null)));
        await PrepararConversacionAsync();

        await Construir().ProcesarMensajeEntranteAsync(ParticipanteConSegmentacion(), Mensaje("Idea completa de respaldo"), CancellationToken.None);

        await _evaluador.Received(1).EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>());
        respuestas.Should().ContainSingle();
        respuestas[0].IdeaIndice.Should().BeNull();
        respuestas[0].RespuestaPadreId.Should().BeNull();
        await _logSeguridad.Received(1).RegistrarAsync(
            Arg.Is<LogSeguridad>(log => log.TipoEvento == TipoEventoSeguridad.SegmentacionIdeas
                && log.Resultado == "fallback"
                && log.Detalle!.Contains("motivo:salida_invalida:no_json", StringComparison.Ordinal)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Procesar_SegmentacionDescartaDuplicadosYTruncaAntesDeEvaluar()
    {
        _segmentadorIdeas.SegmentarAsync(Arg.Any<ContextoSegmentacionIdeas>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoSegmentacionIdeas.Exito(
                new[]
                {
                    new IdeaSegmentada(1, "Idea uno suficientemente larga para pasar el minimo.", null),
                    new IdeaSegmentada(2, "  Idea uno suficientemente larga para pasar el minimo.  ", null),
                    new IdeaSegmentada(3, "Idea dos suficientemente larga para pasar el minimo.", null),
                    new IdeaSegmentada(4, "Idea tres suficientemente larga para pasar el minimo.", null),
                    new IdeaSegmentada(5, "corta", null),
                },
                UsoTokensLlm.Crear(1, 1)));
        _evaluador.EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Exito(CrearEvaluacion(RecomendacionEvaluacion.Cerrar, null)));
        await PrepararConversacionAsync();

        await Construir(new OpcionesConversacion { MaxIdeasPorMensaje = 2, LongitudMinimaIdea = 30 })
            .ProcesarMensajeEntranteAsync(ParticipanteConSegmentacion(), Mensaje("Varias ideas"), CancellationToken.None);

        await _evaluador.Received(2).EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>());
        await _logSeguridad.Received(1).RegistrarAsync(
            Arg.Is<LogSeguridad>(log => log.TipoEvento == TipoEventoSeguridad.SegmentacionIdeas
                && log.Detalle!.Contains("truncada:True", StringComparison.Ordinal)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Procesar_TejidoActivo_RecuperaAportesLosInyectaAlEvaluarYRegistraTelemetria()
    {
        ContextoEvaluacion? contextoVisto = null;
        _evaluador.EvaluarAsync(Arg.Do<ContextoEvaluacion>(c => contextoVisto = c), Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Exito(CrearEvaluacion(RecomendacionEvaluacion.Cerrar, null)));
        _baseConocimiento.RecuperarAsync(
                "c_1", Arg.Any<string>(), Arg.Any<IReadOnlyCollection<string>>(),
                "u_1", Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new[] { new AporteRelevante("huerta comunitaria", new[] { "verde" }, Epoca) });
        await PrepararConversacionAsync();

        await Construir().ProcesarMensajeEntranteAsync(ParticipanteConTejido(), Mensaje("Mi idea sobre huertas"), CancellationToken.None);

        // Excluye al propio autor y a la conversación en curso, y respeta el topK por defecto (3).
        await _baseConocimiento.Received(1).RecuperarAsync(
            "c_1", "Mi idea sobre huertas", Arg.Any<IReadOnlyCollection<string>>(),
            "u_1", "conv_c_1_u_1_p_1", 3, Arg.Any<CancellationToken>());
        contextoVisto.Should().NotBeNull();
        contextoVisto!.AportesComunidad.Should().ContainSingle().Which.Should().Contain("huerta comunitaria");
        await _logSeguridad.Received(1).RegistrarAsync(
            Arg.Is<LogSeguridad>(l => l.TipoEvento == TipoEventoSeguridad.TejidoColectivo
                && l.Resultado == "tejida"
                && l.Detalle!.Contains("recuperados:1", StringComparison.Ordinal)
                && l.Detalle.Contains("tejidos:1", StringComparison.Ordinal)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Procesar_CampaniaSinTejido_NoLlamaRecuperacionNiInyecta()
    {
        ContextoEvaluacion? contextoVisto = null;
        _evaluador.EvaluarAsync(Arg.Do<ContextoEvaluacion>(c => contextoVisto = c), Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Exito(CrearEvaluacion(RecomendacionEvaluacion.Cerrar, null)));
        await PrepararConversacionAsync();

        await Construir().ProcesarMensajeEntranteAsync(Participante(), Mensaje("Mi idea"), CancellationToken.None);

        await _baseConocimiento.DidNotReceiveWithAnyArgs().RecuperarAsync(
            default!, default!, default!, default!, default, default, default);
        contextoVisto!.AportesComunidad.Should().BeEmpty();
    }

    [Fact]
    public async Task Procesar_KillSwitchGlobalApagado_NoLlamaRecuperacionAunqueLaCampaniaLoActive()
    {
        _evaluador.EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Exito(CrearEvaluacion(RecomendacionEvaluacion.Cerrar, null)));
        await PrepararConversacionAsync();

        await Construir(new OpcionesConversacion { TejidoColectivo = false })
            .ProcesarMensajeEntranteAsync(ParticipanteConTejido(), Mensaje("Mi idea"), CancellationToken.None);

        await _baseConocimiento.DidNotReceiveWithAnyArgs().RecuperarAsync(
            default!, default!, default!, default!, default, default, default);
        await _evaluador.Received(1).EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Procesar_RecuperacionFalla_DegradaAutocontenidoSinRomperElHilo()
    {
        ContextoEvaluacion? contextoVisto = null;
        _evaluador.EvaluarAsync(Arg.Do<ContextoEvaluacion>(c => contextoVisto = c), Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Exito(CrearEvaluacion(RecomendacionEvaluacion.Cerrar, null)));
        _baseConocimiento
            .When(x => x.RecuperarAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyCollection<string>>(),
                Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>()))
            .Do(_ => throw new InvalidOperationException("cosmos caido"));
        await PrepararConversacionAsync();

        await Construir().ProcesarMensajeEntranteAsync(ParticipanteConTejido(), Mensaje("Mi idea"), CancellationToken.None);

        // El hilo sigue: evalúa sin aportes (autocontenido) y ofrece la mejora como siempre.
        await _evaluador.Received(1).EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>());
        contextoVisto!.AportesComunidad.Should().BeEmpty();
        await _gateway.Received(1).EnviarTextoAsync(Numero, Arg.Any<string>(), TipoEnvioMensaje.Repregunta, Arg.Any<CancellationToken>());
        await _logSeguridad.Received(1).RegistrarAsync(
            Arg.Is<LogSeguridad>(l => l.TipoEvento == TipoEventoSeguridad.TejidoColectivo
                && l.Resultado == "error"
                && l.Detalle!.Contains("error:True", StringComparison.Ordinal)),
            Arg.Any<CancellationToken>());
    }

    private static ParticipanteResuelto ParticipanteConTejido()
    {
        var pregunta = CrearPregunta("p_1", 1, 1);
        var campania = CrearCampania(
            new[] { pregunta },
            configConversacional: ConfigConversacional.Crear(1, "Gracias por participar.", tejidoColectivo: true));
        var usuario = FabricasDominio.CrearUsuario("u_1", Numero, RolUsuario.Participante);
        var participante = FabricasDominio.CrearParticipante("pc_1", "c_1", "u_1", Numero);
        return new ParticipanteResuelto(usuario, campania, participante, pregunta);
    }

    private OrquestadorConversacion Construir(OpcionesConversacion? opciones = null)
        => new(
            _conversaciones,
            _respuestas,
            _participantes,
            _configuracion,
            _evaluador,
            _segmentadorIdeas,
            _baseConocimiento,
            _compilador,
            _gateway,
            _logSeguridad,
            _correlacion,
            opciones ?? new OpcionesConversacion(),
            _reloj);

    private Task PrepararConversacionAsync()
        => _conversaciones.GuardarConversacionAsync(
            DominioConversacion.Iniciar("conv_c_1_u_1_p_1", "c_1", "u_1", "p_1", "whatsapp", null, Epoca),
            CancellationToken.None);

    /// <summary>Persiste un Mensaje(in) previo en el hilo estandar, para los contadores de cupos.</summary>
    private Task SembrarEntranteAsync(string texto)
        => _conversaciones.GuardarMensajeAsync(
            ElTejido.Domain.Conversaciones.Mensaje.Crear(
                "msg_seed_" + Guid.NewGuid().ToString("N"),
                "c_1",
                "conv_c_1_u_1_p_1",
                DireccionMensaje.In,
                texto,
                "wamid.seed",
                Epoca),
            CancellationToken.None);

    private static ParticipanteResuelto Participante(int maxRepreguntas = 1)
    {
        var pregunta = CrearPregunta("p_1", 1, maxRepreguntas);
        var campania = CrearCampania(new[] { pregunta });
        var usuario = FabricasDominio.CrearUsuario("u_1", Numero, RolUsuario.Participante);
        var participante = FabricasDominio.CrearParticipante("pc_1", "c_1", "u_1", Numero);
        return new ParticipanteResuelto(usuario, campania, participante, pregunta);
    }

    private static ParticipanteResuelto ParticipanteConSegmentacion()
    {
        var pregunta = CrearPregunta("p_1", 1, 1);
        var campania = CrearCampania(
            new[] { pregunta },
            configConversacional: ConfigConversacional.Crear(1, "Gracias por participar.", segmentacionIdeas: true));
        var usuario = FabricasDominio.CrearUsuario("u_1", Numero, RolUsuario.Participante);
        var participante = FabricasDominio.CrearParticipante("pc_1", "c_1", "u_1", Numero);
        return new ParticipanteResuelto(usuario, campania, participante, pregunta);
    }

    private static ParticipanteResuelto ParticipanteFrio()
    {
        var pregunta = FabricasDominio.CrearPregunta("p_1", 1);
        var campania = CrearCampania(new[] { pregunta });
        var usuario = FabricasDominio.CrearUsuario("u_1", Numero, RolUsuario.Participante);
        // estadoEnvio = Pendiente: el envio inicial de campania nunca se hizo (primer contacto en frio).
        var participante = ParticipanteCampania.Crear(
            "pc_1", "c_1", "u_1", NumeroWhatsApp.FromNormalized(Numero),
            EstadoRegistro.Activo, EstadoEnvio.Pendiente, EstadoRespuestaParticipante.SinRespuesta,
            Epoca, null, null);
        return new ParticipanteResuelto(usuario, campania, participante, pregunta);
    }

    private static ParticipanteResuelto ParticipanteConMensajeInicial(string textoInicial)
    {
        var pregunta = CrearPregunta("p_1", 1, 1);
        var mensajeInicial = MensajeInicial.Crear(
            "mi_1", "bienvenida", textoInicial, 1, variablesDinamicas: null, EstadoRegistro.Activo, plantillaWhatsApp: null);
        var campania = CrearCampania(new[] { pregunta }, new[] { mensajeInicial });
        var usuario = FabricasDominio.CrearUsuario("u_1", Numero, RolUsuario.Participante);
        var participante = FabricasDominio.CrearParticipante("pc_1", "c_1", "u_1", Numero);
        return new ParticipanteResuelto(usuario, campania, participante, pregunta);
    }

    /// <summary>Participante cuya campania define limites de cupo propios (10 §2).</summary>
    private static ParticipanteResuelto ParticipanteConCupos(
        int maxMensajesPorUsuario,
        int maxLlamadasLlm,
        bool dosPreguntas = false,
        int presupuestoTokens = 0)
    {
        var pregunta1 = CrearPregunta("p_1", 1, 1);
        var preguntas = dosPreguntas
            ? new[] { pregunta1, CrearPregunta("p_2", 2, 1) }
            : new[] { pregunta1 };
        var campania = CrearCampania(
            preguntas,
            limites: LimitesSeguridad.Crear(1500, maxMensajesPorUsuario, maxLlamadasLlm, presupuestoTokens));
        var usuario = FabricasDominio.CrearUsuario("u_1", Numero, RolUsuario.Participante);
        var participante = FabricasDominio.CrearParticipante("pc_1", "c_1", "u_1", Numero);
        return new ParticipanteResuelto(usuario, campania, participante, pregunta1);
    }

    private static ParticipanteResuelto ParticipanteMultipregunta()
    {
        var pregunta1 = FabricasDominio.CrearPregunta("p_1", 1);
        var pregunta2 = FabricasDominio.CrearPregunta("p_2", 2);
        var campania = CrearCampania(new[] { pregunta1, pregunta2 });
        var usuario = FabricasDominio.CrearUsuario("u_1", Numero, RolUsuario.Participante);
        var participante = FabricasDominio.CrearParticipante("pc_1", "c_1", "u_1", Numero);
        return new ParticipanteResuelto(usuario, campania, participante, pregunta1);
    }

    private static MensajeEntrante Mensaje(string texto) => new(Numero, texto, "wamid." + Guid.NewGuid().ToString("N"), Epoca);

    private static Pregunta CrearPregunta(string id, int orden, int maxRepreguntas)
        => Pregunta.Crear(
            id,
            $"Pregunta {orden}",
            "Instruccion",
            "categoria",
            orden,
            EstadoRegistro.Activo,
            rubricaRef: null,
            versionRubrica: null,
            promptRefs: null,
            maxRepreguntas,
            LimitesSeguridad.ParaPregunta(1500, 2),
            ConfigMarkdown.Crear(TipoArtefactoMarkdown.Respuesta));

    private static Campania CrearCampania(
        IEnumerable<Pregunta> preguntas,
        IEnumerable<MensajeInicial>? mensajesIniciales = null,
        LimitesSeguridad? limites = null,
        ConfigConversacional? configConversacional = null)
        => Campania.Crear(
            "c_1", "Campania c_1", "Descripcion", "Objetivo", EstadoCampania.Activa,
            mensajesIniciales, preguntas,
            "rub_1",
            new Dictionary<string, string> { ["evaluar"] = "pr_eval" },
            "llm_1",
            ConfigMarkdown.Crear(TipoArtefactoMarkdown.Respuesta),
            configConversacional ?? ConfigConversacional.Crear(1, "Gracias por participar."),
            limites ?? LimitesSeguridad.Crear(1500, 10, 2),
            usuariosHabilitados: null, Epoca, Epoca);

    private static DominioEvaluacion CrearEvaluacion(RecomendacionEvaluacion recomendacion, string? repregunta, string retro = "Buena idea", decimal calificacionTotal = 4m)
        => DominioEvaluacion.Crear(
            "eval_1", "c_1", "resp_1", "u_1", "p_1", "rub_1", 1, "pr_eval", 1, "llm_1",
            new ConfigLlmSnapshot("AzureOpenAI", "gpt-4o-mini", "https://x", new Dictionary<string, object?>()),
            new Dictionary<string, decimal> { ["claridad"] = 1m },
            new[] { CalificacionCriterio.Crear("claridad", 4m, "clara") },
            calificacionTotal, "explica", retro, recomendacion, repregunta, new[] { "tema" }, new[] { "ent" }, false, Epoca);

    private static Rubrica CrearRubrica(EstadoRubrica estado = EstadoRubrica.Activa)
        => Rubrica.Crear("rub_1", "Rubrica", "desc", "# Rubrica", EscalaRubrica.Crear(1, 5),
            new[] { CriterioRubrica.Crear("claridad", 1m) }, 1, estado, Epoca, Epoca);

    private static Prompt CrearPrompt(EstadoPrompt estado = EstadoPrompt.Activo, bool aprobado = true)
        => Prompt.Crear("pr_eval", "Prompt", "evaluar", "Eres evaluador.", 1, estado,
            aprobado ? "u_admin" : null, aprobado ? Epoca : null, Epoca, Epoca);

    private static ConfigLlm CrearConfig(EstadoRegistro estado = EstadoRegistro.Activo)
        => ConfigLlm.Crear("llm_1", "Azure", "AzureOpenAI", "gpt-4o-mini", "https://x", "llm-key", null,
            LimitesTokensLlm.Crear(6000, 800), 30, 2, estado, Epoca, Epoca);

    private sealed class FakeConversaciones : IRepositorioConversaciones
    {
        private readonly Dictionary<string, DominioConversacion> _conversaciones = new(StringComparer.Ordinal);
        private readonly List<Mensaje> _mensajes = new();

        public DominioConversacion? Ultima { get; private set; }

        public IReadOnlyCollection<DominioConversacion> Conversaciones => _conversaciones.Values.ToArray();

        public int MensajesGuardados => _mensajes.Count;

        public Task GuardarConversacionAsync(DominioConversacion conversacion, CancellationToken cancellationToken)
        {
            _conversaciones[conversacion.Id] = conversacion;
            Ultima = conversacion;
            return Task.CompletedTask;
        }

        public Task<DominioConversacion?> ObtenerConversacionAsync(string campaniaId, string conversacionId, CancellationToken cancellationToken)
            => Task.FromResult(_conversaciones.GetValueOrDefault(conversacionId));

        public Task<IReadOnlyCollection<DominioConversacion>> ListarConversacionesAsync(string campaniaId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<DominioConversacion>>(_conversaciones.Values.Where(c => c.CampaniaId == campaniaId).ToArray());

        public Task<IReadOnlyCollection<DominioConversacion>> ListarAbiertasInactivasAsync(DateTimeOffset limite, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<DominioConversacion>>(
                _conversaciones.Values.Where(c => c.Estado == EstadoConversacion.Abierta).ToArray());

        public Task<IReadOnlyCollection<Mensaje>> ListarMensajesAsync(string campaniaId, string conversacionId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<Mensaje>>(
                _mensajes.Where(m => m.CampaniaId == campaniaId && m.ConversacionId == conversacionId).ToArray());

        public Task GuardarMensajeAsync(Mensaje mensaje, CancellationToken cancellationToken)
        {
            _mensajes.Add(mensaje);
            return Task.CompletedTask;
        }

        public Task<ConteoBorradoConversaciones> EliminarPorUsuarioAsync(string campaniaId, string? usuarioId, CancellationToken cancellationToken)
        {
            var conversaciones = _conversaciones.Values
                .Where(c => c.CampaniaId == campaniaId && (usuarioId is null || c.UsuarioId == usuarioId))
                .ToArray();
            var ids = conversaciones.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
            foreach (var c in conversaciones)
            {
                _conversaciones.Remove(c.Id);
            }

            var mensajes = _mensajes.RemoveAll(m => m.CampaniaId == campaniaId && ids.Contains(m.ConversacionId));
            return Task.FromResult(new ConteoBorradoConversaciones(conversaciones.Length, mensajes));
        }
    }
}
