using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ElTejido.Application.Auth;
using ElTejido.Application.Campanas;
using ElTejido.Application.Conversacion;
using ElTejido.Application.Participantes;
using ElTejido.Application.Respuestas;
using ElTejido.Domain.Campanas;
using ElTejido.Domain.Common;
using ElTejido.Domain.Conversaciones;
using ElTejido.Domain.Evaluacion;
using ElTejido.Domain.Identidad;
using ElTejido.Domain.Participantes;
using ElTejido.Domain.Respuestas;
using ElTejido.Domain.Usuarios;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using DominioConversacion = ElTejido.Domain.Conversaciones.Conversacion;
using DominioEvaluacion = ElTejido.Domain.Evaluacion.Evaluacion;

namespace ElTejido.IntegrationTests;

/// <summary>
/// P-03 — endpoints admin de reinicio de datos: por participante (cold-start real: borra el flujo,
/// resetea el participante) y masivo (gateado por el flag <c>Seguridad:PermitirReinicioDatos</c>).
/// Corre en modo Memoria con sesion admin fake.
/// </summary>
public sealed class ReinicioDatosIntegrationTests
{
    private const string CookieSesion = "eltejido_sesion";
    private const string CsrfAdmin = "csrf-admin";
    private static readonly DateTimeOffset Epoca = DateTimeOffset.UnixEpoch;

    [Fact]
    public async Task ReiniciarParticipante_BorraElFlujoYReseteaElParticipante()
    {
        using var fabrica = Construir(permitirReinicio: true);
        using var client = CrearClienteConSesion(fabrica);
        await SembrarAsync(fabrica, "c_1", "u_1");

        using var respuesta = await EnviarJsonAsync(
            client, HttpMethod.Post, "/api/admin/campanias/c_1/participantes/u_1/reiniciar", new { reiniciarEnvios = false });

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await respuesta.Content.ReadAsStringAsync();
        json.Should().Contain("\"conversaciones\":1");
        json.Should().Contain("\"respuestas\":1");
        json.Should().Contain("\"participantesReseteados\":1");

        var respuestas = fabrica.Services.GetRequiredService<IRepositorioRespuestas>();
        var conversaciones = fabrica.Services.GetRequiredService<IRepositorioConversaciones>();
        var participantes = fabrica.Services.GetRequiredService<IRepositorioParticipantes>();
        (await respuestas.ListarRespuestasAsync("c_1", CancellationToken.None)).Should().BeEmpty();
        (await conversaciones.ListarConversacionesAsync("c_1", CancellationToken.None)).Should().BeEmpty();
        var participante = await participantes.ObtenerParticipantePorUsuarioAsync("c_1", "u_1", CancellationToken.None);
        participante!.EstadoRespuesta.Should().Be(EstadoRespuestaParticipante.SinRespuesta);
    }

    [Fact]
    public async Task ReiniciarDatosCampania_ConFlagApagado_Responde409()
    {
        using var fabrica = Construir(permitirReinicio: false);
        using var client = CrearClienteConSesion(fabrica);
        await SembrarAsync(fabrica, "c_1", "u_1");

        using var respuesta = await EnviarJsonAsync(
            client, HttpMethod.Post, "/api/admin/campanias/c_1/reiniciar-datos", new { reiniciarEnvios = false });

        respuesta.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ReiniciarParticipante_SinSesion_Responde401()
    {
        using var fabrica = Construir(permitirReinicio: true);
        using var client = fabrica.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/campanias/c_1/participantes/u_1/reiniciar");
        request.Headers.Add("X-CSRF-Token", CsrfAdmin);
        request.Content = JsonContent.Create(new { reiniciarEnvios = false });
        using var respuesta = await client.SendAsync(request);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static async Task SembrarAsync(WebApplicationFactory<Program> fabrica, string campaniaId, string usuarioId)
    {
        var campanias = fabrica.Services.GetRequiredService<IRepositorioCampanias>();
        var participantes = fabrica.Services.GetRequiredService<IRepositorioParticipantes>();
        var conversaciones = fabrica.Services.GetRequiredService<IRepositorioConversaciones>();
        var respuestas = fabrica.Services.GetRequiredService<IRepositorioRespuestas>();

        await campanias.GuardarCampaniaAsync(
            Campania.Crear(
                campaniaId, "Campania", "Desc", "Obj", EstadoCampania.Activa, null,
                new[] { Pregunta.Crear("p_1", "Idea", "Instr", "cat", 1, EstadoRegistro.Activo, null, null, null, 1, LimitesSeguridad.ParaPregunta(1500, 2), ConfigMarkdown.Crear(TipoArtefactoMarkdown.Respuesta)) },
                "r_general", null, "llm_default",
                ConfigMarkdown.Crear(TipoArtefactoMarkdown.Campania), ConfigConversacional.Crear(1, "Gracias"),
                LimitesSeguridad.Crear(1500, 10, 2), null, Epoca, Epoca),
            CancellationToken.None);

        await participantes.GuardarParticipanteAsync(
            ParticipanteCampania.Crear(
                $"part_{usuarioId}", campaniaId, usuarioId, NumeroWhatsApp.FromNormalized("573001112233"),
                EstadoRegistro.Activo, EstadoEnvio.Enviado, EstadoRespuestaParticipante.Respondio, Epoca, Epoca, Epoca),
            CancellationToken.None);

        await conversaciones.GuardarConversacionAsync(
            DominioConversacion.Iniciar("conv_1", campaniaId, usuarioId, "p_1", "whatsapp", null, Epoca), CancellationToken.None);
        await conversaciones.GuardarMensajeAsync(
            Mensaje.Crear("conv_1_m0", campaniaId, "conv_1", DireccionMensaje.In, "hola", null, Epoca), CancellationToken.None);
        await respuestas.GuardarRespuestaAsync(
            Respuesta.Crear("resp_1", campaniaId, usuarioId, "p_1", "conv_1", "Idea", "whatsapp", false, EstadoRespuesta.Recibida, Epoca, null),
            CancellationToken.None);
        await respuestas.GuardarEvaluacionAsync(
            DominioEvaluacion.Crear(
                "eval_1", campaniaId, "resp_1", usuarioId, "p_1", "r_general", 1, "pr_eval", 1, "llm_default",
                new ConfigLlmSnapshot("AzureOpenAI", "gpt-4o-mini", "https://x", new Dictionary<string, object?>()),
                null, null, 3m, "ok", "Bien", RecomendacionEvaluacion.Cerrar, null, null, null, false, Epoca),
            CancellationToken.None);
    }

    private static WebApplicationFactory<Program> Construir(bool permitirReinicio)
        => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("Persistencia:Modo", "Memoria");
            builder.UseSetting("Seguridad:PermitirReinicioDatos", permitirReinicio ? "true" : "false");
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IServicioSesion, SesionesFake>();
            });
        });

    private static HttpClient CrearClienteConSesion(WebApplicationFactory<Program> fabrica)
    {
        var client = fabrica.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", $"{CookieSesion}=token-admin");
        return client;
    }

    private static Task<HttpResponseMessage> EnviarJsonAsync<T>(HttpClient client, HttpMethod method, string path, T body)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-CSRF-Token", CsrfAdmin);
        request.Content = JsonContent.Create(body);
        return client.SendAsync(request);
    }

    private sealed class SesionesFake : IServicioSesion
    {
        public Task<SesionEmitida> EmitirAsync(Usuario usuario, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<PrincipalSesion?> ValidarAsync(string token, CancellationToken cancellationToken)
            => Task.FromResult<PrincipalSesion?>(token == "token-admin"
                ? new PrincipalSesion("u_admin", "Admin", RolUsuario.Admin, CsrfAdmin, DateTimeOffset.UtcNow.AddMinutes(30))
                : null);
    }
}
