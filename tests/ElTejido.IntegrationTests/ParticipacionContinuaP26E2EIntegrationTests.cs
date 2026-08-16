using System.Net;
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
using ElTejido.Domain.Seguridad;
using ElTejido.Domain.Usuarios;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using DominioConversacion = ElTejido.Domain.Conversaciones.Conversacion;
using DominioEvaluacion = ElTejido.Domain.Evaluacion.Evaluacion;

namespace ElTejido.IntegrationTests;

/// <summary>
/// P-26 §12 criterio 16 — E2E simulada del recorrido completo, <b>sin WhatsApp real</b>:
/// webhook → selección de campaña → selección de pregunta → coaching → madurez → aporte posterior →
/// idea nueva. Todo el enrutamiento es determinista y server-side; el LLM y el gateway están
/// mockeados (13 §1). Cubre además la serialización por participante de §11: dos aportes seguidos sin
/// afinidad no abren dos ciclos.
/// </summary>
public sealed class ParticipacionContinuaP26E2EIntegrationTests
{
    private const string AppSecret = "appsec-p26";
    private const string Numero = "573001112233";
    private static readonly DateTimeOffset Epoca = DateTimeOffset.UnixEpoch;

    [Fact]
    public async Task RecorridoCompleto_SeleccionaCampaniaYPregunta_LuegoAbreUnCicloNuevo()
    {
        var gateway = new GatewayDePrueba();
        var conversaciones = new ConversacionesFake();
        using var fabrica = Construir(gateway, conversaciones);
        using var client = fabrica.CreateClient();

        // (1) Aporte inicial con DOS campañas elegibles: se conserva y se ofrece el menú de campañas.
        await EnviarAsync(client, "wamid.p26.1", "Se me ocurrio una idea para la convencion");
        await EsperarAsync(() => gateway.Enviados.Count >= 1);
        gateway.Enviados.Last().Texto.Should().Contain("¿A cuál campaña corresponde tu aporte?");
        gateway.Enviados.Last().Texto.Should().Contain("1. Alfa").And.Contain("2. Beta");

        // (2) Selección de campaña: la elegida tiene dos preguntas activas => menú de preguntas.
        await EnviarAsync(client, "wamid.p26.2", "1");
        await EsperarAsync(() => gateway.Enviados.Count >= 2);
        gateway.Enviados.Last().Texto.Should().Contain("¿Sobre cuál pregunta quieres aportar?");
        gateway.Enviados.Last().Texto.Should().Contain("1. Pregunta uno").And.Contain("2. Pregunta dos");

        // (3) Selección de pregunta: se resuelve el alcance y arranca el hilo con la pregunta elegida.
        await EnviarAsync(client, "wamid.p26.3", "1");
        await EsperarAsync(() => gateway.Enviados.Any(e => e.Tipo == TipoEnvioMensaje.Inicial));
        var primerHilo = conversaciones.Todas.Should().ContainSingle().Which;
        primerHilo.PreguntaId.Should().Be("p_1", "se respetó la pregunta elegida");
        primerHilo.CampaniaId.Should().Be("c_alfa");
        primerHilo.CicloParticipacion.Should().Be(1);

        // (4) Aporte real: se evalúa y el coach ofrece una mejora (coaching).
        await EnviarAsync(client, "wamid.p26.4", "Propongo un tablero con indicadores de impacto");
        await EsperarAsync(() => gateway.Enviados.Any(e => e.Tipo == TipoEnvioMensaje.Repregunta));

        // (5) Respuesta al coaching: sin cupo de revisiones, la idea cierra (madurez) y el hilo tambien.
        await EnviarAsync(client, "wamid.p26.5", "La mejoro: agrego metas trimestrales y responsables");
        await EsperarAsync(() => conversaciones.Todas.Any(c => c.Estado == EstadoConversacion.Cerrada));

        // (6) Al cerrar, el orquestador abre por si mismo la siguiente pregunta del recorrido. Como la
        //     idea anterior terminó, la afinidad se cierra y el aporte vuelve a resolver alcance
        //     (siguen habiendo dos campañas elegibles). Se completa tambien esa pregunta: solo
        //     entonces la campaña queda sin trabajo pendiente.
        await EsperarAsync(() => conversaciones.Todas.Any(c => c.PreguntaId == "p_2"));
        await EnviarAsync(client, "wamid.p26.6", "Para la segunda pregunta propongo rotacion de roles");
        await EsperarAsync(() =>
            gateway.Enviados.Count(e => e.Texto.Contains("¿A cuál campaña corresponde tu aporte?", StringComparison.Ordinal)) >= 2);

        // Una sola pregunta pendiente => se selecciona sola, sin menú (§5.4).
        await EnviarAsync(client, "wamid.p26.7", "1");
        await EsperarAsync(() => conversaciones.Todas.Any(c => c.PreguntaId == "p_2" && c.RepreguntasUsadas > 0));
        await EnviarAsync(client, "wamid.p26.8", "La mejoro: defino duracion y criterios de rotacion");
        await EsperarAsync(() => conversaciones.Todas.Count(c => c.Estado == EstadoConversacion.Cerrada) >= 2);

        // (7) Aporte posterior con TODO el recorrido completado: la campaña continua vuelve a ofrecer
        //     sus preguntas (§5.4) en vez de rechazar el aporte.
        await EnviarAsync(client, "wamid.p26.9", "Ahora se me ocurre un programa de mentoria inversa");
        await EsperarAsync(() =>
            gateway.Enviados.Count(e => e.Texto.Contains("¿A cuál campaña corresponde tu aporte?", StringComparison.Ordinal)) >= 3);

        await EnviarAsync(client, "wamid.p26.10", "1");
        await EsperarAsync(() =>
            gateway.Enviados.Count(e => e.Texto.Contains("¿Sobre cuál pregunta quieres aportar?", StringComparison.Ordinal)) >= 2);

        await EnviarAsync(client, "wamid.p26.11", "2");

        // (8) La idea nueva vive en OTRA conversacion (ciclo 2); la anterior queda cerrada e intacta.
        await EsperarAsync(() => conversaciones.Todas.Count(c => c.PreguntaId == "p_2") >= 2);
        var ciclos = conversaciones.Todas
            .Where(c => c.CampaniaId == "c_alfa" && c.PreguntaId == "p_2")
            .OrderBy(c => c.CicloParticipacion)
            .ToArray();
        ciclos.Should().HaveCount(2);
        ciclos[0].Estado.Should().Be(EstadoConversacion.Cerrada, "el hilo anterior queda intacto");
        ciclos[1].CicloParticipacion.Should().Be(2);
        ciclos[1].OrigenAporteMessageId.Should().Be("wamid.p26.9", "el ciclo se deriva del aporte raíz");
        primerHilo.PreguntaId.Should().Be("p_1", "la idea nueva no toca el hilo de la otra pregunta");
    }

    [Fact]
    public async Task DosAportesSeguidosSinAfinidad_NoAbrenDosCiclos()
    {
        var gateway = new GatewayDePrueba();
        var conversaciones = new ConversacionesFake();
        // Hilo ya cerrado: el siguiente aporte abriría un ciclo nuevo.
        conversaciones.Sembrar(
            DominioConversacion
                .Iniciar("conv_previa", "c_alfa", "u_1", "p_1", "whatsapp", null, Epoca)
                .Cerrar(Epoca.AddMinutes(1)));
        using var fabrica = Construir(gateway, conversaciones, unaSolaCampania: true, unaSolaPregunta: true);
        using var client = fabrica.CreateClient();

        // §11: la cola del webhook tiene un solo lector, asi que los entrantes de un participante se
        // procesan en orden; el primero abre el ciclo y el segundo entra en esa misma conversacion.
        await EnviarAsync(client, "wamid.conc.1", "Primera idea nueva del dia");
        await EnviarAsync(client, "wamid.conc.2", "Segunda idea casi simultanea");

        await EsperarAsync(() => conversaciones.Todas.Count(c => c.CicloParticipacion == 2) >= 1);
        await Task.Delay(200);

        conversaciones.Todas.Count(c => c.CicloParticipacion == 2)
            .Should().Be(1, "nunca se abren dos ciclos/afinidades activas por accidente");
    }

    /// <summary>
    /// P-28 corte 3: el saludo de reingreso entra por webhook, abre la ventana de servicio y recibe
    /// una bienvenida, pero no abre ni altera una idea. El aporte posterior sigue siendo la raiz del
    /// ciclo nuevo P-26.
    /// </summary>
    [Fact]
    public async Task P28_SaludoSinFlujo_EnviaBienvenidaSinCrearIdea_YAportePosteriorAbreCicloP26()
    {
        var gateway = new GatewayDePrueba();
        var conversaciones = new ConversacionesFake();
        conversaciones.Sembrar(
            DominioConversacion
                .Iniciar("conv_previa", "c_alfa", "u_1", "p_1", "whatsapp", null, Epoca)
                .Cerrar(Epoca.AddMinutes(1)));
        using var fabrica = Construir(
            gateway,
            conversaciones,
            unaSolaCampania: true,
            unaSolaPregunta: true,
            despertarProactivo: true);
        using var client = fabrica.CreateClient();

        await EnviarAsync(client, "wamid.p28.saludo", "Hola");
        await EsperarAsync(() => gateway.Enviados.Any(e => e.Tipo == TipoEnvioMensaje.Inicial));

        gateway.Enviados.Last().Texto.Should().Contain("nueva idea");
        conversaciones.Todas.Should().ContainSingle().Which.Estado.Should().Be(EstadoConversacion.Cerrada);

        await EnviarAsync(client, "wamid.p28.aporte", "Propongo una red de mentoria entre equipos");
        await EsperarAsync(() => conversaciones.Todas.Any(c => c.CicloParticipacion == 2));

        var cicloNuevo = conversaciones.Todas.Single(c => c.CicloParticipacion == 2);
        cicloNuevo.OrigenAporteMessageId.Should().Be("wamid.p28.aporte");
    }

    private static async Task EnviarAsync(HttpClient client, string wamid, string texto)
    {
        var cuerpo =
            "{\"entry\":[{\"changes\":[{\"value\":{\"messages\":[{\"from\":\"" + Numero + "\",\"id\":\"" + wamid
            + "\",\"timestamp\":\"1700000000\",\"type\":\"text\",\"text\":{\"body\":\"" + texto + "\"}}]}}]}]}";
        using var contenido = new StringContent(cuerpo, System.Text.Encoding.UTF8, "application/json");
        contenido.Headers.Add("X-Hub-Signature-256", "sha256=ignorada-en-prueba");
        using var respuesta = await client.PostAsync("/webhook/whatsapp", contenido);
        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static async Task EsperarAsync(Func<bool> condicion)
    {
        var limite = DateTime.UtcNow.AddSeconds(5);
        while (!condicion() && DateTime.UtcNow < limite)
        {
            await Task.Delay(25);
        }

        condicion().Should().BeTrue("el efecto observable debe ocurrir dentro del tiempo de espera");
    }

    private static WebApplicationFactory<Program> Construir(
        GatewayDePrueba gateway,
        ConversacionesFake conversaciones,
        bool unaSolaCampania = false,
        bool unaSolaPregunta = false,
        bool despertarProactivo = false)
    {
        var dedupe = Substitute.For<IRegistroWebhookDedupe>();
        dedupe.IntentarRegistrarMensajeAsync(Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var configuracion = Substitute.For<IRepositorioConfiguracion>();
        configuracion.ObtenerUltimaRubricaAsync("rub_1", Arg.Any<CancellationToken>()).Returns(CrearRubrica());
        configuracion.ObtenerPromptVigenteAsync("pr_eval", Arg.Any<CancellationToken>())
            .Returns(ResolutorPromptRuntime.Resolver([CrearPrompt()]));
        configuracion.ObtenerConfigLlmAsync("llm_1", Arg.Any<CancellationToken>()).Returns(CrearConfig());

        var evaluador = Substitute.For<IEvaluadorLlm>();
        evaluador.EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Exito(CrearEvaluacion()));

        var resolutor = new ResolutorCandidatosFake(Candidatos(unaSolaCampania, unaSolaPregunta));

        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration(config =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Secretos:wa-appsec"] = AppSecret,
                    // Ruta canónica simple: I-19 tiene su propia batería de pruebas.
                    ["Conversacion:ConsolidacionProgresivaHabilitada"] = "false",
                    ["Conversacion:ConfirmacionExplicitaIdeasHabilitada"] = "true",
                    ["Conversacion:DespertarProactivoHabilitado"] = despertarProactivo.ToString(),
                }));

            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IWhatsAppGateway>(gateway);
                services.AddSingleton(dedupe);
                services.AddSingleton<IResolutorParticipante>(resolutor);
                services.AddSingleton<IRepositorioConversaciones>(conversaciones);
                services.AddSingleton(configuracion);
                services.AddSingleton(evaluador);
                services.AddSingleton(Substitute.For<IRepositorioRespuestas>());
                services.AddSingleton(Substitute.For<IRepositorioParticipantes>());
                services.AddSingleton(Substitute.For<ICompiladorMarkdown>());
                services.AddSingleton(Substitute.For<IRepositorioLogSeguridad>());
                services.AddSingleton(Substitute.For<IProveedorCorrelacion>());
                services.AddScoped<IOrquestadorConversacion, OrquestadorConversacion>();
                services.AddScoped<ProcesadorWebhookEntrante>();
            });
        });
    }

    /// <summary>Alfa (continua, 2 preguntas) y Beta (con trabajo pendiente): fuerzan ambos menús.</summary>
    private static IReadOnlyList<CandidatoCampania> Candidatos(bool unaSolaCampania, bool unaSolaPregunta)
    {
        var preguntasAlfa = unaSolaPregunta
            ? new[] { CrearPregunta("p_1", "Pregunta uno", 1) }
            : new[] { CrearPregunta("p_1", "Pregunta uno", 1), CrearPregunta("p_2", "Pregunta dos", 2) };
        var alfa = CrearCampania("c_alfa", "Alfa", preguntasAlfa, participacionContinua: true);
        var candidatos = new List<CandidatoCampania>
        {
            new(CrearParticipante("pc_alfa", "c_alfa"), alfa, preguntasAlfa[0]),
        };

        if (!unaSolaCampania)
        {
            var preguntasBeta = new[] { CrearPregunta("p_b1", "Pregunta beta", 1) };
            var beta = CrearCampania("c_beta", "Beta", preguntasBeta, participacionContinua: false);
            candidatos.Add(new CandidatoCampania(CrearParticipante("pc_beta", "c_beta"), beta, preguntasBeta[0]));
        }

        return candidatos;
    }

    private static Pregunta CrearPregunta(string id, string texto, int orden)
        => Pregunta.Crear(
            id, texto, "Se concreto", "categoria", orden, EstadoRegistro.Activo,
            null, null, null, 1, LimitesSeguridad.ParaPregunta(1500, 2),
            ConfigMarkdown.Crear(TipoArtefactoMarkdown.Respuesta));

    private static Campania CrearCampania(
        string id, string nombre, IEnumerable<Pregunta> preguntas, bool participacionContinua)
        => Campania.Crear(
            id, nombre, "Descripcion", "Objetivo", EstadoCampania.Activa, null, preguntas,
            "rub_1", new Dictionary<string, string> { ["evaluar"] = "pr_eval" }, "llm_1",
            ConfigMarkdown.Crear(TipoArtefactoMarkdown.Respuesta),
            ConfigConversacional.Crear(1, "Gracias por participar.", participacionContinua: participacionContinua),
            LimitesSeguridad.Crear(1500, 100, 100), null, Epoca, Epoca);

    private static ParticipanteCampania CrearParticipante(string id, string campaniaId)
        => ParticipanteCampania.Crear(
            id, campaniaId, "u_1", NumeroWhatsApp.FromNormalized(Numero), EstadoRegistro.Activo,
            EstadoEnvio.Enviado, EstadoRespuestaParticipante.SinRespuesta, Epoca, Epoca, null);

    private static Usuario CrearUsuario()
        => Usuario.Crear(
            "u_1", 1, "Ana", NumeroWhatsApp.FromNormalized(Numero), RolUsuario.Participante,
            EstadoRegistro.Activo, "Operaciones", "GHT", null, null, Epoca, Epoca);

    private static Rubrica CrearRubrica()
        => Rubrica.Crear("rub_1", "Rubrica", "desc", EscalaRubrica.Crear(1, 5),
            new[] { CriterioRubrica.Crear("claridad", 1m) }, 1, EstadoRubrica.Activa, Epoca, Epoca);

    private static Prompt CrearPrompt()
        => Prompt.Crear("pr_eval", "Prompt", "evaluar", "Eres evaluador.", 1, EstadoPrompt.Activo, "u_admin", Epoca, Epoca, Epoca);

    private static ConfigLlm CrearConfig()
        => ConfigLlm.Crear("llm_1", "Azure", "AzureOpenAI", "gpt-4o-mini", "https://x", "llm-key", null,
            LimitesTokensLlm.Crear(6000, 800), 30, 2, EstadoRegistro.Activo, Epoca, Epoca);

    private static DominioEvaluacion CrearEvaluacion()
        => DominioEvaluacion.Crear(
            "eval_1", "c_alfa", "resp_1", "u_1", "p_2", "rub_1", 1, "pr_eval", 1, "llm_1",
            new ConfigLlmSnapshot("AzureOpenAI", "gpt-4o-mini", "https://x", new Dictionary<string, object?>()),
            new Dictionary<string, decimal> { ["claridad"] = 1m },
            new[] { CalificacionCriterio.Crear("claridad", 4m, "clara") },
            4m, "explica", "Buena idea", RecomendacionEvaluacion.Repreguntar, "¿Como lo medirias?",
            new[] { "tema" }, new[] { "ent" }, false, Epoca);

    private sealed class ResolutorCandidatosFake : IResolutorParticipante
    {
        private readonly IReadOnlyList<CandidatoCampania> _candidatos;

        public ResolutorCandidatosFake(IReadOnlyList<CandidatoCampania> candidatos) => _candidatos = candidatos;

        public Task<ResultadoResolucion> ResolverAsync(string numeroCrudo, CancellationToken cancellationToken)
            => Task.FromResult<ResultadoResolucion>(new ResultadoResolucion.Autorizado(
                new ParticipanteResuelto(
                    CrearUsuario(), _candidatos[0].Campania, _candidatos[0].Participante, _candidatos[0].PreguntaVigente)));

        public Task<ResultadoCandidatos> ResolverCandidatosAsync(string numeroCrudo, CancellationToken cancellationToken)
            => Task.FromResult<ResultadoCandidatos>(new ResultadoCandidatos.Autorizado(CrearUsuario(), _candidatos));
    }

    private sealed class GatewayDePrueba : IWhatsAppGateway
    {
        public System.Collections.Concurrent.ConcurrentQueue<(string Numero, string Texto, TipoEnvioMensaje Tipo)> Enviados { get; } = new();

        public bool VerificarFirma(ReadOnlySpan<byte> cuerpoCrudo, string? firmaHeader, string appSecret) => true;

        public MensajeEntrante? ParsearWebhook(WhatsAppWebhookPayload payload)
        {
            var mensaje = payload.Entry?
                .SelectMany(e => e.Changes ?? Array.Empty<WhatsAppChange>())
                .Select(c => c.Value)
                .Where(v => v is not null)
                .SelectMany(v => v!.Messages ?? Array.Empty<WhatsAppMessage>())
                .FirstOrDefault(m => m.Type == "text" && !string.IsNullOrWhiteSpace(m.Text?.Body));

            // El timestamp debe ser el del mensaje real: la ventana de servicio (y con ella la
            // afinidad P-26 §5.6) se renueva desde el ultimo entrante.
            return mensaje is null
                ? null
                : new MensajeEntrante(mensaje.From!, mensaje.Text!.Body!, mensaje.Id!, DateTimeOffset.UtcNow);
        }

        public Task<EnvioResultado> EnviarTextoAsync(string numeroE164, string texto, TipoEnvioMensaje tipo, CancellationToken cancellationToken, string? emisor = null)
        {
            Enviados.Enqueue((numeroE164, texto, tipo));
            return Task.FromResult(EnvioResultado.Ok("wamid.out"));
        }

        public Task<EnvioResultado> EnviarPlantillaAsync(string numeroE164, PlantillaWhatsApp plantilla, IReadOnlyDictionary<string, string> variables, TipoEnvioMensaje tipo, CancellationToken cancellationToken, string? emisor = null)
            => Task.FromResult(EnvioResultado.Ok("wamid.out"));

        public Task<EnvioResultado> EnviarPlantillaAutenticacionAsync(string numeroE164, PlantillaWhatsApp plantilla, string codigo, TipoEnvioMensaje tipo, CancellationToken cancellationToken, string? emisor = null)
            => Task.FromResult(EnvioResultado.Ok("wamid.out"));
    }

    private sealed class ConversacionesFake : IRepositorioConversaciones
    {
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, DominioConversacion> _conversaciones = new(StringComparer.Ordinal);
        private readonly System.Collections.Concurrent.ConcurrentBag<Mensaje> _mensajes = new();

        public IReadOnlyList<DominioConversacion> Todas => _conversaciones.Values.ToArray();

        public void Sembrar(DominioConversacion conversacion) => _conversaciones[conversacion.Id] = conversacion;

        public Task GuardarConversacionAsync(DominioConversacion conversacion, CancellationToken cancellationToken)
        {
            _conversaciones[conversacion.Id] = conversacion;
            return Task.CompletedTask;
        }

        public Task<DominioConversacion?> ObtenerConversacionAsync(string campaniaId, string conversacionId, CancellationToken cancellationToken)
            => Task.FromResult(
                _conversaciones.TryGetValue(conversacionId, out var conversacion) && conversacion.CampaniaId == campaniaId
                    ? conversacion
                    : null);

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
