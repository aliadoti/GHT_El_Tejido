using ElTejido.Domain.Campanas;
using ElTejido.Domain.Configuracion;
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
        resultado.NivelMadurez.Should().Be(NivelMadurez.Incubacion);
    }

    [Fact]
    public void Respuesta_RoundTrip_ConservaNivelMadurezMaduro()
    {
        var respuesta = Respuesta.Crear(
            "resp_1", "c_1", "u_1", "p_1", "conv_1", "Mi idea", "whatsapp", false, EstadoRespuesta.Evaluada, Epoca, null,
            nivelMadurez: NivelMadurez.Maduro);

        var resultado = RespuestaCosmosDocument.FromDomain(respuesta).ToDomain();

        resultado.NivelMadurez.Should().Be(NivelMadurez.Maduro);
    }

    [Fact]
    public void Respuesta_DocumentoAnteriorSinNivelMadurez_DeserializaComoIncubacion()
    {
        // I-17: documento historico (03 §3.8) sin el campo -> default seguro incubacion.
        var documento = new RespuestaCosmosDocument
        {
            Id = "resp_legacy",
            CampaniaId = "c_1",
            UsuarioId = "u_1",
            PreguntaId = "p_1",
            ConversacionId = "conv_1",
            Texto = "Idea historica",
            Canal = "whatsapp",
            Estado = "evaluada",
            Fecha = Epoca,
            NivelMadurez = null,
        };

        documento.ToDomain().NivelMadurez.Should().Be(NivelMadurez.Incubacion);
    }

    [Fact]
    public void Respuesta_RoundTrip_ConservaLinajeI18YLegacyQuedaSinLinaje()
    {
        var revision = Respuesta.Crear(
            "resp_1_rev_1",
            "c_1",
            "u_1",
            "p_1",
            "conv_1",
            "Idea mejorada",
            "whatsapp",
            true,
            EstadoRespuesta.Evaluada,
            Epoca,
            null,
            ideaIndice: 1,
            respuestaPadreId: "wamid.raiz",
            ideaRaizId: "resp_1",
            respuestaAnteriorId: "resp_1",
            revisionIndice: 1);

        var resultado = RespuestaCosmosDocument.FromDomain(revision).ToDomain();
        var legacy = new RespuestaCosmosDocument
        {
            Id = "resp_legacy",
            CampaniaId = "c_1",
            UsuarioId = "u_1",
            PreguntaId = "p_1",
            ConversacionId = "conv_1",
            Texto = "Idea",
            Estado = "evaluada",
            Fecha = Epoca,
        }.ToDomain();

        resultado.IdeaRaizId.Should().Be("resp_1");
        resultado.RespuestaAnteriorId.Should().Be("resp_1");
        resultado.RevisionIndice.Should().Be(1);
        legacy.IdeaRaizId.Should().BeNull();
        legacy.RevisionIndice.Should().BeNull();
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
            new[] { "eficiencia" }, new[] { "bodega" }, anomaliaSeguridad: true, Epoca,
            parafraseoDevuelto: "Entendi que propones optimizar la bodega.");

        var resultado = EvaluacionCosmosDocument.FromDomain(evaluacion).ToDomain();

        resultado.VersionRubrica.Should().Be(3);
        resultado.VersionPrompt.Should().Be(5);
        resultado.Recomendacion.Should().Be(RecomendacionEvaluacion.Repreguntar);
        resultado.RepreguntaSugerida.Should().Be("Cuanto ahorra?");
        resultado.PesosUsados.Should().ContainKey("claridad");
        resultado.CalificacionPorCriterio.Should().ContainSingle();
        resultado.AnomaliaSeguridad.Should().BeTrue();
        resultado.ConfigLlmSnapshot.Modelo.Should().Be("gpt-4o-mini");
        resultado.ParafraseoDevuelto.Should().Be("Entendi que propones optimizar la bodega.");
    }

    [Fact]
    public void Evaluacion_RoundTrip_ConservaSnapshotDeRubricaYCriterioId()
    {
        // DT-RUB-01 §8: la evaluacion debe seguir siendo explicable aunque despues exista una version
        // nueva de la rubrica, sin volver a consultarla.
        var rubrica = Rubrica.Crear(
            "r_general",
            "Rubrica general",
            "desc",
            new EscalaRubrica(1, 5),
            [
                CriterioRubrica.Crear("claridad", "Claridad", "Que tan concreta es.", 0.3m, 1),
                CriterioRubrica.Crear("viabilidad", "Viabilidad", "Que tan realizable es.", 0.7m, 2),
            ],
            3,
            EstadoRubrica.Activa,
            Epoca,
            Epoca);

        var evaluacion = DominioEvaluacion.Crear(
            "eval_1", "c_1", "resp_1", "u_1", "p_1", "r_general", 3, "pr_eval", 5, "llm_default",
            new ConfigLlmSnapshot("AzureOpenAI", "gpt-4o-mini", "https://x", new Dictionary<string, object?>()),
            new Dictionary<string, decimal> { ["claridad"] = 0.3m, ["viabilidad"] = 0.7m },
            new[]
            {
                CalificacionCriterio.Crear("claridad", "Claridad", 5m, "clara"),
                CalificacionCriterio.Crear("viabilidad", "Viabilidad", 3m, "cuesta"),
            },
            3.6m, "buena", "Buena idea", RecomendacionEvaluacion.Cerrar, null,
            null, null, anomaliaSeguridad: false, Epoca,
            rubricaSnapshot: SnapshotRubricaEvaluacion.Desde(rubrica));

        var resultado = EvaluacionCosmosDocument.FromDomain(evaluacion).ToDomain();

        resultado.CalificacionPorCriterio.Select(c => c.CriterioId).Should().Equal("claridad", "viabilidad");
        resultado.CalificacionTotal.Should().Be(3.6m);
        resultado.RubricaSnapshot.Should().NotBeNull();
        resultado.RubricaSnapshot!.RubricaId.Should().Be("r_general");
        resultado.RubricaSnapshot.Version.Should().Be(3);
        resultado.RubricaSnapshot.Escala.Max.Should().Be(5);
        resultado.RubricaSnapshot.HashEstructura.Should().Be(rubrica.HashEstructura);
        resultado.RubricaSnapshot.Criterios.Select(c => c.Id).Should().Equal("claridad", "viabilidad");
        resultado.RubricaSnapshot.Criterios.Select(c => c.Peso).Should().Equal(0.3m, 0.7m);
        resultado.RubricaSnapshot.Criterios[0].Descripcion.Should().Be("Que tan concreta es.");
    }

    [Fact]
    public void Evaluacion_DocumentoHistoricoSinCriterioId_ConservaElNombreSnapshot()
    {
        // 03 §3.9: la compatibilidad de lectura no infiere una clave que el documento nunca tuvo.
        const string JsonLegacy = """
            {
              "id": "eval_legacy",
              "type": "Evaluacion",
              "campaniaId": "c_1",
              "respuestaId": "resp_1",
              "usuarioId": "u_1",
              "preguntaId": "p_1",
              "rubricaRef": "r_general",
              "versionRubrica": 2,
              "promptRef": "pr_eval",
              "versionPrompt": 4,
              "configLLMRef": "llm_default",
              "configLLMSnapshot": { "proveedor": "AzureOpenAI", "modelo": "gpt-4o-mini", "endpoint": "https://x", "parametros": {} },
              "pesosUsados": { "Impacto": 1.0 },
              "calificacionPorCriterio": [ { "criterio": "Impacto", "puntaje": 4, "justificacion": "ok" } ],
              "calificacionTotal": 4.0,
              "explicacion": "buena",
              "retroalimentacionEnviada": "Gracias",
              "recomendacion": "cerrar",
              "temas": [],
              "entidades": [],
              "anomaliaSeguridad": false,
              "fecha": "1970-01-01T00:00:00Z"
            }
            """;

        var resultado = Newtonsoft.Json.JsonConvert
            .DeserializeObject<EvaluacionCosmosDocument>(JsonLegacy)!
            .ToDomain();

        resultado.CalificacionPorCriterio.Should().ContainSingle();
        resultado.CalificacionPorCriterio.First().Criterio.Should().Be("Impacto");
        resultado.CalificacionPorCriterio.First().CriterioId.Should().BeEmpty();
        resultado.RubricaSnapshot.Should().BeNull();
        resultado.CalificacionTotal.Should().Be(4.0m);
    }

    [Fact]
    public void Evaluacion_DocumentoAnteriorSinParafraseo_DeserializaComoNull()
    {
        var evaluacion = DominioEvaluacion.Crear(
            "eval_1", "c_1", "resp_1", "u_1", "p_1", "r_general", 3, "pr_eval", 5, "llm_default",
            new ConfigLlmSnapshot("AzureOpenAI", "gpt-4o-mini", "https://x", new Dictionary<string, object?>()),
            new Dictionary<string, decimal>(), Array.Empty<CalificacionCriterio>(), 4m, "buena", "Buena idea",
            RecomendacionEvaluacion.Cerrar, null, null, null, anomaliaSeguridad: false, Epoca);

        var resultado = EvaluacionCosmosDocument.FromDomain(evaluacion).ToDomain();

        resultado.ParafraseoDevuelto.Should().BeNull();
    }

    [Fact]
    public void Respuesta_I19_RoundTrip_ConservaIdeaYTipoAporte()
    {
        var respuesta = Respuesta.Crear(
            "resp_1", "c_1", "u_1", "p_1", "conv_1", "Complemento", "whatsapp", true,
            EstadoRespuesta.Recibida, Epoca, null, ideaId: "idea_1", tipoAporte: TipoAporteIdea.Complemento);

        var resultado = RespuestaCosmosDocument.FromDomain(respuesta).ToDomain();

        resultado.IdeaId.Should().Be("idea_1");
        resultado.TipoAporte.Should().Be(TipoAporteIdea.Complemento);
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
