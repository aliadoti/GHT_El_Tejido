using System.Net;
using ElTejido.Application.Campanas;
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
/// P-29 §10 criterio 9 — E2E simulada del cierre por tiempo, <b>sin WhatsApp real</b>: inactividad →
/// cierre determinista de I-17/I-19 → aviso de pausa (redactado por LLM o con su respaldo) → reingreso
/// posterior delegado a P-26. El disparo y la transición son server-side; el LLM y el gateway están
/// mockeados (13 §1). El barrido corre a demanda: el trabajador de fondo espera su primer tick.
/// </summary>
public sealed class CierrePorTiempoP29E2EIntegrationTests
{
    private const string AppSecret = "appsec-p29";
    private const string Numero = "573001112233";
    private static readonly DateTimeOffset Epoca = DateTimeOffset.UnixEpoch;

    [Fact]
    public async Task Inactividad_CierraLaIdeaYEnviaUnAvisoRedactado_YElAportePosteriorAbreCicloP26()
    {
        var gateway = new GatewayDePrueba();
        var conversaciones = new ConversacionesFake();
        var respuestas = new RespuestasFake();
        var logs = new LogSeguridadFake();
        conversaciones.Sembrar(HiloAbierto(DateTimeOffset.UtcNow.AddMinutes(-30)));
        respuestas.Sembrar(IdeaAbierta());

        using var fabrica = Construir(
            gateway, conversaciones, respuestas, logs,
            RedactorQueDevuelve("Te dejo en pausa por ahora; retomamos cuando me escribas."));
        using var client = fabrica.CreateClient();

        var cerradas = await BarrerAsync(fabrica);

        // (1) El cierre es el de siempre: hilo cerrado e idea abierta finalizada como pendiente.
        cerradas.Should().Be(1);
        conversaciones.Todas.Single().Estado.Should().Be(EstadoConversacion.Cerrada);
        var idea = respuestas.Ideas.Single();
        idea.EstadoResultado.Should().Be(EstadoResultadoIdeaConsolidada.Pendiente);
        idea.MotivoCierre.Should().Be("inactividad");
        idea.NivelMadurez.Should().Be(NivelMadurez.Incubacion, "cerrar por tiempo nunca madura una idea");

        // (2) Un único aviso humano, con el texto del LLM y sin rastro de rúbrica ni puntajes.
        gateway.Enviados.Should().ContainSingle();
        var aviso = gateway.Enviados.Single();
        aviso.Tipo.Should().Be(TipoEnvioMensaje.Cierre);
        aviso.Texto.Should().Be("Te dejo en pausa por ahora; retomamos cuando me escribas.");
        aviso.Texto.Should().NotContainAny("rúbrica", "puntaje", "calificación", "umbral");

        // (3) Telemetría sin texto del participante ni del aviso.
        var evento = logs.Registrados.Single(log => log.TipoEvento == TipoEventoSeguridad.CierrePorInactividad);
        evento.Resultado.Should().Be("avisoEnviado");
        evento.CampaniaId.Should().Be("c_alfa");
        evento.Detalle.Should().Contain("envio:ok").And.NotContain(aviso.Texto);

        // (4) Un segundo barrido no repite el aviso: el hilo ya está cerrado.
        (await BarrerAsync(fabrica)).Should().Be(0);
        gateway.Enviados.Should().ContainSingle();

        // (5) El reingreso no lo resuelve P-29: un aporte sustantivo posterior abre su ciclo por P-26.
        await EnviarAsync(client, "wamid.p29.reingreso", "Retomo con un programa de mentoria inversa");
        await EsperarAsync(() => conversaciones.Todas.Any(c => c.CicloParticipacion == 2));
        var cicloNuevo = conversaciones.Todas.Single(c => c.CicloParticipacion == 2);
        cicloNuevo.OrigenAporteMessageId.Should().Be("wamid.p29.reingreso");
        cicloNuevo.Estado.Should().Be(EstadoConversacion.Abierta);
    }

    [Fact]
    public async Task RedactorNoDisponible_EnviaElRespaldoDeterministaYLoRegistra()
    {
        var gateway = new GatewayDePrueba();
        var conversaciones = new ConversacionesFake();
        var respuestas = new RespuestasFake();
        var logs = new LogSeguridadFake();
        conversaciones.Sembrar(HiloAbierto(DateTimeOffset.UtcNow.AddMinutes(-30)));
        respuestas.Sembrar(IdeaAbierta());

        var redactor = Substitute.For<IRedactorTurnoConversacional>();
        redactor.RedactarAsync(Arg.Any<ContextoRedaccionTurno>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoRedaccionTurno.Fallback("error_proveedor", null));
        using var fabrica = Construir(gateway, conversaciones, respuestas, logs, redactor);

        await BarrerAsync(fabrica);

        gateway.Enviados.Should().ContainSingle()
            .Which.Texto.Should().Be(OpcionesMensajesConversacion.PausaPorInactividadDefault);
        logs.Registrados.Single(log => log.TipoEvento == TipoEventoSeguridad.CierrePorInactividad)
            .Resultado.Should().Be("fallbackUsado");
        conversaciones.Todas.Single().Estado.Should().Be(EstadoConversacion.Cerrada);
        respuestas.Ideas.Single().MotivoCierre.Should().Be("inactividad");
    }

    [Fact]
    public async Task VentanaDeServicioVencida_ConservaElCierreYOmiteElEnvioLibre()
    {
        var gateway = new GatewayDePrueba();
        var conversaciones = new ConversacionesFake();
        var respuestas = new RespuestasFake();
        var logs = new LogSeguridadFake();
        // Hilo abierto desde hace 25 h: sigue siendo inactivo, pero la ventana de 24 h ya venció.
        conversaciones.Sembrar(HiloAbierto(DateTimeOffset.UtcNow.AddHours(-25)));
        respuestas.Sembrar(IdeaAbierta());

        using var fabrica = Construir(
            gateway, conversaciones, respuestas, logs, RedactorQueDevuelve("No debería enviarse."));

        (await BarrerAsync(fabrica)).Should().Be(1);

        gateway.Enviados.Should().BeEmpty("fuera de la ventana no se fuerza texto libre ni plantilla HSM");
        conversaciones.Todas.Single().Estado.Should().Be(EstadoConversacion.Cerrada);
        respuestas.Ideas.Single().MotivoCierre.Should().Be("inactividad");
        logs.Registrados.Single(log => log.TipoEvento == TipoEventoSeguridad.CierrePorInactividad)
            .Resultado.Should().Be("avisoOmitidoSinVentana");
    }

    [Fact]
    public async Task KillSwitchApagado_CierraIgualPeroSinAviso()
    {
        var gateway = new GatewayDePrueba();
        var conversaciones = new ConversacionesFake();
        var respuestas = new RespuestasFake();
        var logs = new LogSeguridadFake();
        conversaciones.Sembrar(HiloAbierto(DateTimeOffset.UtcNow.AddMinutes(-30)));
        respuestas.Sembrar(IdeaAbierta());

        using var fabrica = Construir(
            gateway, conversaciones, respuestas, logs,
            RedactorQueDevuelve("No debería enviarse."), cierrePorTiempo: false);

        (await BarrerAsync(fabrica)).Should().Be(1);

        gateway.Enviados.Should().BeEmpty();
        logs.Registrados.Should().NotContain(log => log.TipoEvento == TipoEventoSeguridad.CierrePorInactividad);
        conversaciones.Todas.Single().Estado.Should().Be(EstadoConversacion.Cerrada);
        respuestas.Ideas.Single().MotivoCierre.Should().Be("inactividad");
    }

    private static async Task<int> BarrerAsync(WebApplicationFactory<Program> fabrica)
    {
        using var scope = fabrica.Services.CreateScope();
        var servicio = scope.ServiceProvider.GetRequiredService<ServicioExpiracionConversaciones>();
        return await servicio.CerrarExpiradasAsync(CancellationToken.None);
    }

    private static DominioConversacion HiloAbierto(DateTimeOffset inicio)
        => DominioConversacion.Iniciar("conv_p29", "c_alfa", "u_1", "p_1", "whatsapp", null, inicio);

    private static IdeaConsolidada IdeaAbierta()
        => IdeaConsolidada.Crear("idea_p29", "c_alfa", "u_1", "p_1", "conv_p29", "resp_1", 1, Epoca);

    private static IRedactorTurnoConversacional RedactorQueDevuelve(string puente)
    {
        var redactor = Substitute.For<IRedactorTurnoConversacional>();
        redactor.RedactarAsync(Arg.Any<ContextoRedaccionTurno>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoRedaccionTurno.Exito(puente, null, UsoTokensLlm.Crear(12, 5)));
        return redactor;
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
        RespuestasFake respuestas,
        LogSeguridadFake logs,
        IRedactorTurnoConversacional redactor,
        bool cierrePorTiempo = true)
    {
        var dedupe = Substitute.For<IRegistroWebhookDedupe>();
        dedupe.IntentarRegistrarMensajeAsync(Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var campania = CrearCampania();
        var campanias = Substitute.For<IRepositorioCampanias>();
        campanias.BuscarCampaniasAsync(Arg.Any<FiltroCampanias>(), Arg.Any<CancellationToken>())
            .Returns(new[] { campania });
        campanias.ObtenerCampaniaPorIdAsync("c_alfa", Arg.Any<CancellationToken>()).Returns(campania);

        var participantes = Substitute.For<IRepositorioParticipantes>();
        participantes.ObtenerParticipantePorUsuarioAsync("c_alfa", "u_1", Arg.Any<CancellationToken>())
            .Returns(CrearParticipante());

        var configuracion = Substitute.For<IRepositorioConfiguracion>();
        configuracion.ObtenerUltimaRubricaAsync("rub_1", Arg.Any<CancellationToken>()).Returns(CrearRubrica());
        configuracion.ObtenerUltimoPromptAsync("pr_eval", Arg.Any<CancellationToken>()).Returns(CrearPrompt("pr_eval", "evaluar"));
        configuracion.ObtenerUltimoPromptAsync("pr_cierre", Arg.Any<CancellationToken>()).Returns(CrearPrompt("pr_cierre", "cierre"));
        configuracion.ObtenerConfigLlmAsync("llm_1", Arg.Any<CancellationToken>()).Returns(CrearConfig());

        var evaluador = Substitute.For<IEvaluadorLlm>();
        evaluador.EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Exito(CrearEvaluacion()));

        var resolutor = new ResolutorCandidatosFake(campania);

        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration(config =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Secretos:wa-appsec"] = AppSecret,
                    // El umbral y el barrido son los de I-17 §7; P-29 solo agrega el aviso.
                    ["Conversacion:MinutosInactividadSesion"] = "5",
                    ["Conversacion:CierrePorTiempoHabilitado"] = cierrePorTiempo.ToString(),
                    ["Conversacion:ConsolidacionProgresivaHabilitada"] = "false",
                    ["Conversacion:ConfirmacionExplicitaIdeasHabilitada"] = "true",
                }));

            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IWhatsAppGateway>(gateway);
                services.AddSingleton(dedupe);
                services.AddSingleton<IResolutorParticipante>(resolutor);
                services.AddSingleton<IRepositorioConversaciones>(conversaciones);
                services.AddSingleton(respuestas.Repositorio);
                services.AddSingleton<IRepositorioLogSeguridad>(logs);
                services.AddSingleton(campanias);
                services.AddSingleton(participantes);
                services.AddSingleton(configuracion);
                services.AddSingleton(evaluador);
                services.AddSingleton(redactor);
                services.AddSingleton(Substitute.For<ICompiladorMarkdown>());
                services.AddSingleton(Substitute.For<IProveedorCorrelacion>());
                services.AddScoped<IOrquestadorConversacion, OrquestadorConversacion>();
                services.AddScoped<ProcesadorWebhookEntrante>();
                services.AddScoped<ServicioExpiracionConversaciones>();
            });
        });
    }

    private static Campania CrearCampania()
        => Campania.Crear(
            "c_alfa", "Alfa", "Descripcion", "Objetivo", EstadoCampania.Activa, null,
            new[] { CrearPregunta() },
            "rub_1",
            new Dictionary<string, string> { ["evaluar"] = "pr_eval", ["cierre"] = "pr_cierre" },
            "llm_1",
            ConfigMarkdown.Crear(TipoArtefactoMarkdown.Respuesta),
            ConfigConversacional.Crear(
                1, "Gracias por participar.", minutosInactividadSesion: 5, participacionContinua: true),
            LimitesSeguridad.Crear(1500, 100, 100), null, Epoca, Epoca);

    private static Pregunta CrearPregunta()
        => Pregunta.Crear(
            "p_1", "Pregunta uno", "Se concreto", "categoria", 1, EstadoRegistro.Activo,
            null, null, null, 1, LimitesSeguridad.ParaPregunta(1500, 2),
            ConfigMarkdown.Crear(TipoArtefactoMarkdown.Respuesta));

    private static ParticipanteCampania CrearParticipante()
        => ParticipanteCampania.Crear(
            "pc_alfa", "c_alfa", "u_1", NumeroWhatsApp.FromNormalized(Numero), EstadoRegistro.Activo,
            EstadoEnvio.Enviado, EstadoRespuestaParticipante.SinRespuesta, Epoca, Epoca, null);

    private static Usuario CrearUsuario()
        => Usuario.Crear(
            "u_1", "Ana", NumeroWhatsApp.FromNormalized(Numero), RolUsuario.Participante,
            EstadoRegistro.Activo, "Operaciones", "GHT", null, null, Epoca, Epoca);

    private static Rubrica CrearRubrica()
        => Rubrica.Crear("rub_1", "Rubrica", "desc", "# Rubrica", EscalaRubrica.Crear(1, 5),
            new[] { CriterioRubrica.Crear("claridad", 1m) }, 1, EstadoRubrica.Activa, Epoca, Epoca);

    private static Prompt CrearPrompt(string id, string tipo)
        => Prompt.Crear(id, "Prompt", tipo, "Habla con calidez.", 1, EstadoPrompt.Activo, "u_admin", Epoca, Epoca, Epoca);

    private static ConfigLlm CrearConfig()
        => ConfigLlm.Crear("llm_1", "Azure", "AzureOpenAI", "gpt-4o-mini", "https://x", "llm-key", null,
            LimitesTokensLlm.Crear(6000, 800), 30, 2, EstadoRegistro.Activo, Epoca, Epoca);

    private static DominioEvaluacion CrearEvaluacion()
        => DominioEvaluacion.Crear(
            "eval_1", "c_alfa", "resp_1", "u_1", "p_1", "rub_1", 1, "pr_eval", 1, "llm_1",
            new ConfigLlmSnapshot("AzureOpenAI", "gpt-4o-mini", "https://x", new Dictionary<string, object?>()),
            new Dictionary<string, decimal> { ["claridad"] = 1m },
            new[] { CalificacionCriterio.Crear("claridad", 4m, "clara") },
            4m, "explica", "Buena idea", RecomendacionEvaluacion.Repreguntar, "¿Como lo medirias?",
            new[] { "tema" }, new[] { "ent" }, false, Epoca);

    private sealed class ResolutorCandidatosFake : IResolutorParticipante
    {
        private readonly Campania _campania;

        public ResolutorCandidatosFake(Campania campania) => _campania = campania;

        public Task<ResultadoResolucion> ResolverAsync(string numeroCrudo, CancellationToken cancellationToken)
            => Task.FromResult<ResultadoResolucion>(new ResultadoResolucion.Autorizado(
                new ParticipanteResuelto(CrearUsuario(), _campania, CrearParticipante(), _campania.Preguntas.First())));

        public Task<ResultadoCandidatos> ResolverCandidatosAsync(string numeroCrudo, CancellationToken cancellationToken)
            => Task.FromResult<ResultadoCandidatos>(new ResultadoCandidatos.Autorizado(
                CrearUsuario(),
                new[] { new CandidatoCampania(CrearParticipante(), _campania, _campania.Preguntas.First()) }));
    }

    private sealed class GatewayDePrueba : IWhatsAppGateway
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<(string Numero, string Texto, TipoEnvioMensaje Tipo)> _enviados = new();

        public IReadOnlyList<(string Numero, string Texto, TipoEnvioMensaje Tipo)> Enviados => _enviados.ToArray();

        public bool VerificarFirma(ReadOnlySpan<byte> cuerpoCrudo, string? firmaHeader, string appSecret) => true;

        public MensajeEntrante? ParsearWebhook(WhatsAppWebhookPayload payload)
        {
            var mensaje = payload.Entry?
                .SelectMany(e => e.Changes ?? Array.Empty<WhatsAppChange>())
                .Select(c => c.Value)
                .Where(v => v is not null)
                .SelectMany(v => v!.Messages ?? Array.Empty<WhatsAppMessage>())
                .FirstOrDefault(m => m.Type == "text" && !string.IsNullOrWhiteSpace(m.Text?.Body));

            return mensaje is null
                ? null
                : new MensajeEntrante(mensaje.From!, mensaje.Text!.Body!, mensaje.Id!, DateTimeOffset.UtcNow);
        }

        public Task<EnvioResultado> EnviarTextoAsync(string numeroE164, string texto, TipoEnvioMensaje tipo, CancellationToken cancellationToken, string? emisor = null)
        {
            _enviados.Enqueue((numeroE164, texto, tipo));
            return Task.FromResult(EnvioResultado.Ok("wamid.out"));
        }

        public Task<EnvioResultado> EnviarPlantillaAsync(string numeroE164, PlantillaWhatsApp plantilla, IReadOnlyDictionary<string, string> variables, TipoEnvioMensaje tipo, CancellationToken cancellationToken, string? emisor = null)
            => Task.FromResult(EnvioResultado.Ok("wamid.out"));

        public Task<EnvioResultado> EnviarPlantillaAutenticacionAsync(string numeroE164, PlantillaWhatsApp plantilla, string codigo, TipoEnvioMensaje tipo, CancellationToken cancellationToken, string? emisor = null)
            => Task.FromResult(EnvioResultado.Ok("wamid.out"));
    }

    /// <summary>Conversaciones en memoria con el filtro real de inactividad (I-17 §7).</summary>
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
            => Task.FromResult<IReadOnlyCollection<DominioConversacion>>(
                _conversaciones.Values
                    .Where(c => c.CampaniaId == campaniaId
                        && c.Estado == EstadoConversacion.Abierta
                        && UltimaActividad(c) <= limite)
                    .ToArray());

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

        private DateTimeOffset UltimaActividad(DominioConversacion conversacion)
            => _mensajes
                .Where(m => m.ConversacionId == conversacion.Id)
                .Select(m => m.Timestamp)
                .DefaultIfEmpty(conversacion.FechaInicio)
                .Max();
    }

    /// <summary>
    /// Ideas consolidadas en memoria sobre el puerto real: solo se sustituyen las operaciones que el
    /// cierre por inactividad toca (I-19 §4.8); el resto del repositorio queda mockeado.
    /// </summary>
    private sealed class RespuestasFake
    {
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, IdeaConsolidada> _ideas = new(StringComparer.Ordinal);

        public RespuestasFake()
        {
            Repositorio = Substitute.For<IRepositorioRespuestas>();
            Repositorio.ListarIdeasConsolidadasAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(llamada => (IReadOnlyCollection<IdeaConsolidada>)_ideas.Values
                    .Where(idea => idea.CampaniaId == llamada.ArgAt<string>(0))
                    .ToArray());
            Repositorio.ObtenerIdeaConsolidadaAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(llamada => _ideas.GetValueOrDefault(llamada.ArgAt<string>(1)));
            Repositorio.GuardarIdeaConsolidadaAsync(
                    Arg.Do<IdeaConsolidada>(idea => _ideas[idea.Id] = idea), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);
        }

        public IRepositorioRespuestas Repositorio { get; }

        public IReadOnlyList<IdeaConsolidada> Ideas => _ideas.Values.ToArray();

        public void Sembrar(IdeaConsolidada idea) => _ideas[idea.Id] = idea;
    }

    private sealed class LogSeguridadFake : IRepositorioLogSeguridad
    {
        private readonly System.Collections.Concurrent.ConcurrentBag<LogSeguridad> _logs = new();

        public IReadOnlyList<LogSeguridad> Registrados => _logs.ToArray();

        public Task RegistrarAsync(LogSeguridad log, CancellationToken cancellationToken)
        {
            _logs.Add(log);
            return Task.CompletedTask;
        }
    }
}
