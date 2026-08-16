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

    [Fact]
    public async Task DTQA01_MensajeInyectado_RecorreLaMismaColaYOfreceLaPreguntaInicial()
    {
        var gateway = new GatewayDePrueba();
        var conversaciones = new ConversacionesFake();

        using var fabrica = Construir(gateway, conversaciones);
        using var client = fabrica.CreateClient();

        using var respuesta = await client.PostAsJsonAsync(
            "/diagnostico/simulacion/webhook-entrante",
            new
            {
                numero = Numero,
                texto = "Hola",
                whatsappMessageId = "wamid.DTQA.1",
            });

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        await EsperarAsync(() => gateway.Enviados.Count >= 1);

        gateway.Enviados.Should().ContainSingle();
        gateway.Enviados.Single().Tipo.Should().Be(TipoEnvioMensaje.Inicial);
    }

    [Fact]
    public async Task P31_Simulacion_AporteSobreUmbralMuestraResumenYLaMejoraNoLoRepite()
    {
        var gateway = new GatewayDePrueba();
        var conversaciones = new ConversacionesFake();
        var respuestas = new RespuestasFake();
        var contextos = new System.Collections.Concurrent.ConcurrentQueue<ContextoEvaluacion>();
        var compilaciones = new System.Collections.Concurrent.ConcurrentQueue<SolicitudCompilacion>();
        var logs = Substitute.For<IRepositorioLogSeguridad>();

        using var fabrica = Construir(
            gateway,
            conversaciones,
            respuestas,
            contextos,
            compilaciones,
            confirmacionExplicitaIdeas: false,
            logSeguridad: logs,
            resumenConsolidacion: true,
            evaluacion: CrearEvaluacion(
                recomendacion: RecomendacionEvaluacion.Repreguntar,
                repregunta: "Que resultado esperas?",
                calificacionTotal: 3m));
        using var client = fabrica.CreateClient();

        await EnviarSimulacionAsync(client, "wamid.P31.1", "Hola");
        await EsperarAsync(() => gateway.Enviados.Any(envio => envio.Tipo == TipoEnvioMensaje.Inicial));

        const string aporte = "Una ruta clara para que los usuarios reciban respuesta a sus solicitudes";
        await EnviarSimulacionAsync(client, "wamid.P31.2", aporte);
        await EsperarAsync(() => gateway.Enviados.Any(envio => envio.Tipo == TipoEnvioMensaje.Repregunta));

        gateway.Enviados.Last(envio => envio.Tipo == TipoEnvioMensaje.Repregunta).Texto.Should().Contain(aporte);
        respuestas.Ideas.Values.Should().ContainSingle().Which.ResumenEnviadoEn.Should().NotBeNull();
        await logs.Received().RegistrarAsync(
            Arg.Is<LogSeguridad>(log => log.TipoEvento == TipoEventoSeguridad.ResumenConsolidacion
                && log.Resultado == "enviado"
                && !log.Detalle!.Contains(aporte, StringComparison.Ordinal)),
            Arg.Any<CancellationToken>());

        await EnviarSimulacionAsync(client, "wamid.P31.3", "Agrego responsables y un plazo para cada respuesta");
        await EsperarAsync(() => contextos.Count >= 2);

        gateway.Enviados.Count(envio => envio.Tipo == TipoEnvioMensaje.Repregunta && envio.Texto.Contains("Asi va tu idea", StringComparison.Ordinal))
            .Should().Be(1);
        await logs.Received().RegistrarAsync(
            Arg.Is<LogSeguridad>(log => log.TipoEvento == TipoEventoSeguridad.ResumenConsolidacion
                && log.Resultado == "omitidoYaEnviado"),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// I-19 §13: recorrido completo de una idea por el webhook real — aporte → propuesta →
    /// confirmación → evaluación de la versión consolidada → cierre madura con curaduría pendiente y su
    /// Markdown canónico. Comprueba explícitamente que **el último mensaje aislado no es el texto
    /// evaluado** (§13.5) y que nada se evalúa antes de confirmar (§13.1).
    /// </summary>
    [Fact]
    public async Task I19_CicloCompleto_ConfirmaAntesDeEvaluarYCierraLaIdeaMadura()
    {
        var gateway = new GatewayDePrueba();
        var conversaciones = new ConversacionesFake();
        var respuestas = new RespuestasFake();
        var contextos = new System.Collections.Concurrent.ConcurrentQueue<ContextoEvaluacion>();
        var compilaciones = new System.Collections.Concurrent.ConcurrentQueue<SolicitudCompilacion>();

        using var fabrica = Construir(gateway, conversaciones, respuestas, contextos, compilaciones);
        using var client = fabrica.CreateClient();

        await EnviarEntranteAsync(client, "wamid.I19.1", "Hola");
        await EsperarAsync(() => gateway.Enviados.Count >= 1);

        await EnviarEntranteAsync(client, "wamid.I19.2", "Mi idea es reducir el desperdicio en bodega");
        await EsperarAsync(() => gateway.Enviados.Any(e => e.Texto.Contains("¿Es correcto?", StringComparison.Ordinal)));

        // §13.1: la propuesta se pide confirmar y nada se evaluó todavía.
        contextos.Should().BeEmpty();
        respuestas.Ideas.Values.Should().ContainSingle()
            .Which.EstadoFlujo.Should().Be(EstadoFlujoIdeaConsolidada.PendienteConfirmacion);

        await EnviarEntranteAsync(client, "wamid.I19.3", "si");
        await EsperarAsync(() => respuestas.Ideas.Values.Any(idea => idea.EstadoFlujo == EstadoFlujoIdeaConsolidada.Cerrada));

        // §13.5: lo evaluado es la versión consolidada, no el "si" suelto.
        contextos.Should().ContainSingle();
        contextos.Single().RespuestaTexto.Should().Contain("desperdicio en bodega").And.NotBe("si");
        contextos.Single().IdeaId.Should().NotBeNull();

        // §13.2: superar el umbral deja la idea madura y pendiente de curaduría.
        var idea = respuestas.Ideas.Values.Single();
        idea.EstadoResultado.Should().Be(EstadoResultadoIdeaConsolidada.Madura);
        idea.EstadoCuraduria.Should().Be(EstadoCuraduriaIdea.Pendiente);
        idea.NivelMadurez.Should().Be(NivelMadurez.Maduro);

        // §13.1: la evaluación referencia la idea y la versión exacta.
        var evaluacion = respuestas.Evaluaciones.Single();
        evaluacion.IdeaId.Should().Be(idea.Id);
        evaluacion.VersionIdeaId.Should().Be(idea.VersionConfirmadaRef);

        // §13.4: el Markdown canónico se compila por idea, no por aporte.
        compilaciones.Should().Contain(solicitud =>
            solicitud.Tipo == TipoArtefactoMarkdown.Idea && solicitud.IdeaId == idea.Id);
        gateway.Enviados.Should().Contain(enviado => enviado.Tipo == TipoEnvioMensaje.Cierre);
    }

    [Fact]
    public async Task P30_IntencionListaSeleccionReaperturaYReevaluacion_ConservaIdeaId()
    {
        var gateway = new GatewayDePrueba();
        var conversaciones = new ConversacionesFake();
        var respuestas = new RespuestasFake();
        var contextos = new System.Collections.Concurrent.ConcurrentQueue<ContextoEvaluacion>();
        var compilaciones = new System.Collections.Concurrent.ConcurrentQueue<SolicitudCompilacion>();
        await SembrarIdeaHistoricaAsync(
            conversaciones, respuestas, "idea_reciente", "conv_reciente", "Reducir desperdicio reciente", Epoca.AddDays(-1));
        await SembrarIdeaHistoricaAsync(
            conversaciones, respuestas, "idea_antigua", "conv_antigua", "Automatizar la atención antigua", Epoca.AddDays(-2));

        using var fabrica = Construir(
            gateway,
            conversaciones,
            respuestas,
            contextos,
            compilaciones,
            confirmacionExplicitaIdeas: false,
            retomarIdeas: true);
        using var client = fabrica.CreateClient();

        await EnviarEntranteAsync(client, "wamid.P30.1", "quiero retomar una idea");
        await EsperarAsync(() => gateway.Enviados.Any(
            envio => envio.Texto.Contains("Automatizar la atención antigua", StringComparison.Ordinal)));
        contextos.Should().BeEmpty("la intención y el menú no son aportes evaluables");

        await EnviarEntranteAsync(client, "wamid.P30.2", "2");
        await EsperarAsync(() => respuestas.Ideas["idea_antigua"].EstadoFlujo == EstadoFlujoIdeaConsolidada.EnRevision);
        respuestas.Ideas["idea_antigua"].Id.Should().Be("idea_antigua");
        conversaciones.Conversaciones.Single(c => c.Id == "conv_antigua").Estado.Should().Be(EstadoConversacion.Abierta);

        await EnviarEntranteAsync(client, "wamid.P30.3", "Agregar medición mensual y un responsable.");
        await EsperarAsync(() => contextos.Any(contexto => contexto.IdeaId == "idea_antigua"));

        var idsEvaluados = string.Join(",", contextos.Select(contexto => contexto.IdeaId ?? "null"));
        var reevaluacion = contextos.Should()
            .ContainSingle(contexto => contexto.IdeaId == "idea_antigua", $"ids observados: {idsEvaluados}").Which;
        reevaluacion.RespuestaTexto.Should().NotBe("2").And.NotBe("quiero retomar una idea");
        respuestas.Ideas["idea_antigua"].VersionConfirmadaRef.Should().NotBeNull();
        respuestas.Ideas.Should().HaveCount(2, "retomar no crea una tercera idea");
    }

    /// <summary>
    /// I-20 §8.5: el recorrido real webhook → worker → orquestador usa al redactor inyectado, conserva
    /// la propuesta completa en el medio y no concatena el respaldo histórico de confirmación.
    /// </summary>
    [Fact]
    public async Task I20_CicloCompleto_UsaRedactorEnConfirmacionSinCambiarLaVersionEvaluada()
    {
        var gateway = new GatewayDePrueba();
        var conversaciones = new ConversacionesFake();
        var respuestas = new RespuestasFake();
        var contextos = new System.Collections.Concurrent.ConcurrentQueue<ContextoEvaluacion>();
        var compilaciones = new System.Collections.Concurrent.ConcurrentQueue<SolicitudCompilacion>();
        var redactor = Substitute.For<IRedactorTurnoConversacional>();
        redactor.RedactarAsync(Arg.Any<ContextoRedaccionTurno>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoRedaccionTurno.Exito(
                "Recojo esta propuesta sobre la bodega.",
                "¿Refleja lo que quieres plantear?",
                UsoTokensLlm.Crear(7, 5)));

        using var fabrica = Construir(gateway, conversaciones, respuestas, contextos, compilaciones, redactor);
        using var client = fabrica.CreateClient();

        await EnviarEntranteAsync(client, "wamid.I20.1", "Hola");
        await EsperarAsync(() => gateway.Enviados.Count >= 1);
        await EnviarEntranteAsync(client, "wamid.I20.2", "Mi idea es reducir el desperdicio en bodega");
        await EsperarAsync(() => gateway.Enviados.Any(e => e.Texto.Contains("Recojo esta propuesta", StringComparison.Ordinal)));

        var confirmacion = gateway.Enviados.Single(e => e.Texto.Contains("Recojo esta propuesta", StringComparison.Ordinal));
        confirmacion.Texto.Should().Contain("Recojo esta propuesta sobre la bodega.")
            .And.Contain("Mi idea es reducir el desperdicio en bodega")
            .And.Contain("¿Refleja lo que quieres plantear?")
            .And.NotContain("Entendí que propones")
            .And.NotContain("¿Es correcto?");

        await EnviarEntranteAsync(client, "wamid.I20.3", "sí");
        await EsperarAsync(() => respuestas.Ideas.Values.Any(idea => idea.EstadoFlujo == EstadoFlujoIdeaConsolidada.Cerrada));

        // La voz cambia; la decisión y el texto evaluado siguen siendo los de I-19.
        contextos.Should().ContainSingle();
        contextos.Single().RespuestaTexto.Should().Contain("desperdicio en bodega").And.NotBe("sí");
        await redactor.Received().RedactarAsync(
            Arg.Is<ContextoRedaccionTurno>(contexto => contexto.Acto == ActoConversacional.Confirmar),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// P-24: el recorrido real de webhook no convierte una petición breve de ayuda en un aporte. Confirma
    /// implícitamente la propuesta, evalúa la versión completa y conserva la trazabilidad canónica.
    /// </summary>
    [Fact]
    public async Task P24_SolicitarMejora_EvaluaLaPropuestaCompletaSinPersistirLaFraseComoRespuesta()
    {
        var gateway = new GatewayDePrueba();
        var conversaciones = new ConversacionesFake();
        var respuestas = new RespuestasFake();
        var contextos = new System.Collections.Concurrent.ConcurrentQueue<ContextoEvaluacion>();
        var compilaciones = new System.Collections.Concurrent.ConcurrentQueue<SolicitudCompilacion>();

        using var fabrica = Construir(gateway, conversaciones, respuestas, contextos, compilaciones);
        using var client = fabrica.CreateClient();

        await EnviarEntranteAsync(client, "wamid.P24.1", "Hola");
        await EsperarAsync(() => gateway.Enviados.Count >= 1);
        await EnviarEntranteAsync(client, "wamid.P24.2", "Mi idea es reducir el desperdicio en bodega");
        await EsperarAsync(() => gateway.Enviados.Any(e => e.Texto.Contains("¿Es correcto?", StringComparison.Ordinal)));

        await EnviarEntranteAsync(client, "wamid.P24.3", "Vamos a mejorarla");
        await EsperarAsync(() => respuestas.Ideas.Values.Any(idea => idea.EstadoFlujo == EstadoFlujoIdeaConsolidada.Cerrada));

        contextos.Should().ContainSingle();
        contextos.Single().RespuestaTexto.Should().Contain("desperdicio en bodega").And.NotBe("Vamos a mejorarla");
        (await respuestas.ListarRespuestasAsync("c_1", CancellationToken.None))
            .Should().ContainSingle(respuesta => respuesta.Texto.Contains("desperdicio en bodega", StringComparison.Ordinal));
    }

    /// <summary>
    /// P-25: la configuración operativa evalúa el aporte sustantivo en su mismo turno y elimina la
    /// confirmación repetitiva del recorrido webhook real.
    /// </summary>
    [Fact]
    public async Task P25_CicloCompleto_EvaluaSinPedirConfirmacionExplicita()
    {
        var gateway = new GatewayDePrueba();
        var conversaciones = new ConversacionesFake();
        var respuestas = new RespuestasFake();
        var contextos = new System.Collections.Concurrent.ConcurrentQueue<ContextoEvaluacion>();
        var compilaciones = new System.Collections.Concurrent.ConcurrentQueue<SolicitudCompilacion>();

        using var fabrica = Construir(
            gateway, conversaciones, respuestas, contextos, compilaciones,
            confirmacionExplicitaIdeas: false);
        using var client = fabrica.CreateClient();

        await EnviarEntranteAsync(client, "wamid.P25.1", "Hola");
        await EsperarAsync(() => gateway.Enviados.Count >= 1);
        await EnviarEntranteAsync(
            client,
            "wamid.P25.2",
            "Hagamos una presentación en PowerPoint con casos reales y una demostración");
        await EsperarAsync(() =>
            respuestas.Ideas.Values.Any(idea => idea.EstadoFlujo == EstadoFlujoIdeaConsolidada.Cerrada));

        contextos.Should().ContainSingle();
        contextos.Single().RespuestaTexto.Should()
            .Contain("presentación en PowerPoint con casos reales y una demostración");
        gateway.Enviados.Should().NotContain(enviado =>
            enviado.Texto.Contains("¿Es correcto?", StringComparison.Ordinal)
            || enviado.Texto.Contains("Entendí que propones", StringComparison.OrdinalIgnoreCase));
        respuestas.Ideas.Values.Should().ContainSingle().Which.VersionConfirmadaRef.Should().NotBeNull();
    }

    [Fact]
    public async Task P27_WebhookCoaching_ClasificaSalidaFlexibleYCierraSinEvaluarlaComoAporte()
    {
        var gateway = new GatewayDePrueba();
        var conversaciones = new ConversacionesFake();
        var respuestas = new RespuestasFake();
        var contextos = new System.Collections.Concurrent.ConcurrentQueue<ContextoEvaluacion>();
        var compilaciones = new System.Collections.Concurrent.ConcurrentQueue<SolicitudCompilacion>();
        var clasificador = Substitute.For<IClasificadorIntencionControl>();
        var logs = Substitute.For<IRepositorioLogSeguridad>();
        clasificador.ClasificarAsync(Arg.Any<ContextoClasificacionIntencionControl>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoClasificacionIntencionControl.Exito(IntencionControl.FinalizarParticipacion, UsoTokensLlm.Crear(12, 5)));

        using var fabrica = Construir(
            gateway,
            conversaciones,
            respuestas,
            contextos,
            compilaciones,
            confirmacionExplicitaIdeas: false,
            clasificador: clasificador,
            logSeguridad: logs,
            clasificacionIntencionControl: true,
            evaluacion: CrearEvaluacion(RecomendacionEvaluacion.Repreguntar, "¿Qué cambiarías?", calificacionTotal: 1m));
        using var client = fabrica.CreateClient();

        await EnviarEntranteAsync(client, "wamid.P27.1", "Hola");
        await EsperarAsync(() => gateway.Enviados.Count >= 1);
        await EnviarEntranteAsync(client, "wamid.P27.2", "Mi idea reduce desperdicio en bodega");
        await EsperarAsync(() => conversaciones.Ultima?.EstadoMaquina == EstadoMaquinaConversacion.EsperandoRepregunta);
        conversaciones.Ultima!.EstadoMaquina.Should().Be(EstadoMaquinaConversacion.EsperandoRepregunta);
        await EnviarEntranteAsync(client, "wamid.P27.3", "I think I should stop for today");
        await EsperarAsync(() => conversaciones.Ultima?.Estado == EstadoConversacion.Cerrada);

        await clasificador.Received(1).ClasificarAsync(
            Arg.Any<ContextoClasificacionIntencionControl>(), Arg.Any<CancellationToken>());
        contextos.Should().ContainSingle();
        gateway.Enviados.Should().Contain(enviado => enviado.Tipo == TipoEnvioMensaje.Cierre);
        await logs.Received(1).RegistrarAsync(
            Arg.Is<LogSeguridad>(log => log.TipoEvento == TipoEventoSeguridad.ClasificacionIntencionControl
                && log.Resultado == "clasificada"
                && log.CampaniaId == "c_1"
                && log.EsLlamadaLlm
                && log.PromptTokens == 12
                && log.CompletionTokens == 5
                && !log.Detalle!.Contains("I think I should stop for today", StringComparison.Ordinal)),
            Arg.Any<CancellationToken>());
    }

    private static async Task SembrarIdeaHistoricaAsync(
        ConversacionesFake conversaciones,
        RespuestasFake respuestas,
        string ideaId,
        string conversacionId,
        string texto,
        DateTimeOffset fecha)
    {
        var versionId = ideaId + "_v1";
        var version = VersionIdeaConsolidada.Crear(
            versionId,
            "c_1",
            ideaId,
            1,
            null,
            texto,
            [ideaId + "_aporte"],
            [ideaId + "_aporte"],
            TipoAporteIdea.Inicial,
            EstadoConfirmacionVersionIdea.Confirmada,
            null,
            null,
            null,
            null,
            fecha.AddMinutes(-2),
            fecha.AddMinutes(-1));
        var idea = IdeaConsolidada
            .Crear(ideaId, "c_1", "u_1", "p_1", conversacionId, ideaId + "_resp", 1, fecha.AddMinutes(-3))
            .ConfirmarVersion(versionId, fecha.AddMinutes(-1))
            .Cerrar(EstadoResultadoIdeaConsolidada.Pendiente, null, "participante", fecha);
        var conversacion = DominioConversacion
            .Iniciar(conversacionId, "c_1", "u_1", "p_1", "whatsapp", null, fecha.AddMinutes(-3))
            .Cerrar(fecha);
        await respuestas.GuardarVersionIdeaAsync(version, CancellationToken.None);
        await respuestas.GuardarIdeaConsolidadaAsync(idea, CancellationToken.None);
        await conversaciones.GuardarConversacionAsync(conversacion, CancellationToken.None);
    }

    private static async Task EnviarEntranteAsync(HttpClient client, string wamid, string texto)
    {
        var cuerpo =
            $"{{\"entry\":[{{\"changes\":[{{\"value\":{{\"messages\":[{{\"from\":\"{Numero}\",\"id\":\"{wamid}\",\"timestamp\":\"1700000000\",\"type\":\"text\",\"text\":{{\"body\":\"{texto}\"}}}}]}}}}]}}]}}";
        using var contenido = new StringContent(cuerpo, System.Text.Encoding.UTF8, "application/json");
        contenido.Headers.Add("X-Hub-Signature-256", "sha256=ignorada-en-prueba");
        using var respuesta = await client.PostAsync("/webhook/whatsapp", contenido);
        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static async Task EnviarSimulacionAsync(HttpClient client, string wamid, string texto)
    {
        using var respuesta = await client.PostAsJsonAsync(
            "/diagnostico/simulacion/webhook-entrante",
            new { numero = Numero, texto, whatsappMessageId = wamid });
        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static WebApplicationFactory<Program> Construir(
        GatewayDePrueba gateway,
        ConversacionesFake conversaciones,
        RespuestasFake respuestas,
        System.Collections.Concurrent.ConcurrentQueue<ContextoEvaluacion> contextos,
        System.Collections.Concurrent.ConcurrentQueue<SolicitudCompilacion> compilaciones,
        IRedactorTurnoConversacional? redactor = null,
        bool confirmacionExplicitaIdeas = true,
        IClasificadorIntencionControl? clasificador = null,
        IRepositorioLogSeguridad? logSeguridad = null,
        bool clasificacionIntencionControl = false,
        bool retomarIdeas = false,
        DominioEvaluacion? evaluacion = null,
        bool resumenConsolidacion = false)
    {
        var dedupe = Substitute.For<IRegistroWebhookDedupe>();
        dedupe.IntentarRegistrarMensajeAsync(Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(true);
        var resolutor = new ResolutorFake(CrearParticipante(clasificacionIntencionControl));

        var configuracion = Substitute.For<IRepositorioConfiguracion>();
        configuracion.ObtenerUltimaRubricaAsync("rub_1", Arg.Any<CancellationToken>()).Returns(CrearRubrica());
        configuracion.ObtenerPromptVigenteAsync("pr_eval", Arg.Any<CancellationToken>())
            .Returns(ResolutorPromptRuntime.Resolver([CrearPrompt()]));
        configuracion.ObtenerConfigLlmAsync("llm_1", Arg.Any<CancellationToken>()).Returns(CrearConfig());

        var evaluador = Substitute.For<IEvaluadorLlm>();
        evaluador.EvaluarAsync(Arg.Do<ContextoEvaluacion>(contextos.Enqueue), Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Exito(evaluacion ?? CrearEvaluacion()));

        // Consolidador determinista: acumula sin inventar, como haría el LLM en el camino feliz.
        var consolidador = Substitute.For<IConsolidadorIdeas>();
        consolidador.ConsolidarAsync(Arg.Any<ContextoConsolidacionIdeas>(), Arg.Any<CancellationToken>())
            .Returns(llamada =>
            {
                var contexto = llamada.Arg<ContextoConsolidacionIdeas>();
                var texto = string.IsNullOrWhiteSpace(contexto.TextoConfirmadoAnterior)
                    ? contexto.NuevoAporte
                    : $"{contexto.TextoConfirmadoAnterior} {contexto.NuevoAporte}";
                return new ResultadoConsolidacionIdeas.Exito(texto, TipoAporteIdea.Inicial, [], false, null, false, null);
            });

        // El compilador solo se observa: el artefacto real se prueba en CompiladorMarkdownTests.
        var compilador = Substitute.For<ICompiladorMarkdown>();
        compilador
            .CompilarAsync(Arg.Do<SolicitudCompilacion>(compilaciones.Enqueue), Arg.Any<CancellationToken>())
            .ReturnsForAnyArgs(Task.FromException<ArtefactoMarkdown>(new ErrorNoEncontrado("sin blob en la prueba")));

        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration(config =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Secretos:wa-appsec"] = AppSecret,
                    ["Conversacion:ConfirmacionExplicitaIdeasHabilitada"] =
                        confirmacionExplicitaIdeas.ToString(),
                    ["Conversacion:ClasificacionIntencionControl"] = clasificacionIntencionControl.ToString(),
                    ["Conversacion:RetomarIdeasHabilitado"] = retomarIdeas.ToString(),
                    ["Conversacion:ResumenConsolidacionHabilitado"] = resumenConsolidacion.ToString(),
                    ["Conversacion:UmbralResumenConsolidacion"] = "0.4",
                }));

            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IWhatsAppGateway>(gateway);
                services.AddSingleton(dedupe);
                services.AddSingleton<IResolutorParticipante>(resolutor);
                services.AddSingleton<IRepositorioConversaciones>(conversaciones);
                services.AddSingleton(configuracion);
                services.AddSingleton(evaluador);
                services.AddSingleton(consolidador);
                services.AddSingleton<IRepositorioRespuestas>(respuestas);
                services.AddSingleton(Substitute.For<IRepositorioParticipantes>());
                services.AddSingleton(compilador);
                services.AddSingleton(logSeguridad ?? Substitute.For<IRepositorioLogSeguridad>());
                services.AddSingleton(Substitute.For<IProveedorCorrelacion>());
                if (redactor is not null)
                {
                    services.AddSingleton(redactor);
                }
                if (clasificador is not null)
                {
                    services.AddSingleton(clasificador);
                }
                services.AddScoped<IOrquestadorConversacion, OrquestadorConversacion>();
                services.AddScoped<ProcesadorWebhookEntrante>();
            });
        });
    }

    private static WebApplicationFactory<Program> Construir(GatewayDePrueba gateway, ConversacionesFake conversaciones)
    {
        var dedupe = Substitute.For<IRegistroWebhookDedupe>();
        dedupe.IntentarRegistrarMensajeAsync(Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var resolutor = new ResolutorFake(CrearParticipante());

        var configuracion = Substitute.For<IRepositorioConfiguracion>();
        configuracion.ObtenerUltimaRubricaAsync("rub_1", Arg.Any<CancellationToken>()).Returns(CrearRubrica());
        configuracion.ObtenerPromptVigenteAsync("pr_eval", Arg.Any<CancellationToken>())
            .Returns(ResolutorPromptRuntime.Resolver([CrearPrompt()]));
        configuracion.ObtenerConfigLlmAsync("llm_1", Arg.Any<CancellationToken>()).Returns(CrearConfig());

        var evaluador = Substitute.For<IEvaluadorLlm>();
        evaluador.EvaluarAsync(Arg.Any<ContextoEvaluacion>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoEvaluacion.Exito(CrearEvaluacion()));

        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration(config =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Secretos:wa-appsec"] = AppSecret,
                    // Este recorrido conserva explícitamente el contrato histórico; I-19 tiene sus
                    // propias pruebas de confirmación antes de sustituir este escenario E2E.
                    ["Conversacion:ConsolidacionProgresivaHabilitada"] = "false",
                    ["Conversacion:ConfirmacionExplicitaIdeasHabilitada"] = "true",
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

    private static ParticipanteResuelto CrearParticipante(bool clasificacionIntencionControl = false)
    {
        var pregunta = Pregunta.Crear(
            "p_1", "Idea para ingresos", "Se concreto", "ingresos", 1, EstadoRegistro.Activo,
            null, null, null, 1, LimitesSeguridad.ParaPregunta(1500, 2), ConfigMarkdown.Crear(TipoArtefactoMarkdown.Respuesta));

        var campania = Campania.Crear(
            "c_1", "Campania", "Descripcion", "Objetivo", EstadoCampania.Activa, null, new[] { pregunta },
            "rub_1", new Dictionary<string, string> { ["evaluar"] = "pr_eval" }, "llm_1",
            ConfigMarkdown.Crear(TipoArtefactoMarkdown.Respuesta),
            ConfigConversacional.Crear(1, "Gracias por participar.", clasificacionIntencionControl: clasificacionIntencionControl),
            LimitesSeguridad.Crear(1500, 10, 2), null, Epoca, Epoca);

        var usuario = Usuario.Crear(
            "u_1", 1, "Ana", NumeroWhatsApp.FromNormalized(Numero), RolUsuario.Participante, EstadoRegistro.Activo,
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

    private static DominioEvaluacion CrearEvaluacion(
        RecomendacionEvaluacion recomendacion = RecomendacionEvaluacion.Cerrar,
        string? repregunta = null,
        decimal calificacionTotal = 4m)
        => DominioEvaluacion.Crear(
            "eval_1", "c_1", "resp_1", "u_1", "p_1", "rub_1", 1, "pr_eval", 1, "llm_1",
            new ConfigLlmSnapshot("AzureOpenAI", "gpt-4o-mini", "https://x", new Dictionary<string, object?>()),
            new Dictionary<string, decimal> { ["claridad"] = 1m },
            new[] { CalificacionCriterio.Crear("claridad", calificacionTotal, "clara") },
            calificacionTotal, "explica", "Buena idea", recomendacion, repregunta,
            new[] { "tema" }, new[] { "ent" }, false, Epoca);

    /// <summary>Repositorio en memoria mínimo para el recorrido I-19 (ideas, versiones y evaluaciones).</summary>
    private sealed class RespuestasFake : IRepositorioRespuestas
    {
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, Respuesta> _respuestas = new(StringComparer.Ordinal);
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, VersionIdeaConsolidada> _versiones = new(StringComparer.Ordinal);

        public System.Collections.Concurrent.ConcurrentDictionary<string, IdeaConsolidada> Ideas { get; } = new(StringComparer.Ordinal);

        public System.Collections.Concurrent.ConcurrentBag<DominioEvaluacion> Evaluaciones { get; } = new();

        public Task GuardarRespuestaAsync(Respuesta respuesta, CancellationToken cancellationToken)
        {
            _respuestas[respuesta.Id] = respuesta;
            return Task.CompletedTask;
        }

        public Task<Respuesta?> ObtenerRespuestaAsync(string campaniaId, string respuestaId, CancellationToken cancellationToken)
            => Task.FromResult(_respuestas.GetValueOrDefault(respuestaId));

        public Task GuardarIdeaConsolidadaAsync(IdeaConsolidada idea, CancellationToken cancellationToken)
        {
            Ideas[idea.Id] = idea;
            return Task.CompletedTask;
        }

        public Task<IdeaConsolidada?> ObtenerIdeaConsolidadaAsync(string campaniaId, string ideaId, CancellationToken cancellationToken)
            => Task.FromResult(Ideas.GetValueOrDefault(ideaId));

        public Task<IReadOnlyCollection<IdeaConsolidada>> ListarIdeasConsolidadasAsync(string campaniaId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<IdeaConsolidada>>(Ideas.Values.ToArray());

        public Task GuardarVersionIdeaAsync(VersionIdeaConsolidada version, CancellationToken cancellationToken)
        {
            _versiones[version.Id] = version;
            return Task.CompletedTask;
        }

        public Task<VersionIdeaConsolidada?> ObtenerVersionIdeaAsync(string campaniaId, string versionId, CancellationToken cancellationToken)
            => Task.FromResult(_versiones.GetValueOrDefault(versionId));

        public Task<IReadOnlyCollection<VersionIdeaConsolidada>> ListarVersionesIdeaAsync(string campaniaId, string ideaId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<VersionIdeaConsolidada>>(
                _versiones.Values.Where(version => version.IdeaId == ideaId).ToArray());

        public Task GuardarEvaluacionAsync(DominioEvaluacion evaluacion, CancellationToken cancellationToken)
        {
            Evaluaciones.Add(evaluacion);
            return Task.CompletedTask;
        }

        public Task<DominioEvaluacion?> ObtenerEvaluacionPorRespuestaAsync(string campaniaId, string respuestaId, CancellationToken cancellationToken)
            => Task.FromResult(Evaluaciones.LastOrDefault(evaluacion => evaluacion.RespuestaId == respuestaId));

        public Task<DominioEvaluacion?> ObtenerEvaluacionPorIdAsync(string campaniaId, string evaluacionId, CancellationToken cancellationToken)
            => Task.FromResult(Evaluaciones.FirstOrDefault(evaluacion => evaluacion.Id == evaluacionId));

        public Task<IReadOnlyCollection<DominioEvaluacion>> ListarEvaluacionesAsync(string campaniaId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<DominioEvaluacion>>(Evaluaciones
                .Where(evaluacion => evaluacion.CampaniaId == campaniaId)
                .OrderByDescending(evaluacion => evaluacion.Fecha)
                .ToArray());

        public Task<IReadOnlyCollection<Respuesta>> ListarRespuestasAsync(string campaniaId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<Respuesta>>(_respuestas.Values.ToArray());

        public Task<int> ContarEvaluacionesUsuarioAsync(string campaniaId, string usuarioId, CancellationToken cancellationToken)
            => Task.FromResult(Evaluaciones.Count);

        public Task<long> SumarTokensCampaniaAsync(string campaniaId, CancellationToken cancellationToken)
            => Task.FromResult(0L);

        public Task GuardarArtefactoAsync(ArtefactoMarkdown artefacto, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<ArtefactoMarkdown?> ObtenerArtefactoAsync(string campaniaId, string artefactoId, CancellationToken cancellationToken)
            => Task.FromResult<ArtefactoMarkdown?>(null);

        public Task<IReadOnlyCollection<ArtefactoMarkdown>> ListarArtefactosAsync(string campaniaId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<ArtefactoMarkdown>>(Array.Empty<ArtefactoMarkdown>());

        public Task<ConteoBorradoRespuestas> EliminarPorUsuarioAsync(string campaniaId, string? usuarioId, CancellationToken cancellationToken)
            => Task.FromResult(new ConteoBorradoRespuestas(0, 0, 0, Array.Empty<string>()));
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

            return mensaje is null
                ? null
                : new MensajeEntrante(mensaje.From!, mensaje.Text!.Body!, mensaje.Id!, Epoca);
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

    private sealed class ResolutorFake : IResolutorParticipante
    {
        private readonly ParticipanteResuelto _participante;

        public ResolutorFake(ParticipanteResuelto participante) => _participante = participante;

        public Task<ResultadoResolucion> ResolverAsync(string numeroCrudo, CancellationToken cancellationToken)
            => Task.FromResult<ResultadoResolucion>(new ResultadoResolucion.Autorizado(_participante));

        public Task<ResultadoCandidatos> ResolverCandidatosAsync(string numeroCrudo, CancellationToken cancellationToken)
            => Task.FromResult<ResultadoCandidatos>(new ResultadoCandidatos.Autorizado(
                _participante.Usuario,
                new[]
                {
                    new CandidatoCampania(
                        _participante.Participante,
                        _participante.Campania,
                        _participante.PreguntaVigente),
                }));
    }

    private sealed class ConversacionesFake : IRepositorioConversaciones
    {
        private readonly Dictionary<string, DominioConversacion> _conversaciones = new(StringComparer.Ordinal);

        public DominioConversacion? Ultima { get; private set; }

        public IReadOnlyCollection<DominioConversacion> Conversaciones => _conversaciones.Values.ToArray();

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
