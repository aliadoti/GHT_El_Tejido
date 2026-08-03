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
        _gateway.EnviarTextoAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TipoEnvioMensaje>(), Arg.Any<CancellationToken>(), Arg.Any<string?>())
            .Returns(EnvioResultado.Ok("wamid.out"));
    }

    [Fact]
    public async Task I19_PrimerAporte_CreaPropuestaYNoEvaluaHastaConfirmacion()
    {
        var consolidador = Substitute.For<IConsolidadorIdeas>();
        consolidador.ConsolidarAsync(Arg.Any<ContextoConsolidacionIdeas>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoConsolidacionIdeas.Exito("Idea consolidada completa.", TipoAporteIdea.Inicial, [], false, null, false, null));
        await PrepararConversacionAsync();

        await Construir(consolidador: consolidador).ProcesarMensajeEntranteAsync(Participante(), Mensaje("Aporte inicial"), CancellationToken.None);

        await _evaluador.DidNotReceive().EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>());
        await _respuestas.Received(1).GuardarRespuestaAsync(
            Arg.Is<Respuesta>(r => r.Texto == "Aporte inicial" && r.TipoAporte == TipoAporteIdea.Inicial && r.IdeaId != null),
            Arg.Any<CancellationToken>());
        await _respuestas.Received(1).GuardarVersionIdeaAsync(
            Arg.Is<VersionIdeaConsolidada>(v => v.Texto == "Idea consolidada completa." && v.EstadoConfirmacion == EstadoConfirmacionVersionIdea.Propuesta),
            Arg.Any<CancellationToken>());
        await _gateway.Received(1).EnviarTextoAsync(
            Numero, Arg.Is<string>(t => t.Contains("¿Es correcto?", StringComparison.Ordinal)), TipoEnvioMensaje.Repregunta,
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("Quiero parar aquí")]
    [InlineData("quiero pasar a otra idea")]
    [InlineData("stop now")]
    public async Task P27_AliasDeSalidaEnRepregunta_NoSeGuardaNiEvaluaComoAporte(string texto)
    {
        await _conversaciones.GuardarConversacionAsync(
            DominioConversacion.Crear(
                "conv_c_1_u_1_p_1", "c_1", "u_1", "p_1", "whatsapp", EstadoConversacion.Abierta,
                EstadoMaquinaConversacion.EsperandoRepregunta, repreguntasUsadas: 1, Epoca.AddHours(24), null,
                Epoca, fechaCierre: null),
            CancellationToken.None);

        await Construir().ProcesarMensajeEntranteAsync(Participante(), Mensaje(texto), CancellationToken.None);

        await _evaluador.DidNotReceive().EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>());
        await _respuestas.DidNotReceive().GuardarRespuestaAsync(Arg.Any<Respuesta>(), Arg.Any<CancellationToken>());
        await _gateway.Received(1).EnviarTextoAsync(
            Numero, Arg.Any<string>(), TipoEnvioMensaje.Cierre, Arg.Any<CancellationToken>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task P25_PrimerAporteSustantivo_SeEvaluaYRecibeCoachingSinConfirmacionRepetitiva()
    {
        var almacen = ConfigurarAlmacenIdeas();
        ContextoEvaluacion? contextoEvaluado = null;
        _evaluador.EvaluarAsync(
                Arg.Do<ContextoEvaluacion>(contexto => contextoEvaluado = contexto),
                Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Exito(
                CrearEvaluacion(
                    RecomendacionEvaluacion.Repreguntar,
                    "¿Qué debe contener la presentación?",
                    "Hacer una presentación en PowerPoint es válido; vamos a detallarla.",
                    calificacionTotal: 1m)));
        var redactor = RedactorQueDevuelve(
            "La presentación es un punto de partida válido.",
            "¿Qué debería contener para que el mensaje sea memorable?");
        await PrepararConversacionAsync();

        await Construir(
                new OpcionesConversacion { ConfirmacionExplicitaIdeasHabilitada = false },
                ConsolidadorQueAcumula(),
                redactor)
            .ProcesarMensajeEntranteAsync(
                Participante(maxRepreguntas: 10),
                Mensaje("Hagamos una presentación en PowerPoint y la mostramos"),
                CancellationToken.None);

        contextoEvaluado.Should().NotBeNull();
        contextoEvaluado!.RespuestaTexto.Should().Be("Hagamos una presentación en PowerPoint y la mostramos");
        almacen.Ideas.Values.Should().ContainSingle().Which.VersionConfirmadaRef.Should().NotBeNull();
        await _gateway.Received(1).EnviarTextoAsync(
            Numero,
            Arg.Is<string>(texto =>
                texto.Contains("La presentación es un punto de partida válido", StringComparison.Ordinal)
                && texto.Contains("Hacer una presentación en PowerPoint es válido", StringComparison.Ordinal)
                && texto.Contains("¿Qué debería contener para que el mensaje sea memorable?", StringComparison.Ordinal)),
            TipoEnvioMensaje.Repregunta,
            Arg.Any<CancellationToken>());
        await redactor.Received(1).RedactarAsync(
            Arg.Is<ContextoRedaccionTurno>(contexto => contexto.Acto == ActoConversacional.Mejorar),
            Arg.Any<CancellationToken>());
        await redactor.DidNotReceive().RedactarAsync(
            Arg.Is<ContextoRedaccionTurno>(contexto => contexto.Acto == ActoConversacional.Confirmar),
            Arg.Any<CancellationToken>());
        await _gateway.DidNotReceive().EnviarTextoAsync(
            Numero,
            Arg.Is<string>(texto =>
                texto.Contains("¿Es correcto?", StringComparison.Ordinal)
                || texto.Contains("Entendí que propones", StringComparison.OrdinalIgnoreCase)),
            Arg.Any<TipoEnvioMensaje>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task P25_Complemento_SeEvaluaComoVersionCompletaEnElMismoTurno()
    {
        ConfigurarAlmacenIdeas();
        var contextos = new List<ContextoEvaluacion>();
        _evaluador.EvaluarAsync(
                Arg.Do<ContextoEvaluacion>(contexto => contextos.Add(contexto)),
                Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Exito(
                CrearEvaluacion(
                    RecomendacionEvaluacion.Repreguntar,
                    "¿Qué resultado esperas?",
                    calificacionTotal: 1m)));
        await PrepararConversacionAsync();
        var orquestador = Construir(
            new OpcionesConversacion { ConfirmacionExplicitaIdeasHabilitada = false },
            ConsolidadorQueAcumula());

        await orquestador.ProcesarMensajeEntranteAsync(
            Participante(maxRepreguntas: 10), Mensaje("Aporte inicial"), CancellationToken.None);
        await orquestador.ProcesarMensajeEntranteAsync(
            Participante(maxRepreguntas: 10), Mensaje("Incluye casos reales y una demostración"), CancellationToken.None);

        contextos.Select(contexto => contexto.RespuestaTexto).Should().Equal(
            "Aporte inicial",
            "Aporte inicial + Incluye casos reales y una demostración");
        await _gateway.DidNotReceive().EnviarTextoAsync(
            Numero,
            Arg.Is<string>(texto => texto.Contains("¿Es correcto?", StringComparison.Ordinal)),
            Arg.Any<TipoEnvioMensaje>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task I19_Confirmacion_EvaluaVersionCompletaEnLugarDelUltimoMensaje()
    {
        var consolidador = Substitute.For<IConsolidadorIdeas>();
        consolidador.ConsolidarAsync(Arg.Any<ContextoConsolidacionIdeas>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoConsolidacionIdeas.Exito("Idea consolidada completa.", TipoAporteIdea.Inicial, [], false, null, false, null));
        IdeaConsolidada? ideaGuardada = null;
        VersionIdeaConsolidada? versionGuardada = null;
        _respuestas.GuardarIdeaConsolidadaAsync(Arg.Do<IdeaConsolidada>(idea => ideaGuardada = idea), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _respuestas.GuardarVersionIdeaAsync(Arg.Do<VersionIdeaConsolidada>(version => versionGuardada = version), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _respuestas.ListarIdeasConsolidadasAsync("c_1", Arg.Any<CancellationToken>())
            .Returns(_ => ideaGuardada is null ? Array.Empty<IdeaConsolidada>() : new[] { ideaGuardada });
        _respuestas.ObtenerVersionIdeaAsync("c_1", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => versionGuardada);
        ContextoEvaluacion? contextoEvaluado = null;
        _evaluador.EvaluarAsync(Arg.Do<ContextoEvaluacion>(contexto => contextoEvaluado = contexto), Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Exito(CrearEvaluacion(RecomendacionEvaluacion.Cerrar, null, calificacionTotal: 4m)));
        await PrepararConversacionAsync();
        var orquestador = Construir(consolidador: consolidador);

        await orquestador.ProcesarMensajeEntranteAsync(Participante(), Mensaje("Aporte inicial"), CancellationToken.None);
        await orquestador.ProcesarMensajeEntranteAsync(Participante(), Mensaje("sí"), CancellationToken.None);

        contextoEvaluado.Should().NotBeNull();
        contextoEvaluado!.RespuestaTexto.Should().Be("Idea consolidada completa.");
        contextoEvaluado.IdeaId.Should().Be(ideaGuardada!.Id);
        contextoEvaluado.VersionIdeaId.Should().Be(versionGuardada!.Id);
        await _respuestas.Received(1).GuardarEvaluacionAsync(
            Arg.Is<DominioEvaluacion>(evaluacion => evaluacion.IdeaId == ideaGuardada.Id && evaluacion.VersionIdeaId == versionGuardada.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task I19_CorreccionAntesDeConfirmar_AcumulaElAporteInicialYEncadenaLaVersion()
    {
        var almacen = ConfigurarAlmacenIdeas();
        await PrepararConversacionAsync();
        var orquestador = Construir(consolidador: ConsolidadorQueAcumula());

        await orquestador.ProcesarMensajeEntranteAsync(Participante(), Mensaje("Aporte inicial"), CancellationToken.None);
        await orquestador.ProcesarMensajeEntranteAsync(Participante(), Mensaje("En realidad seria con soporte"), CancellationToken.None);

        await _evaluador.DidNotReceive().EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>());
        almacen.Versiones.Should().HaveCount(2);
        var segunda = almacen.Versiones.Values.Single(version => version.NumeroVersion == 2);
        var primera = almacen.Versiones.Values.Single(version => version.NumeroVersion == 1);
        segunda.VersionAnteriorId.Should().Be(primera.Id);
        segunda.Origen.Should().Be(TipoAporteIdea.Correccion);
        segunda.AporteIdsAcumulados.Should().HaveCount(2);
        segunda.Texto.Should().Be("Aporte inicial + En realidad seria con soporte");
    }

    [Fact]
    public async Task I19_ColaMultiIdea_ProponeLaPrimeraIdeaYNoEvaluaNingunaRaiz()
    {
        var almacen = ConfigurarAlmacenIdeas();
        SegmentarEnDosIdeas();
        await PrepararConversacionAsync();

        await Construir(consolidador: ConsolidadorQueAcumula()).ProcesarMensajeEntranteAsync(
            ParticipanteConCoaching(),
            new MensajeEntrante(Numero, "Dos ideas", "wamid.i19", Epoca),
            CancellationToken.None);

        await _evaluador.DidNotReceive().EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>());
        almacen.Respuestas.Should().HaveCount(2);
        almacen.Respuestas.Should().OnlyContain(respuesta =>
            respuesta.Estado == EstadoRespuesta.Recibida
            && respuesta.TipoAporte == TipoAporteIdea.Inicial
            && respuesta.IdeaId != null
            && respuesta.IdeaRaizId == respuesta.Id);
        almacen.Ideas.Values.Should().HaveCount(2).And.OnlyContain(
            idea => idea.EstadoFlujo == EstadoFlujoIdeaConsolidada.PendienteConfirmacion);
        var cola = _conversaciones.Ultima!.CoachingIdeas!;
        cola.IdeaActivaIndice.Should().Be(1);
        cola.IdeaActiva!.RepreguntasUsadas.Should().Be(0);
        cola.Ideas.Should().OnlyContain(idea => idea.IdeaId != null && idea.VersionIdeaVigenteId != null);
        await _gateway.Received(1).EnviarTextoAsync(
            Numero,
            Arg.Is<string>(texto =>
                texto.Contains("Primera idea", StringComparison.Ordinal)
                && texto.Contains("¿Es correcto?", StringComparison.Ordinal)
                && !texto.Contains("Segunda idea", StringComparison.Ordinal)),
            TipoEnvioMensaje.Repregunta,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task P25_ColaMultiIdea_EvaluaLaPrimeraSinPedirConfirmacion()
    {
        var almacen = ConfigurarAlmacenIdeas();
        var contextos = new List<ContextoEvaluacion>();
        _evaluador.EvaluarAsync(
                Arg.Do<ContextoEvaluacion>(contexto => contextos.Add(contexto)),
                Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Exito(
                CrearEvaluacion(
                    RecomendacionEvaluacion.Repreguntar,
                    "¿Qué resultado concreto buscas?",
                    calificacionTotal: 1m)));
        SegmentarEnDosIdeas();
        await PrepararConversacionAsync();

        await Construir(
                new OpcionesConversacion { ConfirmacionExplicitaIdeasHabilitada = false },
                ConsolidadorQueAcumula())
            .ProcesarMensajeEntranteAsync(
                ParticipanteConCoaching(),
                new MensajeEntrante(Numero, "Dos ideas", "wamid.p25", Epoca),
                CancellationToken.None);

        contextos.Should().ContainSingle();
        contextos.Single().RespuestaTexto.Should().Be("Primera idea suficientemente larga para ser procesada.");
        almacen.Ideas["idea_resp_wamid_p25_1"].VersionConfirmadaRef.Should().NotBeNull();
        almacen.Ideas["idea_resp_wamid_p25_2"].EstadoFlujo.Should()
            .Be(EstadoFlujoIdeaConsolidada.PendienteConfirmacion);
        _conversaciones.Ultima!.CoachingIdeas!.IdeaActiva!.RepreguntasUsadas.Should().Be(1);
        await _gateway.Received(1).EnviarTextoAsync(
            Numero,
            Arg.Is<string>(texto => texto.Contains("¿Qué resultado concreto buscas?", StringComparison.Ordinal)),
            TipoEnvioMensaje.Repregunta,
            Arg.Any<CancellationToken>());
        await _gateway.DidNotReceive().EnviarTextoAsync(
            Numero,
            Arg.Is<string>(texto => texto.Contains("¿Es correcto?", StringComparison.Ordinal)),
            Arg.Any<TipoEnvioMensaje>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task I19_ColaMultiIdea_ConfirmarLaPrimera_EvaluaSuVersionYPideConfirmarLaSegunda()
    {
        var almacen = ConfigurarAlmacenIdeas();
        var contextos = new List<ContextoEvaluacion>();
        _evaluador.EvaluarAsync(Arg.Do<ContextoEvaluacion>(contexto => contextos.Add(contexto)), Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Exito(CrearEvaluacion(RecomendacionEvaluacion.Cerrar, null, calificacionTotal: 4m)));
        SegmentarEnDosIdeas();
        await PrepararConversacionAsync();
        var orquestador = Construir(consolidador: ConsolidadorQueAcumula());

        await orquestador.ProcesarMensajeEntranteAsync(
            ParticipanteConCoaching(),
            new MensajeEntrante(Numero, "Dos ideas", "wamid.i19", Epoca),
            CancellationToken.None);
        await orquestador.ProcesarMensajeEntranteAsync(ParticipanteConCoaching(), Mensaje("si"), CancellationToken.None);

        contextos.Should().ContainSingle();
        contextos[0].RespuestaTexto.Should().Be("Primera idea suficientemente larga para ser procesada.");
        contextos[0].IdeaId.Should().Be("idea_resp_wamid_i19_1");
        var primera = almacen.Ideas["idea_resp_wamid_i19_1"];
        primera.EstadoFlujo.Should().Be(EstadoFlujoIdeaConsolidada.Cerrada);
        primera.EstadoResultado.Should().Be(EstadoResultadoIdeaConsolidada.Madura);
        primera.EstadoCuraduria.Should().Be(EstadoCuraduriaIdea.Pendiente);
        almacen.Ideas["idea_resp_wamid_i19_2"].EstadoFlujo.Should().Be(EstadoFlujoIdeaConsolidada.PendienteConfirmacion);
        var cola = _conversaciones.Ultima!.CoachingIdeas!;
        cola.Ideas[0].MotivoFinalizacion.Should().Be(MotivoFinalizacionIdea.Umbral);
        cola.IdeaActivaIndice.Should().Be(2);
        await _gateway.Received(1).EnviarTextoAsync(
            Numero,
            Arg.Is<string>(texto => texto.Contains("Segunda idea", StringComparison.Ordinal)
                && texto.Contains("¿Es correcto?", StringComparison.Ordinal)),
            TipoEnvioMensaje.Repregunta,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task I19_ColaMultiIdea_BajoUmbral_AcompaniaLaMismaIdeaAntesDeAvanzar()
    {
        ConfigurarAlmacenIdeas();
        _evaluador.EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Exito(
                CrearEvaluacion(RecomendacionEvaluacion.Repreguntar, "¿Que resultado esperas?", calificacionTotal: 1m)));
        SegmentarEnDosIdeas();
        await PrepararConversacionAsync();
        var orquestador = Construir(consolidador: ConsolidadorQueAcumula());

        await orquestador.ProcesarMensajeEntranteAsync(
            ParticipanteConCoaching(),
            new MensajeEntrante(Numero, "Dos ideas", "wamid.i19", Epoca),
            CancellationToken.None);
        await orquestador.ProcesarMensajeEntranteAsync(ParticipanteConCoaching(), Mensaje("si"), CancellationToken.None);

        var cola = _conversaciones.Ultima!.CoachingIdeas!;
        cola.IdeaActivaIndice.Should().Be(1);
        cola.IdeaActiva!.RepreguntasUsadas.Should().Be(1);
        await _gateway.Received(1).EnviarTextoAsync(
            Numero,
            Arg.Is<string>(texto => texto.Contains("¿Que resultado esperas?", StringComparison.Ordinal)),
            TipoEnvioMensaje.Repregunta,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task P24_SolicitarMejora_ConfirmaYEvaluaLaVersionSinCrearAporteOVersionNueva()
    {
        var almacen = ConfigurarAlmacenIdeas();
        var contextos = new List<ContextoEvaluacion>();
        _evaluador.EvaluarAsync(Arg.Do<ContextoEvaluacion>(contexto => contextos.Add(contexto)), Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Exito(
                CrearEvaluacion(RecomendacionEvaluacion.Repreguntar, "¿Qué resultado esperas?", calificacionTotal: 1m)));
        SegmentarEnDosIdeas();
        await PrepararConversacionAsync();
        var orquestador = Construir(consolidador: ConsolidadorQueAcumula());

        await orquestador.ProcesarMensajeEntranteAsync(
            ParticipanteConCoaching(),
            new MensajeEntrante(Numero, "Dos ideas", "wamid.p24.1", Epoca),
            CancellationToken.None);
        await orquestador.ProcesarMensajeEntranteAsync(
            ParticipanteConCoaching(), Mensaje("Vamos a mejorarla", "wamid.p24.2"), CancellationToken.None);

        contextos.Should().ContainSingle();
        contextos.Single().RespuestaTexto.Should().Be("Primera idea suficientemente larga para ser procesada.");
        almacen.Respuestas.Should().HaveCount(2).And.NotContain(respuesta => respuesta.Texto == "Vamos a mejorarla");
        almacen.Versiones.Should().HaveCount(2);
        var ideas = almacen.Ideas.Values.OrderBy(idea => idea.IdeaIndice).ToArray();
        ideas[0].VersionConfirmadaRef.Should().NotBeNull();
        ideas[1].EstadoFlujo.Should().Be(EstadoFlujoIdeaConsolidada.PendienteConfirmacion);

        var cola = _conversaciones.Ultima!.CoachingIdeas!;
        cola.IdeaActivaIndice.Should().Be(1);
        cola.IdeaActiva!.RepreguntasUsadas.Should().Be(1);
        await _gateway.Received(1).EnviarTextoAsync(
            Numero,
            Arg.Is<string>(texto => texto.Contains("¿Qué resultado esperas?", StringComparison.Ordinal)),
            TipoEnvioMensaje.Repregunta,
            Arg.Any<CancellationToken>(),
            Arg.Any<string?>());
    }

    [Fact]
    public async Task I19_ColaMultiIdea_RechazoExplicito_CierraSoloEsaIdeaYSigueConLaSiguiente()
    {
        var almacen = ConfigurarAlmacenIdeas();
        SegmentarEnDosIdeas();
        await PrepararConversacionAsync();
        var orquestador = Construir(consolidador: ConsolidadorQueAcumula());

        await orquestador.ProcesarMensajeEntranteAsync(
            ParticipanteConCoaching(),
            new MensajeEntrante(Numero, "Dos ideas", "wamid.i19", Epoca),
            CancellationToken.None);
        await orquestador.ProcesarMensajeEntranteAsync(
            ParticipanteConCoaching(), Mensaje("no lo guardes"), CancellationToken.None);

        await _evaluador.DidNotReceive().EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>());
        almacen.Ideas["idea_resp_wamid_i19_1"].EstadoResultado.Should().Be(EstadoResultadoIdeaConsolidada.Rechazada);
        almacen.Ideas["idea_resp_wamid_i19_1"].EstadoCuraduria.Should().BeNull();
        almacen.Ideas["idea_resp_wamid_i19_2"].EstadoFlujo.Should().Be(EstadoFlujoIdeaConsolidada.PendienteConfirmacion);
        var cola = _conversaciones.Ultima!.CoachingIdeas!;
        cola.Ideas[0].MotivoFinalizacion.Should().Be(MotivoFinalizacionIdea.Rechazo);
        cola.IdeaActivaIndice.Should().Be(2);
        await _gateway.Received(1).EnviarTextoAsync(
            Numero,
            Arg.Is<string>(texto => texto.Contains("Segunda idea", StringComparison.Ordinal)),
            TipoEnvioMensaje.Repregunta,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task I19_ColaMultiIdea_ComplementoConIdeaNueva_EncolaLaNuevaSinMezclarla()
    {
        const string nueva = "Ademas propongo un tablero semanal de seguimiento por equipo.";
        var almacen = ConfigurarAlmacenIdeas();
        SegmentarEnDosIdeas();
        await PrepararConversacionAsync();
        var orquestador = Construir(consolidador: ConsolidadorQueAcumula(nueva));

        await orquestador.ProcesarMensajeEntranteAsync(
            ParticipanteConCoaching(),
            new MensajeEntrante(Numero, "Dos ideas", "wamid.i19", Epoca),
            CancellationToken.None);
        await orquestador.ProcesarMensajeEntranteAsync(
            ParticipanteConCoaching(),
            Mensaje("En realidad seria con el equipo de soporte"),
            CancellationToken.None);

        await _evaluador.DidNotReceive().EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>());
        var cola = _conversaciones.Ultima!.CoachingIdeas!;
        cola.Ideas.Should().HaveCount(3);
        cola.IdeaActivaIndice.Should().Be(1);
        cola.Ideas[2].Should().BeEquivalentTo(new
        {
            IdeaIndice = 3,
            Estado = EstadoIdeaCoaching.Pendiente,
            RepreguntasUsadas = 0,
        });
        cola.Ideas[2].IdeaId.Should().NotBeNull();
        cola.Ideas[2].VersionIdeaVigenteId.Should().NotBeNull();
        almacen.Respuestas.Should().ContainSingle(respuesta => respuesta.TipoAporte == TipoAporteIdea.NuevaIdea)
            .Which.Texto.Should().Be(nueva);
        almacen.Versiones[cola.Ideas[2].VersionIdeaVigenteId!].Texto.Should().Be(nueva);
        // La idea activa conserva su propio contenido: los textos no se mezclan (§4.6.3).
        almacen.Versiones[cola.IdeaActiva!.VersionIdeaVigenteId!].Texto.Should().NotContain(nueva);
        await _gateway.Received(1).EnviarTextoAsync(
            Numero,
            Arg.Is<string>(texto => texto.Contains("equipo de soporte", StringComparison.Ordinal)
                && !texto.Contains(nueva, StringComparison.Ordinal)),
            TipoEnvioMensaje.Repregunta,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task I19_ColaMultiIdea_TopeDeIdeasAlcanzado_NoEncolaLaIdeaNueva()
    {
        const string nueva = "Ademas propongo un tablero semanal de seguimiento por equipo.";
        var almacen = ConfigurarAlmacenIdeas();
        SegmentarEnDosIdeas();
        await PrepararConversacionAsync();
        var orquestador = Construir(
            new OpcionesConversacion { MaxIdeasPorMensaje = 2 },
            ConsolidadorQueAcumula(nueva));

        await orquestador.ProcesarMensajeEntranteAsync(
            ParticipanteConCoaching(),
            new MensajeEntrante(Numero, "Dos ideas", "wamid.i19", Epoca),
            CancellationToken.None);
        await orquestador.ProcesarMensajeEntranteAsync(
            ParticipanteConCoaching(),
            Mensaje("En realidad seria con el equipo de soporte"),
            CancellationToken.None);

        _conversaciones.Ultima!.CoachingIdeas!.Ideas.Should().HaveCount(2);
        almacen.Respuestas.Should().NotContain(respuesta => respuesta.TipoAporte == TipoAporteIdea.NuevaIdea);
    }

    [Fact]
    public async Task I19_IdeaNuevaSinCola_EsperaTurnoYSeAtiendeAlCerrarLaActiva()
    {
        const string nueva = "Ademas propongo un tablero semanal de seguimiento por equipo.";
        var almacen = ConfigurarAlmacenIdeas();
        _evaluador.EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Exito(CrearEvaluacion(RecomendacionEvaluacion.Cerrar, null, calificacionTotal: 4m)));
        await PrepararConversacionAsync();
        var orquestador = Construir(consolidador: ConsolidadorQueAcumula(nueva));

        await orquestador.ProcesarMensajeEntranteAsync(Participante(), Mensaje("Aporte inicial"), CancellationToken.None);
        await orquestador.ProcesarMensajeEntranteAsync(
            Participante(), Mensaje("En realidad seria con soporte"), CancellationToken.None);
        await orquestador.ProcesarMensajeEntranteAsync(Participante(), Mensaje("si"), CancellationToken.None);

        var ideas = almacen.Ideas.Values.OrderBy(idea => idea.IdeaIndice).ToArray();
        ideas.Should().HaveCount(2);
        ideas[0].EstadoResultado.Should().Be(EstadoResultadoIdeaConsolidada.Madura);
        ideas[1].IdeaIndice.Should().Be(2);
        ideas[1].EstadoFlujo.Should().Be(EstadoFlujoIdeaConsolidada.PendienteConfirmacion);
        _conversaciones.Ultima!.Estado.Should().Be(EstadoConversacion.Abierta);
        await _gateway.Received(1).EnviarTextoAsync(
            Numero,
            Arg.Is<string>(texto => texto.Contains(nueva, StringComparison.Ordinal)
                && texto.Contains("¿Es correcto?", StringComparison.Ordinal)),
            TipoEnvioMensaje.Repregunta,
            Arg.Any<CancellationToken>());
        await _gateway.DidNotReceive().EnviarTextoAsync(
            Numero, Arg.Any<string>(), TipoEnvioMensaje.Cierre, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task I19_SegmentacionSinCoaching_ConfirmaCadaIdeaYNoRepregunta()
    {
        var almacen = ConfigurarAlmacenIdeas();
        var contextos = new List<ContextoEvaluacion>();
        _evaluador.EvaluarAsync(Arg.Do<ContextoEvaluacion>(contexto => contextos.Add(contexto)), Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Exito(
                CrearEvaluacion(RecomendacionEvaluacion.Repreguntar, "¿Y el costo?", calificacionTotal: 1m)));
        SegmentarEnDosIdeas();
        await PrepararConversacionAsync();
        var orquestador = Construir(consolidador: ConsolidadorQueAcumula());

        // Campaña con I-06 activo pero el acompañamiento I-18 apagado.
        await orquestador.ProcesarMensajeEntranteAsync(
            ParticipanteConSegmentacion(),
            new MensajeEntrante(Numero, "Dos ideas", "wamid.sincoach", Epoca),
            CancellationToken.None);

        // Ninguna raíz se evalúa: primero se pide confirmar la idea activa (I-19 §8.1).
        contextos.Should().BeEmpty();
        almacen.Ideas.Should().HaveCount(2);
        await _gateway.Received(1).EnviarTextoAsync(
            Numero,
            Arg.Is<string>(texto => texto.Contains("¿Es correcto?", StringComparison.Ordinal)),
            TipoEnvioMensaje.Repregunta,
            Arg.Any<CancellationToken>());

        await orquestador.ProcesarMensajeEntranteAsync(
            ParticipanteConSegmentacion(), Mensaje("si"), CancellationToken.None);

        // Se evalúa la versión confirmada y, sin coaching, la idea cierra sin pregunta socrática.
        contextos.Should().ContainSingle().Which.CoachingSecuencialIdeas.Should().BeFalse();
        almacen.Ideas.Values.Should().ContainSingle(idea =>
            idea.EstadoFlujo == EstadoFlujoIdeaConsolidada.Cerrada
            && idea.EstadoResultado == EstadoResultadoIdeaConsolidada.Pendiente);
        await _gateway.DidNotReceive().EnviarTextoAsync(
            Numero,
            Arg.Is<string>(texto => texto.Contains("¿Y el costo?", StringComparison.Ordinal)),
            Arg.Any<TipoEnvioMensaje>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task I20_Confirmar_UsaLaVozRedactadaEInsertaLaVersionIntegra()
    {
        ConfigurarAlmacenIdeas();
        var redactor = RedactorQueDevuelve("Recojo lo que me contaste.", "¿Va bien así?");
        await PrepararConversacionAsync();

        await Construir(consolidador: ConsolidadorQueAcumula(), redactor: redactor)
            .ProcesarMensajeEntranteAsync(Participante(), Mensaje("Aporte inicial"), CancellationToken.None);

        await _gateway.Received(1).EnviarTextoAsync(
            Numero,
            Arg.Is<string>(texto =>
                texto.Contains("Recojo lo que me contaste.", StringComparison.Ordinal)
                // §4: la versión propuesta la inserta el servidor, íntegra y entre las dos piezas.
                && texto.Contains("Aporte inicial", StringComparison.Ordinal)
                && texto.Contains("¿Va bien así?", StringComparison.Ordinal)
                // Ya no aparece la frase fija que originó I-20.
                && !texto.Contains("Entendí que propones", StringComparison.Ordinal)),
            TipoEnvioMensaje.Repregunta,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task I20_RedactorEnFallback_ConservaExactamenteElTextoDeHoy()
    {
        ConfigurarAlmacenIdeas();
        var redactor = Substitute.For<IRedactorTurnoConversacional>();
        redactor.RedactarAsync(Arg.Any<ContextoRedaccionTurno>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoRedaccionTurno.Fallback("salida_invalida:contrato", null));
        await PrepararConversacionAsync();

        await Construir(consolidador: ConsolidadorQueAcumula(), redactor: redactor)
            .ProcesarMensajeEntranteAsync(Participante(), Mensaje("Aporte inicial"), CancellationToken.None);

        await _gateway.Received(1).EnviarTextoAsync(
            Numero,
            "Entendí que propones: Aporte inicial\n\n¿Es correcto?",
            TipoEnvioMensaje.Repregunta,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task I20_KillSwitchApagado_NoLlamaAlRedactorNiCambiaElTexto()
    {
        ConfigurarAlmacenIdeas();
        var redactor = RedactorQueDevuelve("No debería usarse.", "¿Ni esto?");
        await PrepararConversacionAsync();

        await Construir(
                new OpcionesConversacion { RedaccionConversacionalFluidaHabilitada = false },
                ConsolidadorQueAcumula(),
                redactor)
            .ProcesarMensajeEntranteAsync(Participante(), Mensaje("Aporte inicial"), CancellationToken.None);

        await redactor.DidNotReceiveWithAnyArgs().RedactarAsync(default!, default);
        await _gateway.Received(1).EnviarTextoAsync(
            Numero,
            "Entendí que propones: Aporte inicial\n\n¿Es correcto?",
            TipoEnvioMensaje.Repregunta,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task I20_Redaccion_RegistraTelemetriaSinElTextoRedactado()
    {
        ConfigurarAlmacenIdeas();
        var logs = new List<LogSeguridad>();
        _logSeguridad.RegistrarAsync(Arg.Do<LogSeguridad>(log => logs.Add(log)), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var redactor = RedactorQueDevuelve("Puente redactado.", "¿Confirmas?");
        await PrepararConversacionAsync();

        await Construir(consolidador: ConsolidadorQueAcumula(), redactor: redactor)
            .ProcesarMensajeEntranteAsync(Participante(), Mensaje("Aporte inicial"), CancellationToken.None);

        var evento = logs.Single(log => log.TipoEvento == TipoEventoSeguridad.RedaccionConversacional);
        evento.Resultado.Should().Be("redactado");
        evento.Detalle.Should().Contain("accion:confirmar").And.Contain("promptTokens:11");
        // 10 §6.2: nunca viaja el texto redactado ni el aporte del participante.
        evento.Detalle.Should().NotContain("Puente redactado").And.NotContain("Aporte inicial");
    }

    [Fact]
    public async Task I20_CupoLlmAgotado_NoLlamaAlRedactorYUsaElRespaldo()
    {
        ConfigurarAlmacenIdeas();
        _respuestas.ContarEvaluacionesUsuarioAsync("c_1", "u_1", Arg.Any<CancellationToken>()).Returns(5);
        var redactor = RedactorQueDevuelve("No debería usarse.", "¿Ni esto?");
        await PrepararConversacionAsync();

        await Construir(
                new OpcionesConversacion { CuposHabilitados = true },
                ConsolidadorQueAcumula(),
                redactor)
            .ProcesarMensajeEntranteAsync(
                ParticipanteConCupos(maxMensajesPorUsuario: 10, maxLlamadasLlm: 2),
                Mensaje("Aporte inicial"),
                CancellationToken.None);

        // §4.1: con el cupo agotado no se gasta LLM en redactar; el turno sale con su respaldo.
        await redactor.DidNotReceiveWithAnyArgs().RedactarAsync(default!, default);
    }

    [Fact]
    public async Task I19_AporteAmbiguo_PideAclaracionSinCrearVersionNiEvaluar()
    {
        var almacen = ConfigurarAlmacenIdeas();
        var consolidador = Substitute.For<IConsolidadorIdeas>();
        consolidador.ConsolidarAsync(Arg.Any<ContextoConsolidacionIdeas>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoConsolidacionIdeas.Exito(
                "Texto que no se debe usar.", TipoAporteIdea.Inicial, [], true,
                "¿Te refieres al proceso de compras o al de pagos?", false, null));
        await PrepararConversacionAsync();

        await Construir(consolidador: consolidador).ProcesarMensajeEntranteAsync(
            Participante(), Mensaje("Eso"), CancellationToken.None);

        // §4.2: no se adivina; no hay versión ni evaluación, pero el aporte sí se conserva.
        almacen.Versiones.Should().BeEmpty();
        await _evaluador.DidNotReceive().EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>());
        almacen.Respuestas.Should().ContainSingle(respuesta => respuesta.Texto == "Eso");
        await _gateway.Received(1).EnviarTextoAsync(
            Numero,
            "¿Te refieres al proceso de compras o al de pagos?",
            TipoEnvioMensaje.Repregunta,
            Arg.Any<CancellationToken>());
        await _logSeguridad.Received().RegistrarAsync(
            Arg.Is<LogSeguridad>(log => log.TipoEvento == TipoEventoSeguridad.ConsolidacionProgresivaIdeas
                && log.Resultado == "aclaracion"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task I19_Telemetria_RegistraCadaTransicionSinTextoNiParafrasis()
    {
        ConfigurarAlmacenIdeas();
        var logs = new List<LogSeguridad>();
        _logSeguridad.RegistrarAsync(Arg.Do<LogSeguridad>(log => logs.Add(log)), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _evaluador.EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Exito(CrearEvaluacion(RecomendacionEvaluacion.Cerrar, null, calificacionTotal: 4m)));
        await PrepararConversacionAsync();
        var orquestador = Construir(consolidador: ConsolidadorQueAcumula());

        await orquestador.ProcesarMensajeEntranteAsync(Participante(), Mensaje("Aporte inicial"), CancellationToken.None);
        await orquestador.ProcesarMensajeEntranteAsync(Participante(), Mensaje("si"), CancellationToken.None);

        var consolidacion = logs
            .Where(log => log.TipoEvento == TipoEventoSeguridad.ConsolidacionProgresivaIdeas)
            .ToArray();
        consolidacion.Select(log => log.Resultado).Should()
            .Equal("propuesta", "confirmada", "evaluada", "cerrada");
        consolidacion.Should().OnlyContain(log => log.Detalle!.Contains("ideaIndice:1", StringComparison.Ordinal));
        consolidacion[3].Detalle.Should().Contain("motivo:umbral").And.Contain("resultado:madura");
        // §12.1: la telemetría nunca lleva el aporte ni la paráfrasis.
        consolidacion.Should().OnlyContain(log =>
            !log.Detalle!.Contains("Aporte inicial", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task I19_TelemetriaFallback_DistingueLaConsolidacionDegradada()
    {
        ConfigurarAlmacenIdeas();
        var logs = new List<LogSeguridad>();
        _logSeguridad.RegistrarAsync(Arg.Do<LogSeguridad>(log => logs.Add(log)), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var consolidador = Substitute.For<IConsolidadorIdeas>();
        consolidador.ConsolidarAsync(Arg.Any<ContextoConsolidacionIdeas>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoConsolidacionIdeas.Fallback("Aporte inicial", "error_proveedor", UsoTokensLlm.Crear(12, 5)));
        await PrepararConversacionAsync();

        await Construir(consolidador: consolidador).ProcesarMensajeEntranteAsync(
            Participante(), Mensaje("Aporte inicial"), CancellationToken.None);

        var evento = logs.Single(log => log.TipoEvento == TipoEventoSeguridad.ConsolidacionProgresivaIdeas);
        evento.Resultado.Should().Be("fallback");
        evento.Detalle.Should().Contain("motivo:consolidacionFallback")
            .And.Contain("promptTokens:12")
            .And.Contain("completionTokens:5");
    }

    [Fact]
    public async Task I19_CupoLlamadasLlm_CuentaTambienLasConsolidaciones()
    {
        var almacen = ConfigurarAlmacenIdeas();
        _respuestas.ContarEvaluacionesUsuarioAsync("c_1", "u_1", Arg.Any<CancellationToken>()).Returns(0);
        // Dos consolidaciones previas ya agotan el cupo aunque no haya ninguna evaluación (I-19 §12.3).
        _respuestas.ContarConsolidacionesUsuarioAsync("c_1", "u_1", Arg.Any<CancellationToken>()).Returns(2);
        await PrepararConversacionAsync();

        await Construir(new OpcionesConversacion { CuposHabilitados = true }, ConsolidadorQueAcumula())
            .ProcesarMensajeEntranteAsync(
                ParticipanteConCupos(maxMensajesPorUsuario: 10, maxLlamadasLlm: 2),
                Mensaje("Aporte inicial"),
                CancellationToken.None);

        almacen.Versiones.Should().BeEmpty();
        await _evaluador.DidNotReceive().EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>());
        await _logSeguridad.Received().RegistrarAsync(
            Arg.Is<LogSeguridad>(log => log.TipoEvento == TipoEventoSeguridad.RateLimit
                && log.Detalle == "cupo_llamadas_llm_usuario"),
            Arg.Any<CancellationToken>());
        // §12.3: agotar el cupo no puede perder el aporte del participante.
        almacen.Respuestas.Should().ContainSingle(respuesta => respuesta.Texto == "Aporte inicial");
        _conversaciones.Ultima!.Estado.Should().Be(EstadoConversacion.Cerrada);
    }

    [Fact]
    public async Task I19_TopeDeTurnosDelHilo_ConservaElAporteYCierraLaIdeaComoPendiente()
    {
        var almacen = ConfigurarAlmacenIdeas();
        _evaluador.EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Exito(CrearEvaluacion(RecomendacionEvaluacion.Repreguntar, "¿Y el costo?", calificacionTotal: 1m)));
        await PrepararConversacionAsync();
        // El tope de turnos del hilo se mide sobre los mensajes ya persistidos.
        for (var turno = 0; turno < 3; turno++)
        {
            await SembrarEntranteAsync($"turno {turno}");
        }

        await Construir(new OpcionesConversacion { MaxTurnosPorHilo = 2 }, ConsolidadorQueAcumula())
            .ProcesarMensajeEntranteAsync(Participante(), Mensaje("Aporte que llega tarde"), CancellationToken.None);

        almacen.Versiones.Should().BeEmpty();
        await _evaluador.DidNotReceive().EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>());
        almacen.Respuestas.Should().ContainSingle(respuesta => respuesta.Texto == "Aporte que llega tarde");
        await _logSeguridad.Received().RegistrarAsync(
            Arg.Is<LogSeguridad>(log => log.TipoEvento == TipoEventoSeguridad.RateLimit
                && log.Detalle == "tope_turnos_hilo"),
            Arg.Any<CancellationToken>());
        _conversaciones.Ultima!.Estado.Should().Be(EstadoConversacion.Cerrada);
    }

    [Fact]
    public async Task I19_LaAnterior_ReabreLaIdeaCerradaMasRecienteConservandoSuHistorial()
    {
        const string nueva = "Ademas propongo un tablero semanal de seguimiento por equipo.";
        var almacen = ConfigurarAlmacenIdeas();
        _evaluador.EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Exito(CrearEvaluacion(RecomendacionEvaluacion.Cerrar, null, calificacionTotal: 4m)));
        await PrepararConversacionAsync();
        var orquestador = Construir(consolidador: ConsolidadorQueAcumula(nueva));

        await orquestador.ProcesarMensajeEntranteAsync(Participante(), Mensaje("Aporte inicial"), CancellationToken.None);
        await orquestador.ProcesarMensajeEntranteAsync(
            Participante(), Mensaje("En realidad seria con soporte"), CancellationToken.None);
        await orquestador.ProcesarMensajeEntranteAsync(Participante(), Mensaje("si"), CancellationToken.None);
        var versionesAntes = almacen.Versiones.Count;
        var confirmadaAntes = almacen.Ideas.Values.Single(idea => idea.IdeaIndice == 1).VersionConfirmadaRef;

        await orquestador.ProcesarMensajeEntranteAsync(
            Participante(), Mensaje("quiero volver a la anterior"), CancellationToken.None);

        var reabierta = almacen.Ideas.Values.Single(idea => idea.IdeaIndice == 1);
        reabierta.EstadoFlujo.Should().Be(EstadoFlujoIdeaConsolidada.EnRevision);
        reabierta.EstadoResultado.Should().BeNull();
        reabierta.EstadoCuraduria.Should().BeNull();
        reabierta.NivelMadurez.Should().Be(NivelMadurez.Incubacion);
        // El historial no se sobrescribe: misma idea, misma versión oficial y ninguna versión nueva.
        reabierta.VersionConfirmadaRef.Should().Be(confirmadaAntes);
        almacen.Versiones.Should().HaveCount(versionesAntes);
        _conversaciones.Ultima!.Estado.Should().Be(EstadoConversacion.Abierta);
        await _gateway.Received(1).EnviarTextoAsync(
            Numero,
            Arg.Is<string>(texto => texto.Contains("volvamos a esa idea", StringComparison.OrdinalIgnoreCase)
                && texto.Contains("Aporte inicial", StringComparison.Ordinal)
                && texto.Contains("¿Qué quieres cambiar o agregar?", StringComparison.Ordinal)),
            TipoEnvioMensaje.Repregunta,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task I19_CampaniaCerrada_NoReabreNingunaIdea()
    {
        var almacen = ConfigurarAlmacenIdeas();
        _evaluador.EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Exito(CrearEvaluacion(RecomendacionEvaluacion.Cerrar, null, calificacionTotal: 4m)));
        await PrepararConversacionAsync();
        var orquestador = Construir(consolidador: ConsolidadorQueAcumula());

        await orquestador.ProcesarMensajeEntranteAsync(Participante(), Mensaje("Aporte inicial"), CancellationToken.None);
        await orquestador.ProcesarMensajeEntranteAsync(Participante(), Mensaje("si"), CancellationToken.None);
        await orquestador.ProcesarMensajeEntranteAsync(
            Participante(estadoCampania: EstadoCampania.Cerrada),
            Mensaje("quiero volver a la anterior"),
            CancellationToken.None);

        almacen.Ideas.Values.Should().OnlyContain(idea => idea.EstadoFlujo == EstadoFlujoIdeaConsolidada.Cerrada);
    }

    [Fact]
    public async Task I19_VariasCandidatas_OfreceListaNumeradaYReabreLaElegida()
    {
        var almacen = ConfigurarAlmacenIdeas();
        await PrepararColaConIdeasCerradasAsync(almacen);
        var orquestador = Construir(consolidador: ConsolidadorQueAcumula());

        await orquestador.ProcesarMensajeEntranteAsync(
            ParticipanteConCoaching(), Mensaje("quiero retomar una idea"), CancellationToken.None);

        _conversaciones.Ultima!.EstadoMaquina.Should().Be(EstadoMaquinaConversacion.EsperandoSeleccionIdea);
        await _gateway.Received(1).EnviarTextoAsync(
            Numero,
            Arg.Is<string>(texto => texto.Contains("1. Idea dos consolidada", StringComparison.Ordinal)
                && texto.Contains("2. Idea uno consolidada", StringComparison.Ordinal)
                && !texto.Contains("4", StringComparison.Ordinal)),
            TipoEnvioMensaje.Repregunta,
            Arg.Any<CancellationToken>());

        await orquestador.ProcesarMensajeEntranteAsync(
            ParticipanteConCoaching(), Mensaje("2"), CancellationToken.None);

        almacen.Ideas["idea_1"].EstadoFlujo.Should().Be(EstadoFlujoIdeaConsolidada.EnRevision);
        almacen.Ideas["idea_2"].EstadoFlujo.Should().Be(EstadoFlujoIdeaConsolidada.Cerrada);
        var cola = _conversaciones.Ultima!.CoachingIdeas!;
        cola.IdeaActivaIndice.Should().Be(1);
        cola.Ideas[0].Estado.Should().Be(EstadoIdeaCoaching.Activa);
        cola.Ideas[0].MotivoFinalizacion.Should().BeNull();
        // La idea que estaba activa se conserva en la cola, no se pierde ni se cierra.
        cola.Ideas[2].Estado.Should().Be(EstadoIdeaCoaching.Pendiente);
        _conversaciones.Ultima.EstadoMaquina.Should().Be(EstadoMaquinaConversacion.EsperandoRepregunta);
    }

    [Fact]
    public async Task I19_SeleccionInvalida_CancelaLaListaYSigueElTurnoNormal()
    {
        var almacen = ConfigurarAlmacenIdeas();
        await PrepararColaConIdeasCerradasAsync(almacen);
        var orquestador = Construir(consolidador: ConsolidadorQueAcumula());

        await orquestador.ProcesarMensajeEntranteAsync(
            ParticipanteConCoaching(), Mensaje("quiero retomar una idea"), CancellationToken.None);
        await orquestador.ProcesarMensajeEntranteAsync(
            ParticipanteConCoaching(),
            Mensaje("agrego el detalle de costos por trimestre y el responsable"),
            CancellationToken.None);

        almacen.Ideas["idea_1"].EstadoFlujo.Should().Be(EstadoFlujoIdeaConsolidada.Cerrada);
        almacen.Ideas["idea_2"].EstadoFlujo.Should().Be(EstadoFlujoIdeaConsolidada.Cerrada);
        _conversaciones.Ultima!.EstadoMaquina.Should().NotBe(EstadoMaquinaConversacion.EsperandoSeleccionIdea);
        // El mensaje no se pierde: alimenta la idea activa como un aporte más.
        almacen.Respuestas.Should().Contain(respuesta =>
            respuesta.Texto == "agrego el detalle de costos por trimestre y el responsable");
        almacen.Versiones.Values.Count(version => version.IdeaId == "idea_3").Should().Be(2);
    }

    [Fact]
    public async Task I19_ColaReanudada_UsaLasReferenciasCanonicasPersistidas()
    {
        var almacen = ConfigurarAlmacenIdeas();
        var idea = IdeaConsolidada.Crear("idea_1", "c_1", "u_1", "p_1", "conv_c_1_u_1_p_1", "resp_1", 1, Epoca);
        var version = VersionIdeaConsolidada.Crear(
            "idea_1_v1", "c_1", "idea_1", 1, null, "Version consolidada persistida.", new[] { "resp_1" },
            new[] { "resp_1" }, TipoAporteIdea.Inicial, EstadoConfirmacionVersionIdea.Propuesta, null, null, null,
            null, Epoca);
        almacen.Ideas[idea.Id] = idea.ConPropuesta(version.Id, Epoca);
        almacen.Versiones[version.Id] = version;
        var politica = new PoliticaColaCoachingIdeas();
        var cola = politica.Crear(
            "wamid.raiz",
            new[] { new RaizIdeaCoaching(1, "resp_1", null, idea.Id, version.Id) },
            Epoca);
        await _conversaciones.GuardarConversacionAsync(
            DominioConversacion.Iniciar("conv_c_1_u_1_p_1", "c_1", "u_1", "p_1", "whatsapp", null, Epoca)
                .ConCoachingIdeas(cola)
                .AvanzarA(EstadoMaquinaConversacion.EsperandoRepregunta),
            CancellationToken.None);
        _respuestas.ObtenerRespuestaAsync("c_1", "resp_1", Arg.Any<CancellationToken>()).Returns(
            Respuesta.Crear(
                "resp_1", "c_1", "u_1", "p_1", "conv_c_1_u_1_p_1", "Aporte inicial", "whatsapp", false,
                EstadoRespuesta.Recibida, Epoca, null, ideaIndice: 1, respuestaPadreId: "wamid.raiz",
                ideaRaizId: "resp_1", revisionIndice: 0, ideaId: idea.Id, tipoAporte: TipoAporteIdea.Inicial));
        ContextoEvaluacion? contextoEvaluado = null;
        _evaluador.EvaluarAsync(Arg.Do<ContextoEvaluacion>(contexto => contextoEvaluado = contexto), Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Exito(CrearEvaluacion(RecomendacionEvaluacion.Cerrar, null, calificacionTotal: 4m)));

        await Construir(consolidador: ConsolidadorQueAcumula()).ProcesarMensajeEntranteAsync(
            ParticipanteConCoaching(), Mensaje("si"), CancellationToken.None);

        contextoEvaluado.Should().NotBeNull();
        contextoEvaluado!.RespuestaTexto.Should().Be("Version consolidada persistida.");
        contextoEvaluado.VersionIdeaId.Should().Be(version.Id);
        almacen.Versiones[version.Id].EstadoConfirmacion.Should().Be(EstadoConfirmacionVersionIdea.Confirmada);
        almacen.Ideas[idea.Id].EstadoResultado.Should().Be(EstadoResultadoIdeaConsolidada.Madura);
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
    public async Task Procesar_MensajeConNumeroDestino_RespondePorEseMismoNumero()
    {
        _evaluador.EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Exito(CrearEvaluacion(RecomendacionEvaluacion.Cerrar, null)));
        await PrepararConversacionAsync();

        await Construir().ProcesarMensajeEntranteAsync(Participante(), Mensaje("Mi idea", "meta-qas"), CancellationToken.None);

        await _gateway.Received(1).EnviarTextoAsync(
            Numero,
            Arg.Any<string>(),
            TipoEnvioMensaje.Repregunta,
            Arg.Any<CancellationToken>(),
            "meta-qas");
    }

    [Fact]
    public async Task Procesar_ParafraseoActivo_AnteponeElResumenALaRetroalimentacion()
    {
        _evaluador.EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Exito(
                CrearEvaluacion(RecomendacionEvaluacion.Cerrar, null, parafraseo: "Entendi que propones reducir desperdicio.")));
        await PrepararConversacionAsync();

        await Construir().ProcesarMensajeEntranteAsync(ParticipanteConParafraseo(), Mensaje("Mi idea"), CancellationToken.None);

        await _gateway.Received(1).EnviarTextoAsync(
            Numero,
            Arg.Is<string>(texto => texto.StartsWith("Entendi que propones reducir desperdicio.\n\nBuena idea", StringComparison.Ordinal)),
            TipoEnvioMensaje.Repregunta,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Procesar_RechazoExplicitoDeIdeaMadura_LaDegradaAIncubacionYCierra()
    {
        // I-17 §5.4: escala 1..5, umbral base 0.6 (corte 3.4); score 4 -> maduro. Tras la parafrasis
        // el participante dice "no es eso": la idea madura se degrada a incubacion y el hilo cierra.
        var guardadas = new List<Respuesta>();
        _respuestas.GuardarRespuestaAsync(
            Arg.Do<Respuesta>(r =>
            {
                guardadas.RemoveAll(x => x.Id == r.Id);
                guardadas.Add(r);
            }),
            Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _respuestas.ListarRespuestasAsync("c_1", Arg.Any<CancellationToken>()).Returns(_ => guardadas.ToArray());
        _evaluador.EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Exito(CrearEvaluacion(RecomendacionEvaluacion.Repreguntar, "Profundiza mas", calificacionTotal: 4m)));
        await PrepararConversacionAsync();
        var participante = Participante();

        await Construir().ProcesarMensajeEntranteAsync(participante, Mensaje("Mi idea madura"), CancellationToken.None);
        guardadas.Should().ContainSingle(r => r.NivelMadurez == NivelMadurez.Maduro);

        await Construir().ProcesarMensajeEntranteAsync(participante, Mensaje("no es eso"), CancellationToken.None);

        // La idea madura fue degradada; el "no es eso" no se evalua (una sola llamada al evaluador).
        guardadas.Should().NotContain(r => r.NivelMadurez == NivelMadurez.Maduro);
        await _evaluador.Received(1).EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>());
        await _gateway.Received(1).EnviarTextoAsync(
            Numero,
            Arg.Is<string>(t => t.Contains("no la guardo como definitiva", StringComparison.Ordinal)),
            TipoEnvioMensaje.Cierre,
            Arg.Any<CancellationToken>());
        await _logSeguridad.Received(1).RegistrarAsync(
            Arg.Is<LogSeguridad>(l => l.TipoEvento == TipoEventoSeguridad.ClasificacionMadurez
                && l.Detalle!.Contains("motivo:rechazo_guardado", StringComparison.Ordinal)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Procesar_RechazoSinIdeaMaduraPrevia_NoCierraYSeEvaluaComoSiempre()
    {
        // I-17 §5.4: si no hay ninguna idea madura que rechazar, un "no" cae al flujo normal (se evalua),
        // para no cortar al participante por una negacion sin contexto de guardado.
        _respuestas.ListarRespuestasAsync("c_1", Arg.Any<CancellationToken>()).Returns(Array.Empty<Respuesta>());
        _evaluador.EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Exito(CrearEvaluacion(RecomendacionEvaluacion.Cerrar, null, calificacionTotal: 2m)));
        await PrepararConversacionAsync();
        var conversacionEnRepregunta = DominioConversacion.Iniciar("conv_c_1_u_1_p_1", "c_1", "u_1", "p_1", "whatsapp", null, Epoca)
            .RegistrarEntrante(Epoca).AvanzarA(EstadoMaquinaConversacion.EsperandoRepregunta);
        await _conversaciones.GuardarConversacionAsync(conversacionEnRepregunta, CancellationToken.None);

        await Construir().ProcesarMensajeEntranteAsync(Participante(), Mensaje("no"), CancellationToken.None);

        await _evaluador.Received(1).EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Procesar_CalificacionSuperaUmbralBase_SellaRespuestaComoMadura()
    {
        // I-17: escala 1..5, umbral base global 0.6 -> corte 3.4; calificacion 4 lo supera -> maduro.
        _evaluador.EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Exito(CrearEvaluacion(RecomendacionEvaluacion.Cerrar, null, calificacionTotal: 4m)));
        await PrepararConversacionAsync();

        await Construir().ProcesarMensajeEntranteAsync(Participante(), Mensaje("Mi idea"), CancellationToken.None);

        await _respuestas.Received(1).GuardarRespuestaAsync(
            Arg.Is<Respuesta>(r => r.NivelMadurez == NivelMadurez.Maduro), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Procesar_CalificacionBajoUmbralBase_SellaRespuestaComoIncubacion()
    {
        // I-17: calificacion 2 < corte 3.4 -> incubacion.
        _evaluador.EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Exito(CrearEvaluacion(RecomendacionEvaluacion.Cerrar, null, calificacionTotal: 2m)));
        await PrepararConversacionAsync();

        await Construir().ProcesarMensajeEntranteAsync(Participante(), Mensaje("Mi idea"), CancellationToken.None);

        await _respuestas.Received(1).GuardarRespuestaAsync(
            Arg.Is<Respuesta>(r => r.NivelMadurez == NivelMadurez.Incubacion), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Procesar_Fallback_SellaRespuestaComoIncubacion()
    {
        // I-17: un fallback (evaluacion no confiable) nunca es maduro, aunque el score fuera alto.
        _evaluador.EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Fallback(
                CrearEvaluacion(RecomendacionEvaluacion.Cerrar, null, EvaluadorLlm.RetroNeutra, calificacionTotal: 5m), "error_proveedor"));
        await PrepararConversacionAsync();

        await Construir().ProcesarMensajeEntranteAsync(Participante(), Mensaje("Mi idea"), CancellationToken.None);

        await _respuestas.Received(1).GuardarRespuestaAsync(
            Arg.Is<Respuesta>(r => r.NivelMadurez == NivelMadurez.Incubacion), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Procesar_OverridePorPregunta_TienePrecedenciaSobreCampania()
    {
        // I-17: pregunta 0.9 (corte 4.6) gana sobre campania 0.5 (corte 3.0); score 4 -> incubacion
        // (con la campania sola habria sido maduro). El log de clasificacion marca origen:pregunta.
        _evaluador.EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Exito(CrearEvaluacion(RecomendacionEvaluacion.Cerrar, null, calificacionTotal: 4m)));
        await PrepararConversacionAsync();

        await Construir().ProcesarMensajeEntranteAsync(
            ParticipanteConUmbralPregunta(umbralPregunta: 0.9, umbralCampania: 0.5), Mensaje("Mi idea"), CancellationToken.None);

        await _respuestas.Received(1).GuardarRespuestaAsync(
            Arg.Is<Respuesta>(r => r.NivelMadurez == NivelMadurez.Incubacion), Arg.Any<CancellationToken>());
        await _logSeguridad.Received(1).RegistrarAsync(
            Arg.Is<LogSeguridad>(l => l.TipoEvento == TipoEventoSeguridad.ClasificacionMadurez
                && l.Resultado == "incubacion"
                && l.Detalle!.Contains("origen:pregunta", StringComparison.Ordinal)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Procesar_ParafraseoEnIncubacion_NoSeAntepone()
    {
        // I-17: la parafrasis solo se antepone cuando la idea es madura. Score 2 -> incubacion -> el
        // mensaje empieza por la retro, sin la parafrasis.
        _evaluador.EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Exito(
                CrearEvaluacion(RecomendacionEvaluacion.Cerrar, null, calificacionTotal: 2m, parafraseo: "Entendi que propones reducir desperdicio.")));
        await PrepararConversacionAsync();

        await Construir().ProcesarMensajeEntranteAsync(ParticipanteConParafraseo(), Mensaje("Mi idea"), CancellationToken.None);

        await _gateway.Received(1).EnviarTextoAsync(
            Numero,
            Arg.Is<string>(texto => texto.StartsWith("Buena idea", StringComparison.Ordinal)
                && !texto.Contains("Entendi que propones", StringComparison.Ordinal)),
            TipoEnvioMensaje.Repregunta,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Procesar_KillSwitchParafraseoApagado_NoSolicitaElCampoAlEvaluador()
    {
        ContextoEvaluacion? contextoVisto = null;
        _evaluador.EvaluarAsync(Arg.Do<ContextoEvaluacion>(c => contextoVisto = c), Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Exito(CrearEvaluacion(RecomendacionEvaluacion.Cerrar, null)));
        await PrepararConversacionAsync();

        await Construir(new OpcionesConversacion { Parafraseo = false })
            .ProcesarMensajeEntranteAsync(ParticipanteConParafraseo(), Mensaje("Mi idea"), CancellationToken.None);

        contextoVisto.Should().NotBeNull();
        contextoVisto!.SolicitarParafraseo.Should().BeFalse();
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
        // no se ofrece mejora: se felicita y cierra. I-17: el cierre anticipado exige el kill-switch en
        // true (default global ahora false para no encenderlo con el umbral base 0.6).
        _evaluador.EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Exito(CrearEvaluacion(RecomendacionEvaluacion.Repreguntar, "Profundiza mas", "Buena idea", calificacionTotal: 5m)));
        var orquestador = Construir(new OpcionesConversacion { UmbralCierreAnticipado = 0.85, CierreAnticipadoHabilitado = true });
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
                && l.Detalle!.Contains("origen:global", StringComparison.Ordinal)
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
    public async Task Procesar_UmbralDeCampania_ActivaCierreConKillSwitchOn()
    {
        // I-17: con el kill-switch de cierre encendido, el override por campaña (0.5) manda sobre el
        // default global (0.6): score 4 supera el corte de campaña (3.0) y dispara el cierre anticipado.
        _evaluador.EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Exito(CrearEvaluacion(RecomendacionEvaluacion.Repreguntar, "Profundiza mas", calificacionTotal: 4m)));
        var participante = ParticipanteConUmbralCierre(0.5);
        var opciones = new OpcionesConversacion { CierreAnticipadoHabilitado = true };

        await Construir(opciones).ProcesarMensajeEntranteAsync(participante, Mensaje("Hola"), CancellationToken.None);
        await Construir(opciones).ProcesarMensajeEntranteAsync(participante, Mensaje("Mi idea"), CancellationToken.None);

        await _gateway.Received(1).EnviarTextoAsync(Numero, Arg.Any<string>(), TipoEnvioMensaje.Cierre, Arg.Any<CancellationToken>());
        await _logSeguridad.Received(1).RegistrarAsync(
            Arg.Is<LogSeguridad>(l => l.TipoEvento == TipoEventoSeguridad.CierreUmbralAnticipado
                && l.Detalle!.Contains("origen:campania", StringComparison.Ordinal)
                && l.Detalle.Contains("umbral:0.5", StringComparison.Ordinal)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Procesar_UmbralDeCampaniaEnCero_ApagaElDefaultGlobalActivo()
    {
        _evaluador.EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Exito(CrearEvaluacion(RecomendacionEvaluacion.Repreguntar, "Profundiza mas", calificacionTotal: 5m)));
        var participante = ParticipanteConUmbralCierre(0);
        // I-17: kill-switch on para probar de verdad que el override 0 de la campaña apaga el cierre
        // pese al default global activo.
        var orquestador = Construir(new OpcionesConversacion { UmbralCierreAnticipado = 0.5, CierreAnticipadoHabilitado = true });

        await orquestador.ProcesarMensajeEntranteAsync(participante, Mensaje("Hola"), CancellationToken.None);
        await orquestador.ProcesarMensajeEntranteAsync(participante, Mensaje("Mi idea"), CancellationToken.None);

        await _gateway.Received(1).EnviarTextoAsync(Numero, Arg.Any<string>(), TipoEnvioMensaje.Repregunta, Arg.Any<CancellationToken>());
        await _logSeguridad.DidNotReceive().RegistrarAsync(
            Arg.Is<LogSeguridad>(l => l.TipoEvento == TipoEventoSeguridad.CierreUmbralAnticipado),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Procesar_KillSwitchGlobalApagado_AnulaOverrideDeCampania()
    {
        _evaluador.EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Exito(CrearEvaluacion(RecomendacionEvaluacion.Repreguntar, "Profundiza mas", calificacionTotal: 5m)));
        var participante = ParticipanteConUmbralCierre(0.5);
        var orquestador = Construir(new OpcionesConversacion
        {
            UmbralCierreAnticipado = 0.5,
            CierreAnticipadoHabilitado = false,
        });

        await orquestador.ProcesarMensajeEntranteAsync(participante, Mensaje("Hola"), CancellationToken.None);
        await orquestador.ProcesarMensajeEntranteAsync(participante, Mensaje("Mi idea"), CancellationToken.None);

        await _gateway.Received(1).EnviarTextoAsync(Numero, Arg.Any<string>(), TipoEnvioMensaje.Repregunta, Arg.Any<CancellationToken>());
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
    public async Task Procesar_I18Activo_IniciaSoloPrimeraIdeaSinConfirmacionAgregada()
    {
        var respuestas = new List<Respuesta>();
        var contextos = new List<ContextoEvaluacion>();
        _respuestas.GuardarRespuestaAsync(
                Arg.Do<Respuesta>(respuesta => respuestas.Add(respuesta)),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _segmentadorIdeas.SegmentarAsync(Arg.Any<ContextoSegmentacionIdeas>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoSegmentacionIdeas.Exito(
                new[]
                {
                    new IdeaSegmentada(1, "Primera idea suficientemente larga para ser procesada.", null),
                    new IdeaSegmentada(2, "Segunda idea suficientemente larga para ser procesada.", null),
                },
                UsoTokensLlm.Crear(8, 3)));
        _evaluador.EvaluarAsync(
                Arg.Do<ContextoEvaluacion>(contexto => contextos.Add(contexto)),
                Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Exito(
                CrearEvaluacion(
                    RecomendacionEvaluacion.Repreguntar,
                    "¿Que resultado concreto esperas?",
                    calificacionTotal: 1m)));
        await PrepararConversacionAsync();

        await Construir().ProcesarMensajeEntranteAsync(
            ParticipanteConCoaching(),
            new MensajeEntrante(Numero, "Dos ideas", "wamid.coaching", Epoca),
            CancellationToken.None);

        contextos.Should().HaveCount(2).And.OnlyContain(contexto => contexto.CoachingSecuencialIdeas);
        respuestas.Should().HaveCount(2);
        respuestas.Should().OnlyContain(respuesta =>
            respuesta.IdeaRaizId == respuesta.Id
            && respuesta.RespuestaAnteriorId == null
            && respuesta.RevisionIndice == 0);
        _conversaciones.Ultima!.CoachingIdeas.Should().NotBeNull();
        _conversaciones.Ultima.CoachingIdeas!.IdeaActivaIndice.Should().Be(1);
        _conversaciones.Ultima.CoachingIdeas.Ideas.Should().ContainSingle(
            idea => idea.Estado == EstadoIdeaCoaching.Activa && idea.RepreguntasUsadas == 1);
        await _gateway.Received(1).EnviarTextoAsync(
            Numero,
            Arg.Is<string>(texto =>
                texto.Contains("¿Que resultado concreto esperas?", StringComparison.Ordinal)
                && !texto.Contains("Registramos", StringComparison.OrdinalIgnoreCase)
                && !texto.Contains("asi esta bien", StringComparison.OrdinalIgnoreCase)),
            TipoEnvioMensaje.Repregunta,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Procesar_I18Revision_EnlazaEvaluaYAvanzaASiguienteSinResegmentar()
    {
        var politica = new PoliticaColaCoachingIdeas();
        var cola = politica.Crear(
            "wamid.raiz",
            new[]
            {
                new RaizIdeaCoaching(1, "resp_1", null),
                new RaizIdeaCoaching(2, "resp_2", null),
            },
            Epoca);
        cola = politica.RegistrarRepregunta(cola);
        var conversacion = DominioConversacion
            .Iniciar("conv_c_1_u_1_p_1", "c_1", "u_1", "p_1", "whatsapp", null, Epoca)
            .ConCoachingIdeas(cola)
            .AvanzarA(EstadoMaquinaConversacion.EsperandoRepregunta);
        await _conversaciones.GuardarConversacionAsync(conversacion, CancellationToken.None);

        var raiz = Respuesta.Crear(
            "resp_1",
            "c_1",
            "u_1",
            "p_1",
            conversacion.Id,
            "Idea inicial",
            "whatsapp",
            false,
            EstadoRespuesta.Evaluada,
            Epoca,
            null,
            ideaIndice: 1,
            respuestaPadreId: "wamid.raiz",
            ideaRaizId: "resp_1",
            revisionIndice: 0);
        _respuestas.ObtenerRespuestaAsync("c_1", "resp_1", Arg.Any<CancellationToken>()).Returns(raiz);
        _respuestas.ObtenerEvaluacionPorRespuestaAsync("c_1", "resp_2", Arg.Any<CancellationToken>())
            .Returns(CrearEvaluacion(
                RecomendacionEvaluacion.Repreguntar,
                "¿Que cambiaria en la segunda idea?",
                calificacionTotal: 1m));
        var guardadas = new List<Respuesta>();
        _respuestas.GuardarRespuestaAsync(
                Arg.Do<Respuesta>(respuesta => guardadas.Add(respuesta)),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _evaluador.EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Exito(
                CrearEvaluacion(RecomendacionEvaluacion.Cerrar, null, "La primera ya esta clara.", 5m)));

        await Construir().ProcesarMensajeEntranteAsync(
            ParticipanteConCoaching(),
            Mensaje("Ahora aclaro el resultado esperado"),
            CancellationToken.None);

        await _segmentadorIdeas.DidNotReceiveWithAnyArgs().SegmentarAsync(default!, default);
        guardadas.Should().ContainSingle(respuesta =>
            respuesta.IdeaRaizId == "resp_1"
            && respuesta.RespuestaAnteriorId == "resp_1"
            && respuesta.RevisionIndice == 1);
        _conversaciones.Ultima!.CoachingIdeas!.Ideas[0].MotivoFinalizacion.Should().Be(MotivoFinalizacionIdea.Umbral);
        _conversaciones.Ultima.CoachingIdeas.IdeaActivaIndice.Should().Be(2);
        _conversaciones.Ultima.CoachingIdeas.IdeaActiva!.RepreguntasUsadas.Should().Be(1);
        await _gateway.Received(1).EnviarTextoAsync(
            Numero,
            Arg.Is<string>(texto => texto.Contains("¿Que cambiaria en la segunda idea?", StringComparison.Ordinal)),
            TipoEnvioMensaje.Repregunta,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnviarTurnoCoachingPendiente_VentanaAbierta_EnviaYRegistraLaSiguienteIdea()
    {
        var contexto = ParticipanteConCoaching();
        var politica = new PoliticaColaCoachingIdeas();
        var cola = politica.Crear(
            "wamid.raiz",
            new[]
            {
                new RaizIdeaCoaching(1, "resp_1", null),
                new RaizIdeaCoaching(2, "resp_2", null),
            },
            Epoca);
        cola = politica.FinalizarActiva(cola, MotivoFinalizacionIdea.Tiempo, Epoca);
        var conversacion = DominioConversacion
            .Iniciar("conv_c_1_u_1_p_1", "c_1", "u_1", "p_1", "whatsapp", null, Epoca)
            .ConCoachingIdeas(cola);
        _participantes.ObtenerParticipantePorUsuarioAsync("c_1", "u_1", Arg.Any<CancellationToken>())
            .Returns(contexto.Participante);
        _respuestas.ObtenerEvaluacionPorRespuestaAsync("c_1", "resp_2", Arg.Any<CancellationToken>())
            .Returns(CrearEvaluacion(
                RecomendacionEvaluacion.Repreguntar,
                "Â¿Que cambiaria en la segunda idea?",
                calificacionTotal: 1m));

        await Construir().EnviarTurnoCoachingPendienteAsync(
            conversacion,
            contexto.Campania,
            CancellationToken.None);

        await _gateway.Received(1).EnviarTextoAsync(
            Numero,
            Arg.Is<string>(texto => texto.Contains("Â¿Que cambiaria en la segunda idea?", StringComparison.Ordinal)),
            TipoEnvioMensaje.Repregunta,
            Arg.Any<CancellationToken>(),
            contexto.Campania.ConfigConversacional.NumeroWhatsAppSaliente);
        _conversaciones.Ultima!.CoachingIdeas!.IdeaActiva!.RepreguntasUsadas.Should().Be(1);
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

    /// <summary>Almacén en memoria de ideas y versiones I-19 sobre el doble de <c>IRepositorioRespuestas</c>.</summary>
    private AlmacenIdeas ConfigurarAlmacenIdeas()
    {
        var almacen = new AlmacenIdeas();
        _respuestas.GuardarRespuestaAsync(Arg.Do<Respuesta>(respuesta => almacen.Respuestas.Add(respuesta)), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _respuestas.ObtenerRespuestaAsync("c_1", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(llamada => almacen.Respuestas.LastOrDefault(respuesta => respuesta.Id == llamada.ArgAt<string>(1)));
        _respuestas.GuardarIdeaConsolidadaAsync(Arg.Do<IdeaConsolidada>(idea => almacen.Ideas[idea.Id] = idea), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _respuestas.GuardarVersionIdeaAsync(Arg.Do<VersionIdeaConsolidada>(version => almacen.Versiones[version.Id] = version), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _respuestas.ObtenerIdeaConsolidadaAsync("c_1", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(llamada => almacen.Ideas.GetValueOrDefault(llamada.ArgAt<string>(1)));
        _respuestas.ObtenerVersionIdeaAsync("c_1", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(llamada => almacen.Versiones.GetValueOrDefault(llamada.ArgAt<string>(1)));
        _respuestas.ListarIdeasConsolidadasAsync("c_1", Arg.Any<CancellationToken>())
            .Returns(_ => (IReadOnlyCollection<IdeaConsolidada>)almacen.Ideas.Values.ToArray());
        _respuestas.ListarVersionesIdeaAsync("c_1", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(llamada => (IReadOnlyCollection<VersionIdeaConsolidada>)almacen.Versiones.Values
                .Where(version => version.IdeaId == llamada.ArgAt<string>(1))
                .ToArray());
        return almacen;
    }

    /// <summary>
    /// Consolidador determinista: acumula la versión vigente y el aporte nuevo, sin llamar a un LLM. Con
    /// <paramref name="nuevaIdea"/> simula que además separó una idea nueva del mismo mensaje (I-19 §4.6).
    /// </summary>
    private static IConsolidadorIdeas ConsolidadorQueAcumula(string? nuevaIdea = null)
    {
        var consolidador = Substitute.For<IConsolidadorIdeas>();
        consolidador.ConsolidarAsync(Arg.Any<ContextoConsolidacionIdeas>(), Arg.Any<CancellationToken>())
            .Returns(llamada =>
            {
                var contexto = llamada.Arg<ContextoConsolidacionIdeas>();
                var texto = string.IsNullOrWhiteSpace(contexto.TextoConfirmadoAnterior)
                    ? contexto.NuevoAporte
                    : contexto.TextoConfirmadoAnterior + " + " + contexto.NuevoAporte;
                var tipo = string.IsNullOrWhiteSpace(contexto.TextoConfirmadoAnterior)
                    ? TipoAporteIdea.Inicial
                    : TipoAporteIdea.Correccion;
                IReadOnlyList<NuevaIdeaDetectada> nuevas = nuevaIdea is null || contexto.NuevoAporte == nuevaIdea
                    ? []
                    : new[] { new NuevaIdeaDetectada(nuevaIdea) };
                return new ResultadoConsolidacionIdeas.Exito(texto, tipo, nuevas, false, null, false, null);
            });
        return consolidador;
    }

    /// <summary>
    /// Hilo con cola I-18 de tres ideas: dos ya cerradas (candidatas a reabrir, la segunda cerrada más
    /// tarde) y una activa esperando confirmación. Sirve para la desambiguación de I-19 §4.7.
    /// </summary>
    private async Task PrepararColaConIdeasCerradasAsync(AlmacenIdeas almacen)
    {
        const string conversacionId = "conv_c_1_u_1_p_1";
        var politica = new PoliticaColaCoachingIdeas();
        var raices = new List<RaizIdeaCoaching>();
        var textos = new[] { "Idea uno consolidada y confirmada.", "Idea dos consolidada y confirmada.", "Idea tres propuesta." };
        for (var indice = 1; indice <= 3; indice++)
        {
            var ideaId = $"idea_{indice}";
            var respuestaId = $"resp_{indice}";
            var version = VersionIdeaConsolidada.Crear(
                $"{ideaId}_v1", "c_1", ideaId, 1, null, textos[indice - 1], new[] { respuestaId },
                new[] { respuestaId }, TipoAporteIdea.Inicial,
                indice == 3 ? EstadoConfirmacionVersionIdea.Propuesta : EstadoConfirmacionVersionIdea.Confirmada,
                null, null, null, null, Epoca, indice == 3 ? null : Epoca);
            almacen.Versiones[version.Id] = version;
            almacen.Respuestas.Add(Respuesta.Crear(
                respuestaId, "c_1", "u_1", "p_1", conversacionId, $"Aporte {indice}", "whatsapp", false,
                EstadoRespuesta.Recibida, Epoca, null, ideaIndice: indice, respuestaPadreId: "wamid.raiz",
                ideaRaizId: respuestaId, revisionIndice: 0, ideaId: ideaId, tipoAporte: TipoAporteIdea.Inicial));

            var idea = IdeaConsolidada.Crear(ideaId, "c_1", "u_1", "p_1", conversacionId, respuestaId, indice, Epoca);
            almacen.Ideas[ideaId] = indice == 3
                ? idea.ConPropuesta(version.Id, Epoca)
                : idea.ConfirmarVersion(version.Id, Epoca)
                    .Cerrar(EstadoResultadoIdeaConsolidada.Madura, null, "umbral", Epoca.AddMinutes(indice));
            raices.Add(new RaizIdeaCoaching(indice, respuestaId, null, ideaId, version.Id));
        }

        var cola = politica.Crear("wamid.raiz", raices, Epoca);
        cola = politica.FinalizarActiva(cola, MotivoFinalizacionIdea.Umbral, Epoca.AddMinutes(1));
        cola = politica.FinalizarActiva(cola, MotivoFinalizacionIdea.Umbral, Epoca.AddMinutes(2));
        await _conversaciones.GuardarConversacionAsync(
            DominioConversacion.Iniciar(conversacionId, "c_1", "u_1", "p_1", "whatsapp", null, Epoca)
                .ConCoachingIdeas(cola)
                .AvanzarA(EstadoMaquinaConversacion.EsperandoRepregunta),
            CancellationToken.None);
    }

    private void SegmentarEnDosIdeas()
        => _segmentadorIdeas.SegmentarAsync(Arg.Any<ContextoSegmentacionIdeas>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoSegmentacionIdeas.Exito(
                new[]
                {
                    new IdeaSegmentada(1, "Primera idea suficientemente larga para ser procesada.", null),
                    new IdeaSegmentada(2, "Segunda idea suficientemente larga para ser procesada.", null),
                },
                UsoTokensLlm.Crear(8, 3)));

    private sealed class AlmacenIdeas
    {
        public List<Respuesta> Respuestas { get; } = new();

        public Dictionary<string, IdeaConsolidada> Ideas { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, VersionIdeaConsolidada> Versiones { get; } = new(StringComparer.Ordinal);
    }

    private OrquestadorConversacion Construir(
        OpcionesConversacion? opciones = null,
        IConsolidadorIdeas? consolidador = null,
        IRedactorTurnoConversacional? redactor = null)
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
            _reloj,
            consolidador,
            redactor);

    /// <summary>I-20: redactor que siempre devuelve la misma voz, para verificar la composición.</summary>
    private static IRedactorTurnoConversacional RedactorQueDevuelve(string puente, string? pregunta)
    {
        var redactor = Substitute.For<IRedactorTurnoConversacional>();
        redactor.RedactarAsync(Arg.Any<ContextoRedaccionTurno>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoRedaccionTurno.Exito(puente, pregunta, UsoTokensLlm.Crear(11, 4)));
        return redactor;
    }

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

    private static ParticipanteResuelto Participante(
        int maxRepreguntas = 1, EstadoCampania estadoCampania = EstadoCampania.Activa)
    {
        var pregunta = CrearPregunta("p_1", 1, maxRepreguntas);
        var campania = CrearCampania(new[] { pregunta }, estado: estadoCampania);
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

    private static ParticipanteResuelto ParticipanteConCoaching()
    {
        var pregunta = CrearPregunta("p_1", 1, 2);
        var campania = CrearCampania(
            new[] { pregunta },
            configConversacional: ConfigConversacional.Crear(
                2,
                "Gracias por participar.",
                segmentacionIdeas: true,
                coachingSecuencialIdeas: true));
        var usuario = FabricasDominio.CrearUsuario("u_1", Numero, RolUsuario.Participante);
        var participante = FabricasDominio.CrearParticipante("pc_1", "c_1", "u_1", Numero);
        return new ParticipanteResuelto(usuario, campania, participante, pregunta);
    }

    private static ParticipanteResuelto ParticipanteConParafraseo()
    {
        var pregunta = CrearPregunta("p_1", 1, 1);
        var campania = CrearCampania(
            new[] { pregunta },
            configConversacional: ConfigConversacional.Crear(1, "Gracias por participar.", parafraseo: true));
        var usuario = FabricasDominio.CrearUsuario("u_1", Numero, RolUsuario.Participante);
        var participante = FabricasDominio.CrearParticipante("pc_1", "c_1", "u_1", Numero);
        return new ParticipanteResuelto(usuario, campania, participante, pregunta);
    }

    private static ParticipanteResuelto ParticipanteConUmbralCierre(double? umbralCierreAnticipado)
    {
        var pregunta = CrearPregunta("p_1", 1, 1);
        var campania = CrearCampania(
            new[] { pregunta },
            configConversacional: ConfigConversacional.Crear(
                1,
                "Gracias por participar.",
                umbralCierreAnticipado: umbralCierreAnticipado));
        var usuario = FabricasDominio.CrearUsuario("u_1", Numero, RolUsuario.Participante);
        var participante = FabricasDominio.CrearParticipante("pc_1", "c_1", "u_1", Numero);
        return new ParticipanteResuelto(usuario, campania, participante, pregunta);
    }

    private static ParticipanteResuelto ParticipanteConUmbralPregunta(double umbralPregunta, double? umbralCampania)
    {
        var pregunta = Pregunta.Crear(
            "p_1", "Pregunta 1", "Instruccion", "categoria", 1, EstadoRegistro.Activo,
            rubricaRef: null, versionRubrica: null, promptRefs: null, maxRepreguntas: 1,
            LimitesSeguridad.ParaPregunta(1500, 2), ConfigMarkdown.Crear(TipoArtefactoMarkdown.Respuesta),
            umbralCierreAnticipado: umbralPregunta);
        var campania = CrearCampania(
            new[] { pregunta },
            configConversacional: ConfigConversacional.Crear(1, "Gracias por participar.", umbralCierreAnticipado: umbralCampania));
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

    private static MensajeEntrante Mensaje(string texto, string? phoneNumberIdDestino = null)
        => new(Numero, texto, "wamid." + Guid.NewGuid().ToString("N"), Epoca, phoneNumberIdDestino);

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
        ConfigConversacional? configConversacional = null,
        EstadoCampania estado = EstadoCampania.Activa)
        => Campania.Crear(
            "c_1", "Campania c_1", "Descripcion", "Objetivo", estado,
            mensajesIniciales, preguntas,
            "rub_1",
            new Dictionary<string, string> { ["evaluar"] = "pr_eval" },
            "llm_1",
            ConfigMarkdown.Crear(TipoArtefactoMarkdown.Respuesta),
            configConversacional ?? ConfigConversacional.Crear(1, "Gracias por participar."),
            limites ?? LimitesSeguridad.Crear(1500, 10, 2),
            usuariosHabilitados: null, Epoca, Epoca);

    private static DominioEvaluacion CrearEvaluacion(
        RecomendacionEvaluacion recomendacion,
        string? repregunta,
        string retro = "Buena idea",
        decimal calificacionTotal = 4m,
        string? parafraseo = null)
        => DominioEvaluacion.Crear(
            "eval_1", "c_1", "resp_1", "u_1", "p_1", "rub_1", 1, "pr_eval", 1, "llm_1",
            new ConfigLlmSnapshot("AzureOpenAI", "gpt-4o-mini", "https://x", new Dictionary<string, object?>()),
            new Dictionary<string, decimal> { ["claridad"] = 1m },
            new[] { CalificacionCriterio.Crear("claridad", 4m, "clara") },
            calificacionTotal, "explica", retro, recomendacion, repregunta, new[] { "tema" }, new[] { "ent" }, false, Epoca,
            parafraseoDevuelto: parafraseo);

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

        public Task<IReadOnlyCollection<DominioConversacion>> ListarAbiertasInactivasAsync(string campaniaId, DateTimeOffset limite, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<DominioConversacion>>(
                _conversaciones.Values.Where(c => c.CampaniaId == campaniaId && c.Estado == EstadoConversacion.Abierta).ToArray());

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
