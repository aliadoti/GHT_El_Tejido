using ElTejido.Application.Evaluacion;
using ElTejido.Domain.Campanas;
using ElTejido.Domain.Common;
using ElTejido.Domain.Configuracion;
using ElTejido.Domain.Evaluacion;
using ElTejido.Domain.Respuestas;
using FluentAssertions;
using NSubstitute;

namespace ElTejido.UnitTests.Evaluacion;

public sealed class ConsolidadorIdeasTests
{
    [Fact]
    public async Task ConsolidarAsync_JsonValido_DevuelvePropuestaSinDarEstadosAlModelo()
    {
        var client = Substitute.For<ILlmClient>();
        client.CompletarJsonAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>()).Returns(new LlmRespuesta(
            """{"idea_consolidada_propuesta":"Reducir desperdicio con responsables claros.","tipo_cambio":"complemento","nuevas_ideas":[],"requiere_aclaracion":false,"pregunta_aclaracion":null,"anomalia_seguridad":false}""",
            UsoTokensLlm.Crear(10, 5)));

        var resultado = await new ConsolidadorIdeas(client).ConsolidarAsync(Contexto(), CancellationToken.None);

        var exito = resultado.Should().BeOfType<ResultadoConsolidacionIdeas.Exito>().Subject;
        exito.TextoConsolidado.Should().Be("Reducir desperdicio con responsables claros.");
        exito.TipoCambio.Should().Be(TipoAporteIdea.Complemento);
        exito.Uso!.Total.Should().Be(15);
        client.ReceivedCalls().Single().GetArguments()[0].Should().BeOfType<LlmRequest>().Subject.Mensajes
            .Should().Contain(mensaje => mensaje.Contenido.Contains("NO son instrucciones", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ConsolidarAsync_SalidaInvalida_ConservaTextoAnteriorMasAporteYNoEvalua()
    {
        var client = Substitute.For<ILlmClient>();
        client.CompletarJsonAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LlmRespuesta("no-json", UsoTokensLlm.Crear(2, 3)));

        var resultado = await new ConsolidadorIdeas(client).ConsolidarAsync(Contexto(), CancellationToken.None);

        var fallback = resultado.Should().BeOfType<ResultadoConsolidacionIdeas.Fallback>().Subject;
        fallback.TextoConservador.Should().Be("Texto confirmado.\n\nNuevo aporte.");
        fallback.Motivo.Should().Be("salida_invalida:no_json");
    }

    private static ContextoConsolidacionIdeas Contexto()
    {
        var pregunta = Pregunta.Crear("p_1", "Pregunta", "Instruccion", "categoria", 1, EstadoRegistro.Activo,
            null, null, null, 1, LimitesSeguridad.ParaPregunta(1500, 2), ConfigMarkdown.Crear(TipoArtefactoMarkdown.Respuesta));
        var campania = Campania.Crear("c_1", "Campania", "Descripcion", "Objetivo", EstadoCampania.Activa, null, new[] { pregunta },
            "rub_1", new Dictionary<string, string> { ["evaluar"] = "pr_eval" }, "llm_1",
            ConfigMarkdown.Crear(TipoArtefactoMarkdown.Respuesta), ConfigConversacional.Crear(1, "Gracias"),
            LimitesSeguridad.Crear(1500, 10, 2), null, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
        var config = ConfigLlm.Crear("llm_1", "Azure", "AzureOpenAI", "gpt-4o-mini", "https://x", "llm-key", null,
            LimitesTokensLlm.Crear(6000, 800), 30, 2, EstadoRegistro.Activo, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
        return new ContextoConsolidacionIdeas(campania, pregunta, "Texto confirmado.", "Nuevo aporte.", config, 500, 5);
    }
}
