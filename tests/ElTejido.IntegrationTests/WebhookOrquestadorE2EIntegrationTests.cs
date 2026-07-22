using System.Net;
using System.Net.Http.Json;
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
/// E2E del camino entrante (04 §6, 05 §2.4/§4): un mensaje entrante autorizado recorre
/// webhook → cola in-process → <c>TrabajadorWebhook</c> → <c>ProcesadorWebhookEntrante</c> →
/// <c>OrquestadorConversacion</c> real → evaluacion → envio de cierre + cierre del hilo. WhatsApp y
/// LLM mockeados (13 §1). Verifica el spine completo del backend conversacional.
/// </summary>
public sealed class WebhookOrquestadorE2EIntegrationTests
{
    private const string AppSecret = "appsec-e2e";
    private const string Numero = "573001112233";
    private static readonly DateTimeOffset Epoca = DateTimeOffset.UnixEpoch;

    [Fact]
    public async Task MensajeEntranteAutorizado_RecorrePipelinePreguntaYOfreceMejora()
    {
        var gateway = new GatewayDePrueba();
        var conversaciones = new ConversacionesFake();

        using var fabrica = Construir(gateway, conversaciones);
        using var client = fabrica.CreateClient();

        const string cuerpoPrimerEntrante = "{\"entry\":[{\"changes\":[{\"value\":{\"messages\":[{\"from\":\"573001112233\",\"id\":\"wamid.E2E.1\",\"timestamp\":\"1700000000\",\"type\":\"text\",\"text\":{\"body\":\"Hola\"}}]}}]}]}";
        using var contenido = new StringContent(cuerpoPrimerEntrante, System.Text.Encoding.UTF8, "application/json");
        contenido.Headers.Add("X-Hub-Signature-256", "sha256=ignorada-en-prueba");

        using var respuesta = await client.PostAsync("/webhook/whatsapp", contenido);
        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);

        // El procesamiento es asincrono (worker de cola); se espera por el efecto observable.
        await EsperarAsync(() => gateway.Enviados.Count >= 1);

        // El primer entrante de un hilo nuevo recibe la pregunta vigente y NO se evalua.
        gateway.Enviados.Should().ContainSingle();
        gateway.Enviados.First().Tipo.Should().Be(TipoEnvioMensaje.Inicial);

        const string cuerpoRespuesta = "{\"entry\":[{\"changes\":[{\"value\":{\"messages\":[{\"from\":\"573001112233\",\"id\":\"wamid.E2E.2\",\"timestamp\":\"1700000000\",\"type\":\"text\",\"text\":{\"body\":\"Mi idea es reducir desperdicio\"}}]}}]}]}";
        using var contenidoRespuesta = new StringContent(cuerpoRespuesta, System.Text.Encoding.UTF8, "application/json");
        contenidoRespuesta.Headers.Add("X-Hub-Signature-256", "sha256=ignorada-en-prueba");

        using var respuestaReal = await client.PostAsync("/webhook/whatsapp", contenidoRespuesta);
        respuestaReal.StatusCode.Should().Be(HttpStatusCode.OK);

        // Tras la primera evaluacion valida el orquestador ofrece SIEMPRE una mejora (05 §4.4):
        // envia la retro + invitacion como Repregunta y deja el hilo abierto esperando el ajuste.
        await EsperarAsync(() => gateway.Enviados.Any(e => e.Tipo == TipoEnvioMensaje.Repregunta));

        await EsperarAsync(() => conversaciones.Ultima is { EstadoMaquina: EstadoMaquinaConversacion.EsperandoRepregunta });
        conversaciones.Ultima!.Estado.Should().Be(EstadoConversacion.Abierta);
        conversaciones.Ultima!.EstadoMaquina.Should().Be(EstadoMaquinaConversacion.EsperandoRepregunta);
    }

    private static WebApplicationFactory<Program> Construir(GatewayDePrueba gateway, ConversacionesFake conversaciones)
    {
        var dedupe = Substitute.For<IRegistroWebhookDedupe>();
        dedupe.IntentarRegistrarMensajeAsync(Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var resolutor = new ResolutorFake(CrearParticipante());

        var configuracion = Substitute.For<IRepositorioConfiguracion>();
        configuracion.ObtenerUltimaRubricaAsync("rub_1", Arg.Any<CancellationToken>()).Returns(CrearRubrica());
        configuracion.ObtenerUltimoPromptAsync("pr_eval", Arg.Any<CancellationToken>()).Returns(CrearPrompt());
        configuracion.ObtenerConfigLlmAsync("llm_1", Arg.Any<CancellationToken>()).Returns(CrearConfig());

        var evaluador = Substitute.For<IEvaluadorLlm>();
        evaluador.EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Exito(CrearEvaluacion()));

        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration(config =>
                config.AddInMemoryCollection(new Dictionary<string, string?> { ["Secretos:wa-appsec"] = AppSecret }));

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

                // El orquestador real y el procesador (normalmente guardados por Cosmos) se cablean aqui.
                services.AddScoped<IOrquestadorConversacion, OrquestadorConversacion>();
                services.AddScoped<ProcesadorWebhookEntrante>();
            });
        });
    }

    private static async Task EsperarAsync(Func<bool> condicion)
    {
        var limite = DateTime.UtcNow.AddSeconds(5);
        while (!condicion() && DateTime.UtcNow < limite)
        {
            await Task.Delay(25);
        }
    }

    private static ParticipanteResuelto CrearParticipante()
    {
        var pregunta = Pregunta.Crear(
            "p_1", "Idea para ingresos", "Se concreto", "ingresos", 1, EstadoRegistro.Activo,
            null, null, null, 1, LimitesSeguridad.ParaPregunta(1500, 2), ConfigMarkdown.Crear(TipoArtefactoMarkdown.Respuesta));

        var campania = Campania.Crear(
            "c_1", "Campania", "Descripcion", "Objetivo", EstadoCampania.Activa, null, new[] { pregunta },
            "rub_1", new Dictionary<string, string> { ["evaluar"] = "pr_eval" }, "llm_1",
            ConfigMarkdown.Crear(TipoArtefactoMarkdown.Respuesta), ConfigConversacional.Crear(1, "Gracias por participar."),
            LimitesSeguridad.Crear(1500, 10, 2), null, Epoca, Epoca);

        var usuario = Usuario.Crear(
            "u_1", "Ana", NumeroWhatsApp.FromNormalized(Numero), RolUsuario.Participante, EstadoRegistro.Activo,
            "Operaciones", "GHT", null, null, Epoca, Epoca);

        var participante = ParticipanteCampania.Crear(
            "pc_1", "c_1", "u_1", NumeroWhatsApp.FromNormalized(Numero), EstadoRegistro.Activo,
            EstadoEnvio.Enviado, EstadoRespuestaParticipante.SinRespuesta, Epoca, Epoca, null);

        return new ParticipanteResuelto(usuario, campania, participante, pregunta);
    }

    private static Rubrica CrearRubrica()
        => Rubrica.Crear("rub_1", "Rubrica", "desc", "# Rubrica", EscalaRubrica.Crear(1, 5),
            new[] { CriterioRubrica.Crear("claridad", 1m) }, 1, EstadoRubrica.Activa, Epoca, Epoca);

    private static Prompt CrearPrompt()
        => Prompt.Crear("pr_eval", "Prompt", "evaluar", "Eres evaluador.", 1, EstadoPrompt.Activo, "u_admin", Epoca, Epoca, Epoca);

    private static ConfigLlm CrearConfig()
        => ConfigLlm.Crear("llm_1", "Azure", "AzureOpenAI", "gpt-4o-mini", "https://x", "llm-key", null,
            LimitesTokensLlm.Crear(6000, 800), 30, 2, EstadoRegistro.Activo, Epoca, Epoca);

    private static DominioEvaluacion CrearEvaluacion()
        => DominioEvaluacion.Crear(
            "eval_1", "c_1", "resp_1", "u_1", "p_1", "rub_1", 1, "pr_eval", 1, "llm_1",
            new ConfigLlmSnapshot("AzureOpenAI", "gpt-4o-mini", "https://x", new Dictionary<string, object?>()),
            new Dictionary<string, decimal> { ["claridad"] = 1m },
            new[] { CalificacionCriterio.Crear("claridad", 4m, "clara") },
            4m, "explica", "Buena idea", RecomendacionEvaluacion.Cerrar, null,
            new[] { "tema" }, new[] { "ent" }, false, Epoca);

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

            return mensaje is null
                ? null
                : new MensajeEntrante(mensaje.From!, mensaje.Text!.Body!, mensaje.Id!, Epoca);
        }

        public Task<EnvioResultado> EnviarTextoAsync(string numeroE164, string texto, TipoEnvioMensaje tipo, CancellationToken cancellationToken)
        {
            Enviados.Enqueue((numeroE164, texto, tipo));
            return Task.FromResult(EnvioResultado.Ok("wamid.out"));
        }

        public Task<EnvioResultado> EnviarPlantillaAsync(string numeroE164, PlantillaWhatsApp plantilla, IReadOnlyDictionary<string, string> variables, TipoEnvioMensaje tipo, CancellationToken cancellationToken)
            => Task.FromResult(EnvioResultado.Ok("wamid.out"));

        public Task<EnvioResultado> EnviarPlantillaAutenticacionAsync(string numeroE164, PlantillaWhatsApp plantilla, string codigo, TipoEnvioMensaje tipo, CancellationToken cancellationToken)
            => Task.FromResult(EnvioResultado.Ok("wamid.out"));
    }

    private sealed class ResolutorFake : IResolutorParticipante
    {
        private readonly ParticipanteResuelto _participante;

        public ResolutorFake(ParticipanteResuelto participante) => _participante = participante;

        public Task<ResultadoResolucion> ResolverAsync(string numeroCrudo, CancellationToken cancellationToken)
            => Task.FromResult<ResultadoResolucion>(new ResultadoResolucion.Autorizado(_participante));
    }

    private sealed class ConversacionesFake : IRepositorioConversaciones
    {
        private readonly Dictionary<string, DominioConversacion> _conversaciones = new(StringComparer.Ordinal);

        public DominioConversacion? Ultima { get; private set; }

        public Task GuardarConversacionAsync(DominioConversacion conversacion, CancellationToken cancellationToken)
        {
            _conversaciones[conversacion.Id] = conversacion;
            Ultima = conversacion;
            return Task.CompletedTask;
        }

        public Task<DominioConversacion?> ObtenerConversacionAsync(string campaniaId, string conversacionId, CancellationToken cancellationToken)
            => Task.FromResult(
                _conversaciones.TryGetValue(conversacionId, out var conversacion) && conversacion.CampaniaId == campaniaId
                    ? conversacion
                    : null);

        public Task<IReadOnlyCollection<DominioConversacion>> ListarConversacionesAsync(string campaniaId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<DominioConversacion>>(
                _conversaciones.Values.Where(conversacion => conversacion.CampaniaId == campaniaId).ToArray());

        public Task<IReadOnlyCollection<DominioConversacion>> ListarAbiertasInactivasAsync(string campaniaId, DateTimeOffset limite, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<DominioConversacion>>(Array.Empty<DominioConversacion>());

        public Task<IReadOnlyCollection<Mensaje>> ListarMensajesAsync(string campaniaId, string conversacionId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<Mensaje>>(Array.Empty<Mensaje>());

        public Task GuardarMensajeAsync(Mensaje mensaje, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<ConteoBorradoConversaciones> EliminarPorUsuarioAsync(string campaniaId, string? usuarioId, CancellationToken cancellationToken)
        {
            var conversaciones = _conversaciones.Values
                .Where(c => c.CampaniaId == campaniaId && (usuarioId is null || c.UsuarioId == usuarioId))
                .ToArray();
            foreach (var c in conversaciones)
            {
                _conversaciones.Remove(c.Id);
            }

            return Task.FromResult(new ConteoBorradoConversaciones(conversaciones.Length, 0));
        }
    }
}
