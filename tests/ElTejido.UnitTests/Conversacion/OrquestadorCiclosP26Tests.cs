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

    private async Task<DominioConversacion> SembrarConversacionCerradaAsync()
    {
        var cerrada = DominioConversacion
            .Iniciar("conv_c_1_u_1_p_1", "c_1", "u_1", "p_1", "whatsapp", null, Epoca)
            .Cerrar(Epoca.AddMinutes(5));
        await _conversaciones.GuardarConversacionAsync(cerrada, CancellationToken.None);
        return cerrada;
    }

    private OrquestadorConversacion Construir()
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
            new OpcionesConversacion(),
            _reloj);

    private static ParticipanteResuelto Participante()
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
            ConfigConversacional.Crear(1, "Gracias por participar.", participacionContinua: true),
            LimitesSeguridad.Crear(1500, 10, 2),
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

        public IReadOnlyCollection<DominioConversacion> Conversaciones => _conversaciones.Values.ToArray();

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
            => Task.FromResult<IReadOnlyCollection<Mensaje>>(Array.Empty<Mensaje>());

        public Task GuardarMensajeAsync(Mensaje mensaje, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<ConteoBorradoConversaciones> EliminarPorUsuarioAsync(string campaniaId, string? usuarioId, CancellationToken cancellationToken)
            => Task.FromResult(new ConteoBorradoConversaciones(0, 0));
    }
}
