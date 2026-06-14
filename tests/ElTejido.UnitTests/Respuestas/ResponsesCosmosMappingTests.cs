using ElTejido.Domain.Campanas;
using ElTejido.Domain.Evaluacion;
using ElTejido.Domain.Respuestas;
using ElTejido.Infrastructure.Respuestas;
using FluentAssertions;
using DominioEvaluacion = ElTejido.Domain.Evaluacion.Evaluacion;

namespace ElTejido.UnitTests.Respuestas;

public sealed class ResponsesCosmosMappingTests
{
    private static readonly DateTimeOffset Epoca = DateTimeOffset.UnixEpoch;

    [Fact]
    public void Respuesta_RoundTrip_ConservaCampos()
    {
        var respuesta = Respuesta.Crear(
            "resp_1", "c_1", "u_1", "p_1", "conv_1", "Mi idea", "whatsapp", true, EstadoRespuesta.EvaluacionPendiente, Epoca, new[] { "t_oper" });

        var resultado = RespuestaCosmosDocument.FromDomain(respuesta).ToDomain();

        resultado.Id.Should().Be("resp_1");
        resultado.ConversacionId.Should().Be("conv_1");
        resultado.EsRepregunta.Should().BeTrue();
        resultado.Estado.Should().Be(EstadoRespuesta.EvaluacionPendiente);
        resultado.TagsSnapshot.Should().ContainSingle().Which.Should().Be("t_oper");
    }

    [Fact]
    public void Evaluacion_RoundTrip_ConservaSnapshotsYRecomendacion()
    {
        var evaluacion = DominioEvaluacion.Crear(
            "eval_1", "c_1", "resp_1", "u_1", "p_1", "r_general", 3, "pr_eval", 5, "llm_default",
            new ConfigLlmSnapshot("AzureOpenAI", "gpt-4o-mini", "https://x", new Dictionary<string, object?>()),
            new Dictionary<string, decimal> { ["claridad"] = 0.5m },
            new[] { CalificacionCriterio.Crear("claridad", 4m, "clara") },
            4.0m, "buena", "Buena idea", RecomendacionEvaluacion.Repreguntar, "Cuanto ahorra?",
            new[] { "eficiencia" }, new[] { "bodega" }, anomaliaSeguridad: true, Epoca);

        var resultado = EvaluacionCosmosDocument.FromDomain(evaluacion).ToDomain();

        resultado.VersionRubrica.Should().Be(3);
        resultado.VersionPrompt.Should().Be(5);
        resultado.Recomendacion.Should().Be(RecomendacionEvaluacion.Repreguntar);
        resultado.RepreguntaSugerida.Should().Be("Cuanto ahorra?");
        resultado.PesosUsados.Should().ContainKey("claridad");
        resultado.CalificacionPorCriterio.Should().ContainSingle();
        resultado.AnomaliaSeguridad.Should().BeTrue();
        resultado.ConfigLlmSnapshot.Modelo.Should().Be("gpt-4o-mini");
    }

    [Fact]
    public void Artefacto_RoundTrip_ConservaVersionYRefs()
    {
        var artefacto = ArtefactoMarkdown.Crear(
            "md_resp_1", "c_1", TipoArtefactoMarkdown.Respuesta, "u_1", "p_1", "resp_1", "eval_1",
            "# Contenido", "campanias/c_1/respuesta/resp_1.md", EstadoArtefacto.Generado, 2, Epoca, Epoca);

        var resultado = ArtefactoMarkdownCosmosDocument.FromDomain(artefacto).ToDomain();

        resultado.Id.Should().Be("md_resp_1");
        resultado.TipoArtefacto.Should().Be(TipoArtefactoMarkdown.Respuesta);
        resultado.Version.Should().Be(2);
        resultado.RespuestaRef.Should().Be("resp_1");
        resultado.EvaluacionRef.Should().Be("eval_1");
        resultado.BlobPath.Should().Be("campanias/c_1/respuesta/resp_1.md");
    }
}
