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
    public async Task Procesar_RecomendacionCerrar_EnviaCierreCompilaYCierra()
    {
        _evaluador.EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Exito(CrearEvaluacion(RecomendacionEvaluacion.Cerrar, null)));

        await Construir().ProcesarMensajeEntranteAsync(Participante(), Mensaje("Mi idea"), CancellationToken.None);

        await _gateway.Received(1).EnviarTextoAsync(Numero, Arg.Any<string>(), TipoEnvioMensaje.Cierre, Arg.Any<CancellationToken>());
        await _compilador.Received(1).CompilarAsync(Arg.Any<SolicitudCompilacion>(), Arg.Any<CancellationToken>());
        _conversaciones.Ultima!.Estado.Should().Be(EstadoConversacion.Cerrada);
        await _respuestas.Received().GuardarRespuestaAsync(
            Arg.Is<Respuesta>(r => r.Estado == EstadoRespuesta.Evaluada), Arg.Any<CancellationToken>());
        await _participantes.Received().GuardarParticipanteAsync(
            Arg.Is<ParticipanteCampania>(p => p.EstadoRespuesta == EstadoRespuestaParticipante.Respondio), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Procesar_Repreguntar_EnviaRepreguntaYNoCierra()
    {
        _evaluador.EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Exito(CrearEvaluacion(RecomendacionEvaluacion.Repreguntar, "¿Cuanto ahorra?")));

        await Construir().ProcesarMensajeEntranteAsync(Participante(), Mensaje("Mi idea"), CancellationToken.None);

        await _gateway.Received(1).EnviarTextoAsync(Numero, Arg.Any<string>(), TipoEnvioMensaje.Repregunta, Arg.Any<CancellationToken>());
        await _compilador.DidNotReceiveWithAnyArgs().CompilarAsync(default!, default);
        _conversaciones.Ultima!.Estado.Should().Be(EstadoConversacion.Abierta);
        _conversaciones.Ultima!.EstadoMaquina.Should().Be(EstadoMaquinaConversacion.EsperandoRepregunta);
        _conversaciones.Ultima!.RepreguntasUsadas.Should().Be(1);
    }

    [Fact]
    public async Task Procesar_SegundoTurno_SiempreCierra_TopeUnaRepregunta()
    {
        _evaluador.EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Exito(CrearEvaluacion(RecomendacionEvaluacion.Repreguntar, "¿Otra?")));
        var orquestador = Construir();

        await orquestador.ProcesarMensajeEntranteAsync(Participante(), Mensaje("Idea"), CancellationToken.None);
        await orquestador.ProcesarMensajeEntranteAsync(Participante(), Mensaje("Repregunta respondida"), CancellationToken.None);

        // Segundo turno: aunque el LLM sugiera repreguntar, el tope (1) obliga a cerrar.
        await _gateway.Received(1).EnviarTextoAsync(Numero, Arg.Any<string>(), TipoEnvioMensaje.Cierre, Arg.Any<CancellationToken>());
        _conversaciones.Ultima!.Estado.Should().Be(EstadoConversacion.Cerrada);
        await _evaluador.Received(2).EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Procesar_Fallback_CierraNeutroYRespuestaPendiente()
    {
        _evaluador.EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Fallback(CrearEvaluacion(RecomendacionEvaluacion.Cerrar, null, EvaluadorLlm.RetroNeutra), "error_proveedor"));

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

        await Construir().ProcesarMensajeEntranteAsync(Participante(), Mensaje("Mi idea"), CancellationToken.None);

        await _evaluador.DidNotReceiveWithAnyArgs().EvaluarAsync(default!, default);
        await _gateway.Received(1).EnviarTextoAsync(Numero, Arg.Any<string>(), TipoEnvioMensaje.Cierre, Arg.Any<CancellationToken>());
        _conversaciones.Ultima!.Estado.Should().Be(EstadoConversacion.Cerrada);
    }

    [Fact]
    public async Task Procesar_ConfigLlmInactiva_CierraNeutroSinEvaluarYRegistraFallback()
    {
        _configuracion.ObtenerConfigLlmAsync("llm_1", Arg.Any<CancellationToken>())
            .Returns(CrearConfig(EstadoRegistro.Inactivo));

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
    public async Task Procesar_SegundoMensajeTrasPrimerContacto_EvaluaComoRespuesta()
    {
        _evaluador.EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Exito(CrearEvaluacion(RecomendacionEvaluacion.Cerrar, null)));
        var orquestador = Construir();
        var frio = ParticipanteFrio();

        await orquestador.ProcesarMensajeEntranteAsync(frio, Mensaje("Hola"), CancellationToken.None);
        await orquestador.ProcesarMensajeEntranteAsync(frio, Mensaje("Mi idea real"), CancellationToken.None);

        // El primer entrante solo envia la pregunta (Inicial); el segundo ya se evalua y cierra.
        await _evaluador.Received(1).EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>());
        await _gateway.Received(1).EnviarTextoAsync(Numero, Arg.Any<string>(), TipoEnvioMensaje.Inicial, Arg.Any<CancellationToken>());
        await _gateway.Received(1).EnviarTextoAsync(Numero, Arg.Any<string>(), TipoEnvioMensaje.Cierre, Arg.Any<CancellationToken>());
        _conversaciones.Ultima!.Estado.Should().Be(EstadoConversacion.Cerrada);
    }

    private OrquestadorConversacion Construir()
        => new(
            _conversaciones,
            _respuestas,
            _participantes,
            _configuracion,
            _evaluador,
            _compilador,
            _gateway,
            _logSeguridad,
            _correlacion,
            _reloj);

    private static ParticipanteResuelto Participante()
    {
        var pregunta = FabricasDominio.CrearPregunta("p_1", 1);
        var campania = CrearCampania(new[] { pregunta });
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

    private static MensajeEntrante Mensaje(string texto) => new(Numero, texto, "wamid." + Guid.NewGuid().ToString("N"), Epoca);

    private static Campania CrearCampania(IEnumerable<Pregunta> preguntas)
        => Campania.Crear(
            "c_1", "Campania c_1", "Descripcion", "Objetivo", EstadoCampania.Activa,
            mensajesIniciales: null, preguntas,
            "rub_1",
            new Dictionary<string, string> { ["evaluar"] = "pr_eval" },
            "llm_1",
            ConfigMarkdown.Crear(TipoArtefactoMarkdown.Respuesta),
            ConfigConversacional.Crear(1, "Gracias por participar."),
            LimitesSeguridad.Crear(1500, 10, 2),
            usuariosHabilitados: null, Epoca, Epoca);

    private static DominioEvaluacion CrearEvaluacion(RecomendacionEvaluacion recomendacion, string? repregunta, string retro = "Buena idea")
        => DominioEvaluacion.Crear(
            "eval_1", "c_1", "resp_1", "u_1", "p_1", "rub_1", 1, "pr_eval", 1, "llm_1",
            new ConfigLlmSnapshot("AzureOpenAI", "gpt-4o-mini", "https://x", new Dictionary<string, object?>()),
            new Dictionary<string, decimal> { ["claridad"] = 1m },
            new[] { CalificacionCriterio.Crear("claridad", 4m, "clara") },
            4m, "explica", retro, recomendacion, repregunta, new[] { "tema" }, new[] { "ent" }, false, Epoca);

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

        public DominioConversacion? Ultima { get; private set; }

        public int MensajesGuardados { get; private set; }

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

        public Task<IReadOnlyCollection<Mensaje>> ListarMensajesAsync(string campaniaId, string conversacionId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<Mensaje>>(Array.Empty<Mensaje>());

        public Task GuardarMensajeAsync(Mensaje mensaje, CancellationToken cancellationToken)
        {
            MensajesGuardados++;
            return Task.CompletedTask;
        }
    }
}
