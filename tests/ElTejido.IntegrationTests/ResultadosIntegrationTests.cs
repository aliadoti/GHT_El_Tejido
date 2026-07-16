using System.Net;
using ElTejido.Application.Auth;
using ElTejido.Application.Conversacion;
using ElTejido.Application.Markdown;
using ElTejido.Application.Respuestas;
using ElTejido.Domain.Campanas;
using ElTejido.Domain.Common;
using ElTejido.Domain.Conversaciones;
using ElTejido.Domain.Evaluacion;
using ElTejido.Domain.Respuestas;
using ElTejido.Domain.Usuarios;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using DominioEvaluacion = ElTejido.Domain.Evaluacion.Evaluacion;

namespace ElTejido.IntegrationTests;

/// <summary>
/// Verifica los endpoints de consulta de resultados (04 §5.8): respuestas/evaluaciones,
/// conversaciones, Markdown (detalle, raw y regeneracion). Repos mockeados.
/// </summary>
public sealed class ResultadosIntegrationTests
{
    private const string CookieSesion = "eltejido_sesion";
    private const string CsrfAdmin = "csrf-admin";
    private const string CampaniaId = "c_1";
    private static readonly DateTimeOffset Epoca = DateTimeOffset.UnixEpoch;

    [Fact]
    public async Task Respuestas_ListaYDetalleConEvaluacion()
    {
        using var fabrica = Construir();
        using var client = ClienteAdmin(fabrica);

        using var lista = await client.GetAsync($"/api/admin/respuestas?campaniaId={CampaniaId}");
        lista.StatusCode.Should().Be(HttpStatusCode.OK);
        var listaJson = await lista.Content.ReadAsStringAsync();
        listaJson.Should().Contain("resp_1");
        listaJson.Should().Contain("\"ideaIndice\":1");
        listaJson.Should().Contain("\"respuestaPadreId\":\"wamid.1\"");

        using var detalle = await client.GetAsync($"/api/admin/respuestas/resp_1?campaniaId={CampaniaId}");
        detalle.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await detalle.Content.ReadAsStringAsync();
        json.Should().Contain("\"recomendacion\":\"cerrar\"");
        json.Should().Contain("eval_1");
        json.Should().Contain("\"ideaIndice\":1");
    }

    [Fact]
    public async Task Respuestas_SinCampaniaId_Responde400()
    {
        using var fabrica = Construir();
        using var client = ClienteAdmin(fabrica);

        using var respuesta = await client.GetAsync("/api/admin/respuestas");

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Markdown_RawDevuelveContenido()
    {
        using var fabrica = Construir();
        using var client = ClienteAdmin(fabrica);

        using var raw = await client.GetAsync($"/api/admin/markdown/md_1/raw?campaniaId={CampaniaId}");

        raw.StatusCode.Should().Be(HttpStatusCode.OK);
        raw.Content.Headers.ContentType!.MediaType.Should().Be("text/markdown");
        (await raw.Content.ReadAsStringAsync()).Should().Contain("# Aporte");
    }

    [Fact]
    public async Task Markdown_Regenerar_RequiereCsrfYDevuelveArtefacto()
    {
        using var fabrica = Construir();
        using var client = ClienteAdmin(fabrica);

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/markdown/md_1/regenerar?campaniaId={CampaniaId}");
        request.Headers.Add("X-CSRF-Token", CsrfAdmin);
        using var respuesta = await client.SendAsync(request);

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        (await respuesta.Content.ReadAsStringAsync()).Should().Contain("\"version\":2");
    }

    [Fact]
    public async Task Conversaciones_DetalleIncluyeMensajes()
    {
        using var fabrica = Construir();
        using var client = ClienteAdmin(fabrica);

        using var detalle = await client.GetAsync($"/api/admin/conversaciones/conv_1?campaniaId={CampaniaId}");

        detalle.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await detalle.Content.ReadAsStringAsync();
        json.Should().Contain("conv_1");
        json.Should().Contain("\"direccion\":\"in\"");
    }

    private static WebApplicationFactory<Program> Construir()
    {
        var respuestas = Substitute.For<IRepositorioRespuestas>();
        var respuesta = Respuesta.Crear(
            "resp_1", CampaniaId, "u_1", "p_1", "conv_1", "Mi idea", "whatsapp", false,
            EstadoRespuesta.Evaluada, Epoca, new[] { "t_oper" }, ideaIndice: 1, respuestaPadreId: "wamid.1");
        respuestas.ListarRespuestasAsync(CampaniaId, Arg.Any<CancellationToken>()).Returns(new[] { respuesta });
        respuestas.ObtenerRespuestaAsync(CampaniaId, "resp_1", Arg.Any<CancellationToken>()).Returns(respuesta);
        respuestas.ObtenerEvaluacionPorRespuestaAsync(CampaniaId, "resp_1", Arg.Any<CancellationToken>()).Returns(CrearEvaluacion());
        var artefacto = CrearArtefacto(1);
        respuestas.ObtenerArtefactoAsync(CampaniaId, "md_1", Arg.Any<CancellationToken>()).Returns(artefacto);
        respuestas.ListarArtefactosAsync(CampaniaId, Arg.Any<CancellationToken>()).Returns(new[] { artefacto });

        var conversaciones = Substitute.For<IRepositorioConversaciones>();
        conversaciones.ObtenerConversacionAsync(CampaniaId, "conv_1", Arg.Any<CancellationToken>())
            .Returns(Conversacion.Iniciar("conv_1", CampaniaId, "u_1", "p_1", "whatsapp", null, Epoca));
        conversaciones.ListarMensajesAsync(CampaniaId, "conv_1", Arg.Any<CancellationToken>())
            .Returns(new[] { Mensaje.Crear("msg_1", CampaniaId, "conv_1", DireccionMensaje.In, "Mi idea", "wamid.1", Epoca) });

        var compilador = Substitute.For<ICompiladorMarkdown>();
        compilador.CompilarAsync(Arg.Any<SolicitudCompilacion>(), Arg.Any<CancellationToken>()).Returns(CrearArtefacto(2));

        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(respuestas);
                services.AddSingleton(conversaciones);
                services.AddSingleton(compilador);
                services.AddSingleton<IServicioSesion, SesionesFake>();
            });
        });
    }

    private static HttpClient ClienteAdmin(WebApplicationFactory<Program> fabrica)
    {
        var client = fabrica.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", $"{CookieSesion}=token-admin");
        return client;
    }

    private static DominioEvaluacion CrearEvaluacion()
        => DominioEvaluacion.Crear(
            "eval_1", CampaniaId, "resp_1", "u_1", "p_1", "rub_1", 1, "pr_eval", 1, "llm_1",
            new ConfigLlmSnapshot("AzureOpenAI", "gpt-4o-mini", "https://x", new Dictionary<string, object?>()),
            new Dictionary<string, decimal> { ["claridad"] = 1m },
            new[] { CalificacionCriterio.Crear("claridad", 4m, "clara") },
            4m, "explica", "Buena idea", RecomendacionEvaluacion.Cerrar, null,
            new[] { "tema" }, new[] { "ent" }, false, Epoca);

    private static ArtefactoMarkdown CrearArtefacto(int version)
        => ArtefactoMarkdown.Crear(
            "md_1", CampaniaId, TipoArtefactoMarkdown.Respuesta, "u_1", "p_1", "resp_1", "eval_1",
            "# Aporte de Ana\n\nContenido", "campanias/c_1/respuesta/resp_1.md", EstadoArtefacto.Generado, version, Epoca, Epoca);

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
