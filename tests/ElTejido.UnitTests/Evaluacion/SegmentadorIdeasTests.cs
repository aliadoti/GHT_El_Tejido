using ElTejido.Application.Evaluacion;
using ElTejido.Domain.Campanas;
using ElTejido.Domain.Common;
using ElTejido.Domain.Configuracion;
using ElTejido.Domain.Evaluacion;
using FluentAssertions;
using NSubstitute;

namespace ElTejido.UnitTests.Evaluacion;

public sealed class SegmentadorIdeasTests
{
    [Fact]
    public async Task SegmentarAsync_JsonValido_DevuelveIdeasSinReescribirlas()
    {
        var client = Substitute.For<ILlmClient>();
        client.CompletarJsonAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LlmRespuesta(
                """{ "ideas": [{ "texto": "Primera idea", "resumen": null }, { "texto": "Segunda idea", "resumen": "resumen" }] }""",
                UsoTokensLlm.Crear(10, 5)));
        var segmentador = new SegmentadorIdeas(client);

        var resultado = await segmentador.SegmentarAsync(Contexto(), CancellationToken.None);

        var exito = resultado.Should().BeOfType<ResultadoSegmentacionIdeas.Exito>().Subject;
        exito.Ideas.Select(idea => idea.Texto).Should().Equal("Primera idea", "Segunda idea");
        exito.Uso!.Total.Should().Be(15);
        var request = client.ReceivedCalls().Single().GetArguments()[0].Should().BeOfType<LlmRequest>().Subject;
        request.Mensajes.Should().Contain(mensaje => mensaje.Rol == LlmMensaje.RolSistema && mensaje.Contenido.Contains("No reescribas", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SegmentarAsync_JsonInvalido_DevuelveFallbackConUso()
    {
        var client = Substitute.For<ILlmClient>();
        client.CompletarJsonAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LlmRespuesta("no es json", UsoTokensLlm.Crear(7, 3)));

        var resultado = await new SegmentadorIdeas(client).SegmentarAsync(Contexto(), CancellationToken.None);

        var fallback = resultado.Should().BeOfType<ResultadoSegmentacionIdeas.Fallback>().Subject;
        fallback.Motivo.Should().Be("salida_invalida:no_json");
        fallback.Uso!.Total.Should().Be(10);
    }

    private static ContextoSegmentacionIdeas Contexto()
    {
        var pregunta = Pregunta.Crear(
            "p_1", "Pregunta", "Instruccion", "categoria", 1, EstadoRegistro.Activo,
            null, null, null, 1, LimitesSeguridad.ParaPregunta(1500, 2), ConfigMarkdown.Crear(TipoArtefactoMarkdown.Respuesta));
        var campania = Campania.Crear(
            "c_1", "Campania", "Descripcion", "Objetivo", EstadoCampania.Activa, null, new[] { pregunta },
            "rub_1", new Dictionary<string, string> { ["evaluar"] = "pr_eval" }, "llm_1",
            ConfigMarkdown.Crear(TipoArtefactoMarkdown.Respuesta), ConfigConversacional.Crear(1, "Gracias"),
            LimitesSeguridad.Crear(1500, 10, 2), null, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
        var config = ConfigLlm.Crear(
            "llm_1", "Azure", "AzureOpenAI", "gpt-4o-mini", "https://x", "llm-key", null,
            LimitesTokensLlm.Crear(6000, 800), 30, 2, EstadoRegistro.Activo, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
        return new ContextoSegmentacionIdeas(campania, pregunta, "Una respuesta", Array.Empty<string>(), config);
    }
}
