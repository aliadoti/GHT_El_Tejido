using System.Net;
using System.Net.Http.Json;
using ElTejido.Application.Auth;
using ElTejido.Application.Campanas;
using ElTejido.Application.Participantes;
using ElTejido.Application.Usuarios;
using ElTejido.Application.WhatsApp;
using ElTejido.Domain.Campanas;
using ElTejido.Domain.Common;
using ElTejido.Domain.Identidad;
using ElTejido.Domain.Participantes;
using ElTejido.Domain.Usuarios;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace ElTejido.IntegrationTests;

/// <summary>
/// Verifica el envio masivo de mensajes iniciales (04 §5.4): la campania debe estar activa (409 si
/// no) y el disparo responde 202 con el job. Repositorios mockeados; las colas/jobs in-process se
/// registran siempre (sin Cosmos).
/// </summary>
public sealed class EnviosIntegrationTests
{
    private const string CookieSesion = "eltejido_sesion";
    private const string CsrfAdmin = "csrf-admin";
    private const string CampaniaId = "c_1";
    private const string Numero = "573001112233";
    private static readonly DateTimeOffset Epoca = DateTimeOffset.UnixEpoch;

    [Fact]
    public async Task Envios_CampaniaNoActiva_Responde409()
    {
        using var fabrica = Construir(EstadoCampania.Borrador);
        using var client = CrearClienteConSesion(fabrica);

        using var respuesta = await EnviarAsync(client);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Envios_CampaniaActiva_Responde202ConJob()
    {
        using var fabrica = Construir(EstadoCampania.Activa);
        using var client = CrearClienteConSesion(fabrica);

        using var respuesta = await EnviarAsync(client);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        cuerpo.Should().Contain("\"jobId\"");
        cuerpo.Should().Contain("\"encolados\":1");
        cuerpo.Should().Contain("\"estado\":\"enProceso\"");
    }

    private static Task<HttpResponseMessage> EnviarAsync(HttpClient client)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/campanias/{CampaniaId}/envios");
        request.Headers.Add("X-CSRF-Token", CsrfAdmin);
        request.Content = JsonContent.Create(new { participantes = new[] { "u_1" }, mensajeInicialId = "mi_1" });
        return client.SendAsync(request);
    }

    private static WebApplicationFactory<Program> Construir(EstadoCampania estado)
    {
        var campanias = Substitute.For<IRepositorioCampanias>();
        campanias.ObtenerCampaniaPorIdAsync(CampaniaId, Arg.Any<CancellationToken>()).Returns(CrearCampania(estado));

        var participantes = Substitute.For<IRepositorioParticipantes>();
        participantes.ListarParticipantesAsync(CampaniaId, Arg.Any<CancellationToken>())
            .Returns(new[] { CrearParticipante("u_1") });

        var usuarios = Substitute.For<IRepositorioUsuarios>();
        usuarios.ObtenerUsuarioPorIdAsync("u_1", Arg.Any<CancellationToken>()).Returns(CrearUsuario("u_1"));

        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(campanias);
                services.AddSingleton(participantes);
                services.AddSingleton(usuarios);
                services.AddSingleton<IServicioSesion, SesionesFake>();
                services.AddScoped<IServicioEnvios, ServicioEnvios>();
            });
        });
    }

    private static HttpClient CrearClienteConSesion(WebApplicationFactory<Program> fabrica)
    {
        var client = fabrica.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", $"{CookieSesion}=token-admin");
        return client;
    }

    private static Campania CrearCampania(EstadoCampania estado)
    {
        var mensaje = MensajeInicial.Crear("mi_1", "saludo", "Hola {{nombre}}", 1, new[] { "nombre" }, EstadoRegistro.Activo, null);
        var pregunta = Pregunta.Crear(
            "p_1",
            "Idea",
            "Instruccion",
            "categoria",
            1,
            EstadoRegistro.Activo,
            null,
            null,
            null,
            1,
            LimitesSeguridad.ParaPregunta(1500, 2),
            ConfigMarkdown.Crear(TipoArtefactoMarkdown.Respuesta));

        return Campania.Crear(
            CampaniaId,
            "Campania",
            "Descripcion",
            "Objetivo",
            estado,
            new[] { mensaje },
            new[] { pregunta },
            "rub_1",
            null,
            "llm_1",
            ConfigMarkdown.Crear(TipoArtefactoMarkdown.Respuesta),
            ConfigConversacional.Crear(1, "Gracias."),
            LimitesSeguridad.Crear(1500, 10, 2),
            null,
            Epoca,
            Epoca);
    }

    private static ParticipanteCampania CrearParticipante(string usuarioId)
        => ParticipanteCampania.Crear(
            "pc_" + usuarioId,
            CampaniaId,
            usuarioId,
            NumeroWhatsApp.FromNormalized(Numero),
            EstadoRegistro.Activo,
            EstadoEnvio.Pendiente,
            EstadoRespuestaParticipante.SinRespuesta,
            Epoca,
            null,
            null);

    private static Usuario CrearUsuario(string id)
        => Usuario.Crear(
            id,
            "Usuario " + id,
            NumeroWhatsApp.FromNormalized(Numero),
            RolUsuario.Participante,
            EstadoRegistro.Activo,
            "Operaciones",
            "GHT",
            null,
            null,
            Epoca,
            Epoca);

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
