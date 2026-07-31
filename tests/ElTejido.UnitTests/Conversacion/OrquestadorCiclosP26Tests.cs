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
using ElTejido.Domain.Participantes;
using ElTejido.Domain.Respuestas;
using ElTejido.Domain.Usuarios;
using ElTejido.UnitTests.Soporte;
using FluentAssertions;
using NSubstitute;
using DominioConversacion = ElTejido.Domain.Conversaciones.Conversacion;
using DominioEvaluacion = ElTejido.Domain.Evaluacion.Evaluacion;

namespace ElTejido.UnitTests.Conversacion;

/// <summary>
/// P-26 corte 3 (spec §5.6/§5.7, 05 §4.4.3): entrega dirigida de un aporte ya enrutado. Con la
/// conversación reciente abierta se procesa allí (afinidad); cerrada abre un <b>ciclo nuevo</b>
/// independiente con id derivado del mensaje raíz (un reintento no lo duplica) y sin tocar el hilo
/// anterior; sin conversación aplica el primer contacto de siempre.
/// </summary>
public sealed class OrquestadorCiclosP26Tests
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

    public OrquestadorCiclosP26Tests()
    {
        _configuracion.ObtenerUltimaRubricaAsync("rub_1", Arg.Any<CancellationToken>()).Returns(CrearRubrica());
        _configuracion.ObtenerUltimoPromptAsync("pr_eval", Arg.Any<CancellationToken>()).Returns(CrearPrompt());
        _configuracion.ObtenerConfigLlmAsync("llm_1", Arg.Any<CancellationToken>()).Returns(CrearConfig());
        _correlacion.CorrelationIdActual.Returns("corr_test");
        _gateway.EnviarTextoAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TipoEnvioMensaje>(), Arg.Any<CancellationToken>(), Arg.Any<string?>())
            .Returns(EnvioResultado.Ok("wamid.out"));
        _evaluador.EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Exito(CrearEvaluacion()));
    }

    [Fact]
    public async Task AporteEnrutado_ConHiloCerrado_AbreCicloNuevoSinTocarElAnterior()
    {
        var cerrada = await SembrarConversacionCerradaAsync();

        await Construir().ProcesarAporteEnrutadoAsync(
            Participante(),
            Mensaje("Una idea totalmente nueva", "wamid.raiz2"),
            new ContextoAporteEnrutado("p_1", "route_u_1_wamid.raiz2"),
            CancellationToken.None);

        var ciclos = _conversaciones.Conversaciones
            .Where(c => c.PreguntaId == "p_1")
            .OrderBy(c => c.CicloParticipacion)
            .ToArray();
        ciclos.Should().HaveCount(2, "el hilo cerrado se conserva y el ciclo nuevo es otra conversación");
        var nuevo = ciclos[1];
        nuevo.Id.Should().NotBe(cerrada.Id);
        nuevo.CicloParticipacion.Should().Be(2);
        nuevo.OrigenAporteMessageId.Should().Be("wamid.raiz2");
        nuevo.EnrutamientoAporteId.Should().Be("route_u_1_wamid.raiz2");
        _conversaciones.Conversaciones.Single(c => c.Id == cerrada.Id).Estado
            .Should().Be(EstadoConversacion.Cerrada, "la conversación anterior queda inmutable");
    }

    [Fact]
    public async Task AporteEnrutado_ElAporteDelCicloNuevoSeEvaluaNoSeTrataComoSaludo()
    {
        await SembrarConversacionCerradaAsync();
        ContextoEvaluacion? evaluado = null;
        _evaluador.EvaluarAsync(Arg.Do<ContextoEvaluacion>(c => evaluado = c), Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Exito(CrearEvaluacion()));

        await Construir().ProcesarAporteEnrutadoAsync(
            Participante(),
            Mensaje("Propongo un tablero de indicadores", "wamid.raiz2"),
            new ContextoAporteEnrutado("p_1", null),
            CancellationToken.None);

        evaluado.Should().NotBeNull("el aporte que abre el ciclo es contenido sustantivo, no un saludo");
        evaluado!.RespuestaTexto.Should().Be("Propongo un tablero de indicadores");
    }

    [Fact]
    public async Task AporteEnrutado_MismoMensajeRaizDosVeces_NoDuplicaElCiclo()
    {
        await SembrarConversacionCerradaAsync();
        var orquestador = Construir();
        var contexto = new ContextoAporteEnrutado("p_1", null);

        await orquestador.ProcesarAporteEnrutadoAsync(Participante(), Mensaje("Idea", "wamid.raiz2"), contexto, CancellationToken.None);
        await orquestador.ProcesarAporteEnrutadoAsync(Participante(), Mensaje("Idea", "wamid.raiz2"), contexto, CancellationToken.None);

        _conversaciones.Conversaciones.Count(c => c.CicloParticipacion == 2)
            .Should().Be(1, "el id del ciclo es determinista por mensaje raíz");
    }

    [Fact]
    public async Task AporteEnrutado_ConHiloAbierto_ContinuaAlliSinCrearCiclo()
    {
        await _conversaciones.GuardarConversacionAsync(
            DominioConversacion.Iniciar("conv_c_1_u_1_p_1", "c_1", "u_1", "p_1", "whatsapp", null, Epoca),
            CancellationToken.None);

        await Construir().ProcesarAporteEnrutadoAsync(
            Participante(),
            Mensaje("Respuesta al coaching", "wamid.seguimiento"),
            new ContextoAporteEnrutado("p_1", null),
            CancellationToken.None);

        _conversaciones.Conversaciones.Should().ContainSingle();
        _conversaciones.Conversaciones.Single().CicloParticipacion.Should().Be(1);
        await _evaluador.Received(1).EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AporteEnrutado_SinConversacionPrevia_AplicaPrimerContactoYNoEvalua()
    {
        await Construir().ProcesarAporteEnrutadoAsync(
            Participante(),
            Mensaje("Hola", "wamid.primero"),
            new ContextoAporteEnrutado("p_1", null),
            CancellationToken.None);

        _conversaciones.Conversaciones.Should().ContainSingle()
            .Which.EstadoMaquina.Should().Be(EstadoMaquinaConversacion.EsperandoRespuestaInicial);
        await _evaluador.DidNotReceiveWithAnyArgs().EvaluarAsync(default!, default);
    }

    [Fact]
    public async Task AporteEnrutado_PreguntaYaNoActiva_DegradaAlFlujoNormal()
    {
        await _conversaciones.GuardarConversacionAsync(
            DominioConversacion.Iniciar("conv_c_1_u_1_p_1", "c_1", "u_1", "p_1", "whatsapp", null, Epoca),
            CancellationToken.None);

        await Construir().ProcesarAporteEnrutadoAsync(
            Participante(),
            Mensaje("Mi respuesta", "wamid.x"),
            new ContextoAporteEnrutado("p_inexistente", null),
            CancellationToken.None);

        // El flujo normal resuelve la pregunta vigente (p_1) y evalúa allí: nunca se pierde el aporte.
        await _evaluador.Received(1).EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>());
        _conversaciones.Conversaciones.Should().ContainSingle();
    }

    // ---------------------------------------------------------------------------------------------
    // Corte 4: reapertura entre alcances (§5.8) y cupos móviles de 24 h (§9).
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task Reapertura_PeticionExplicitaTrasCerrarElRecorrido_NoCreaCicloYConservaElIdeaId()
    {
        var cerrada = await SembrarConversacionCerradaAsync();
        var almacen = ConfigurarAlmacenIdeasCerradas(cerrada.Id);

        await Construir(consolidador: ConsolidadorNeutro()).ProcesarAporteEnrutadoAsync(
            Participante(),
            Mensaje("quiero complementar la anterior", "wamid.reapertura"),
            new ContextoAporteEnrutado("p_1", null),
            CancellationToken.None);

        _conversaciones.Conversaciones.Should().ContainSingle("una reapertura explícita no abre un ciclo nuevo");
        var hilo = _conversaciones.Conversaciones.Single();
        hilo.Id.Should().Be(cerrada.Id);
        hilo.Estado.Should().Be(EstadoConversacion.Abierta, "el hilo que contiene la idea se reabre");
        hilo.CicloParticipacion.Should().Be(1);
        almacen.Ideas.Should().ContainSingle().Which.Value.Id
            .Should().Be("idea_1", "la idea reabierta conserva su ideaId");
    }

    [Fact]
    public async Task Reapertura_AporteNormalTrasCerrarElRecorrido_SiCreaCicloNuevo()
    {
        var cerrada = await SembrarConversacionCerradaAsync();
        ConfigurarAlmacenIdeasCerradas(cerrada.Id);

        await Construir(consolidador: ConsolidadorNeutro()).ProcesarAporteEnrutadoAsync(
            Participante(),
            Mensaje("Propongo además un programa de mentoría inversa", "wamid.otra"),
            new ContextoAporteEnrutado("p_1", null),
            CancellationToken.None);

        _conversaciones.Conversaciones.Should().HaveCount(2, "un aporte normal sí abre otro ciclo");
        _conversaciones.Conversaciones.Single(c => c.Id != cerrada.Id).CicloParticipacion.Should().Be(2);
        _conversaciones.Conversaciones.Single(c => c.Id == cerrada.Id).Estado
            .Should().Be(EstadoConversacion.Cerrada);
    }

    [Fact]
    public async Task Reapertura_SinIdeasCandidatas_SigueElCaminoNormalDeCicloNuevo()
    {
        var cerrada = await SembrarConversacionCerradaAsync();
        ConfigurarAlmacenIdeasVacio();

        await Construir(consolidador: ConsolidadorNeutro()).ProcesarAporteEnrutadoAsync(
            Participante(),
            Mensaje("quiero complementar la anterior", "wamid.sinideas"),
            new ContextoAporteEnrutado("p_1", null),
            CancellationToken.None);

        _conversaciones.Conversaciones.Should().HaveCount(2);
        _conversaciones.Conversaciones.Single(c => c.Id == cerrada.Id).Estado
            .Should().Be(EstadoConversacion.Cerrada, "sin candidatas no hay nada que reabrir");
    }

    [Fact]
    public async Task CuposMoviles_CampaniaContinua_SoloCuentaLasUltimas24Horas()
    {
        // 3 entrantes viejos (fuera de ventana) + 1 reciente; el cupo es 2.
        var reloj = new RelojFijo(Epoca.AddDays(5));
        var conversacion = DominioConversacion.Iniciar("conv_c_1_u_1_p_1", "c_1", "u_1", "p_1", "whatsapp", null, Epoca);
        await _conversaciones.GuardarConversacionAsync(conversacion, CancellationToken.None);
        _conversaciones.SembrarEntrantes(conversacion.Id, Epoca, cantidad: 3);
        _conversaciones.SembrarEntrantes(conversacion.Id, reloj.GetUtcNow().AddHours(-1), cantidad: 1);

        var orquestador = Construir(
            opciones: new OpcionesConversacion { CuposHabilitados = true },
            reloj: reloj);
        await orquestador.ProcesarMensajeEntranteAsync(
            Participante(maxMensajesPorUsuario: 2, participacionContinua: true),
            Mensaje("Sigo participando", "wamid.hoy"),
            CancellationToken.None);

        await _evaluador.Received(1).EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CuposMoviles_CampaniaNoContinua_ConservaElAcumuladoHistorico()
    {
        var reloj = new RelojFijo(Epoca.AddDays(5));
        var conversacion = DominioConversacion.Iniciar("conv_c_1_u_1_p_1", "c_1", "u_1", "p_1", "whatsapp", null, Epoca);
        await _conversaciones.GuardarConversacionAsync(conversacion, CancellationToken.None);
        _conversaciones.SembrarEntrantes(conversacion.Id, Epoca, cantidad: 3);

        var orquestador = Construir(
            opciones: new OpcionesConversacion { CuposHabilitados = true },
            reloj: reloj);
        await orquestador.ProcesarMensajeEntranteAsync(
            Participante(maxMensajesPorUsuario: 2, participacionContinua: false),
            Mensaje("Otro mensaje", "wamid.hoy"),
            CancellationToken.None);

        await _evaluador.DidNotReceiveWithAnyArgs().EvaluarAsync(default!, default);
    }

    [Fact]
    public async Task CuposMoviles_MensajesDentroDeLaVentana_SiAgotanElCupo()
    {
        var reloj = new RelojFijo(Epoca.AddDays(5));
        var conversacion = DominioConversacion.Iniciar("conv_c_1_u_1_p_1", "c_1", "u_1", "p_1", "whatsapp", null, Epoca);
        await _conversaciones.GuardarConversacionAsync(conversacion, CancellationToken.None);
        _conversaciones.SembrarEntrantes(conversacion.Id, reloj.GetUtcNow().AddHours(-2), cantidad: 2);

        var orquestador = Construir(
            opciones: new OpcionesConversacion { CuposHabilitados = true },
            reloj: reloj);
        await orquestador.ProcesarMensajeEntranteAsync(
            Participante(maxMensajesPorUsuario: 2, participacionContinua: true),
            Mensaje("Uno más", "wamid.hoy"),
            CancellationToken.None);

        await _evaluador.DidNotReceiveWithAnyArgs().EvaluarAsync(default!, default);
    }

    /// <summary>Consolidador que devuelve el texto tal cual: aísla la prueba del contenido generado.</summary>
    private static IConsolidadorIdeas ConsolidadorNeutro()
    {
        var consolidador = Substitute.For<IConsolidadorIdeas>();
        consolidador.ConsolidarAsync(Arg.Any<ContextoConsolidacionIdeas>(), Arg.Any<CancellationToken>())
            .Returns(llamada => new ResultadoConsolidacionIdeas.Exito(
                llamada.Arg<ContextoConsolidacionIdeas>().NuevoAporte, TipoAporteIdea.Inicial, [], false, null, false, null));
        return consolidador;
    }

    /// <summary>Una idea cerrada del hilo indicado, candidata a reapertura (I-19 §4.7).</summary>
    private AlmacenIdeasFake ConfigurarAlmacenIdeasCerradas(string conversacionId)
    {
        var almacen = ConfigurarAlmacenIdeasVacio();
        var idea = IdeaConsolidada
            .Crear("idea_1", "c_1", "u_1", "p_1", conversacionId, "resp_1", 1, Epoca)
            .ConPropuesta("idea_1_v1", Epoca)
            .ConfirmarVersion("idea_1_v1", Epoca)
            .Cerrar(EstadoResultadoIdeaConsolidada.Pendiente, null, "participante", Epoca);
        almacen.Ideas[idea.Id] = idea;
        return almacen;
    }

    private AlmacenIdeasFake ConfigurarAlmacenIdeasVacio()
    {
        var almacen = new AlmacenIdeasFake();
        _respuestas.GuardarIdeaConsolidadaAsync(Arg.Do<IdeaConsolidada>(idea => almacen.Ideas[idea.Id] = idea), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _respuestas.GuardarVersionIdeaAsync(Arg.Do<VersionIdeaConsolidada>(v => almacen.Versiones[v.Id] = v), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _respuestas.ObtenerIdeaConsolidadaAsync("c_1", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(l => almacen.Ideas.GetValueOrDefault(l.ArgAt<string>(1)));
        _respuestas.ObtenerVersionIdeaAsync("c_1", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(l => almacen.Versiones.GetValueOrDefault(l.ArgAt<string>(1)));
        _respuestas.ListarIdeasConsolidadasAsync("c_1", Arg.Any<CancellationToken>())
            .Returns(_ => (IReadOnlyCollection<IdeaConsolidada>)almacen.Ideas.Values.ToArray());
        _respuestas.ListarVersionesIdeaAsync("c_1", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(l => (IReadOnlyCollection<VersionIdeaConsolidada>)almacen.Versiones.Values
                .Where(v => v.IdeaId == l.ArgAt<string>(1)).ToArray());
        return almacen;
    }

    private sealed class AlmacenIdeasFake
    {
        public Dictionary<string, IdeaConsolidada> Ideas { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, VersionIdeaConsolidada> Versiones { get; } = new(StringComparer.Ordinal);
    }

    private async Task<DominioConversacion> SembrarConversacionCerradaAsync()
    {
        var cerrada = DominioConversacion
            .Iniciar("conv_c_1_u_1_p_1", "c_1", "u_1", "p_1", "whatsapp", null, Epoca)
            .Cerrar(Epoca.AddMinutes(5));
        await _conversaciones.GuardarConversacionAsync(cerrada, CancellationToken.None);
        return cerrada;
    }

    private OrquestadorConversacion Construir(
        OpcionesConversacion? opciones = null,
        IConsolidadorIdeas? consolidador = null,
        TimeProvider? reloj = null)
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
            reloj ?? _reloj,
            consolidador);

    private static ParticipanteResuelto Participante(
        bool participacionContinua = true,
        int maxMensajesPorUsuario = 10)
    {
        var pregunta = CrearPregunta("p_1", 1);
        var campania = Campania.Crear(
            "c_1", "Campania c_1", "Descripcion", "Objetivo", EstadoCampania.Activa,
            mensajesIniciales: null,
            new[] { pregunta },
            "rub_1",
            new Dictionary<string, string> { ["evaluar"] = "pr_eval" },
            "llm_1",
            ConfigMarkdown.Crear(TipoArtefactoMarkdown.Respuesta),
            ConfigConversacional.Crear(1, "Gracias por participar.", participacionContinua: participacionContinua),
            LimitesSeguridad.Crear(1500, maxMensajesPorUsuario, 2),
            usuariosHabilitados: null,
            Epoca,
            Epoca);
        var usuario = FabricasDominio.CrearUsuario("u_1", Numero, RolUsuario.Participante);
        var participante = FabricasDominio.CrearParticipante("pc_1", "c_1", "u_1", Numero);
        return new ParticipanteResuelto(usuario, campania, participante, pregunta);
    }

    private static MensajeEntrante Mensaje(string texto, string wamid)
        => new(Numero, texto, wamid, Epoca);

    private static Pregunta CrearPregunta(string id, int orden)
        => Pregunta.Crear(
            id, $"Pregunta {orden}", "Instruccion", "categoria", orden, EstadoRegistro.Activo,
            rubricaRef: null, versionRubrica: null, promptRefs: null, maxRepreguntas: 1,
            LimitesSeguridad.ParaPregunta(1500, 2), ConfigMarkdown.Crear(TipoArtefactoMarkdown.Respuesta));

    private static Rubrica CrearRubrica()
        => Rubrica.Crear(
            "rub_1", "Rubrica", "desc", "# Rubrica", EscalaRubrica.Crear(1, 5),
            new[] { CriterioRubrica.Crear("claridad", 1m) }, 1, EstadoRubrica.Activa, Epoca, Epoca);

    private static Prompt CrearPrompt()
        => Prompt.Crear("pr_eval", "Prompt", "evaluar", "Eres evaluador.", 1, EstadoPrompt.Activo, "u_admin", Epoca, Epoca, Epoca);

    private static ConfigLlm CrearConfig()
        => ConfigLlm.Crear(
            "llm_1", "Azure", "AzureOpenAI", "gpt-4o-mini", "https://x", "llm-key", null,
            LimitesTokensLlm.Crear(6000, 800), 30, 2, EstadoRegistro.Activo, Epoca, Epoca);

    private static DominioEvaluacion CrearEvaluacion()
        => DominioEvaluacion.Crear(
            "eval_1", "c_1", "resp_1", "u_1", "p_1", "rub_1", 1, "pr_eval", 1, "llm_1",
            new ConfigLlmSnapshot("AzureOpenAI", "gpt-4o-mini", "https://x", new Dictionary<string, object?>()),
            new Dictionary<string, decimal> { ["claridad"] = 1m },
            new[] { CalificacionCriterio.Crear("claridad", 4m, "clara") },
            4m, "explica", "Buena idea", RecomendacionEvaluacion.Cerrar, null,
            new[] { "tema" }, new[] { "ent" }, false, Epoca);

    private sealed class FakeConversaciones : IRepositorioConversaciones
    {
        private readonly Dictionary<string, DominioConversacion> _conversaciones = new(StringComparer.Ordinal);
        private readonly List<Mensaje> _mensajes = [];

        public IReadOnlyCollection<DominioConversacion> Conversaciones => _conversaciones.Values.ToArray();

        /// <summary>Siembra entrantes con una marca de tiempo concreta (cupos §9: la ventana los filtra).</summary>
        public void SembrarEntrantes(string conversacionId, DateTimeOffset timestamp, int cantidad)
        {
            for (var i = 0; i < cantidad; i++)
            {
                _mensajes.Add(ElTejido.Domain.Conversaciones.Mensaje.Crear(
                    $"msg_{Guid.NewGuid():N}", "c_1", conversacionId, DireccionMensaje.In,
                    "texto", $"wamid.{Guid.NewGuid():N}", timestamp));
            }
        }

        public Task GuardarConversacionAsync(DominioConversacion conversacion, CancellationToken cancellationToken)
        {
            _conversaciones[conversacion.Id] = conversacion;
            return Task.CompletedTask;
        }

        public Task<DominioConversacion?> ObtenerConversacionAsync(string campaniaId, string conversacionId, CancellationToken cancellationToken)
            => Task.FromResult(_conversaciones.GetValueOrDefault(conversacionId));

        public Task<IReadOnlyCollection<DominioConversacion>> ListarConversacionesAsync(string campaniaId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<DominioConversacion>>(
                _conversaciones.Values.Where(c => c.CampaniaId == campaniaId).ToArray());

        public Task<IReadOnlyCollection<DominioConversacion>> ListarAbiertasInactivasAsync(string campaniaId, DateTimeOffset limite, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<DominioConversacion>>(Array.Empty<DominioConversacion>());

        public Task<IReadOnlyCollection<Mensaje>> ListarMensajesAsync(string campaniaId, string conversacionId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<Mensaje>>(
                _mensajes.Where(m => m.CampaniaId == campaniaId && m.ConversacionId == conversacionId).ToArray());

        public Task GuardarMensajeAsync(Mensaje mensaje, CancellationToken cancellationToken)
        {
            _mensajes.Add(mensaje);
            return Task.CompletedTask;
        }

        public Task<ConteoBorradoConversaciones> EliminarPorUsuarioAsync(string campaniaId, string? usuarioId, CancellationToken cancellationToken)
            => Task.FromResult(new ConteoBorradoConversaciones(0, 0));
    }
}
