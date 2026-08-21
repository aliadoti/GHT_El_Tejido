using ElTejido.Domain.Campanas;
using ElTejido.Domain.Evaluacion;
using ElTejido.Domain.Respuestas;
using ElTejido.Infrastructure.Respuestas;
using FluentAssertions;
using Microsoft.Azure.Cosmos;
using DominioEvaluacion = ElTejido.Domain.Evaluacion.Evaluacion;

namespace ElTejido.UnitTests.Respuestas;

/// <summary>
/// P-03 — el adaptador Cosmos de <c>responses</c> borra respuestas/evaluaciones/artefactos por id
/// dentro de la particion <c>campaniaId</c>, filtra por usuario en la consulta y reporta las rutas
/// de blob de los artefactos borrados.
/// </summary>
public sealed class RepositorioRespuestasCosmosReinicioTests
{
    private static readonly DateTimeOffset Epoca = DateTimeOffset.UnixEpoch;

    [Fact]
    public async Task ObtenerEvaluacionPorRespuesta_DevuelveLaMasReciente()
    {
        var container = new FakeResponsesCosmosContainer
        {
            Evaluaciones =
            [
                DocEvaluacion("eval_vieja", "resp_1", "u_1", 2m, Epoca),
                DocEvaluacion("eval_nueva", "resp_1", "u_1", 5m, Epoca.AddMinutes(10)),
            ],
        };
        var repo = new RepositorioRespuestasCosmos(container);

        var resultado = await repo.ObtenerEvaluacionPorRespuestaAsync("c_1", "resp_1", CancellationToken.None);

        resultado.Should().NotBeNull();
        resultado!.Id.Should().Be("eval_nueva");
        resultado.CalificacionTotal.Should().Be(5m);
        container.QueryTexts.Should().ContainSingle(q => q.Contains("ORDER BY c.fecha DESC", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ListarEvaluaciones_DevuelveFechaDescendenteEnLaMismaParticion()
    {
        var container = new FakeResponsesCosmosContainer
        {
            Evaluaciones =
            [
                DocEvaluacion("eval_vieja", "resp_1", "u_1", 2m, Epoca),
                DocEvaluacion("eval_nueva", "resp_2", "u_1", 5m, Epoca.AddMinutes(10)),
            ],
        };
        var repo = new RepositorioRespuestasCosmos(container);

        var resultado = await repo.ListarEvaluacionesAsync("c_1", CancellationToken.None);

        resultado.Select(evaluacion => evaluacion.Id).Should().ContainInOrder("eval_nueva", "eval_vieja");
        container.QueryTexts.Should().ContainSingle(query =>
            query.Contains("c.type = @type", StringComparison.Ordinal)
            && query.Contains("ORDER BY c.fecha DESC", StringComparison.Ordinal));
    }

    [Fact]
    public async Task EliminarPorUsuario_BorraPorIdEnLaParticionYReportaRutasBlob()
    {
        var container = new FakeResponsesCosmosContainer
        {
            Respuestas = [DocRespuesta("resp_1", "u_1")],
            Evaluaciones = [DocEvaluacion("eval_1", "resp_1", "u_1")],
            Artefactos = [DocArtefacto("md_1", "resp_1", "u_1", "campanias/c_1/respuesta/resp_1.md")],
        };
        var repo = new RepositorioRespuestasCosmos(container);

        var conteo = await repo.EliminarPorUsuarioAsync("c_1", "u_1", CancellationToken.None);

        conteo.Respuestas.Should().Be(1);
        conteo.Evaluaciones.Should().Be(1);
        conteo.Artefactos.Should().Be(1);
        conteo.RutasBlob.Should().ContainSingle().Which.Should().Be("campanias/c_1/respuesta/resp_1.md");

        container.Deletes.Should().BeEquivalentTo(new[]
        {
            ("resp_1", "c_1"),
            ("eval_1", "c_1"),
            ("md_1", "c_1"),
        });
        container.QueriesConUsuario.Should().OnlyContain(q => q);
    }

    [Fact]
    public async Task EliminarSinUsuario_NoFiltraPorUsuarioEnLaConsulta()
    {
        var container = new FakeResponsesCosmosContainer();
        var repo = new RepositorioRespuestasCosmos(container);

        await repo.EliminarPorUsuarioAsync("c_1", usuarioId: null, CancellationToken.None);

        container.QueriesConUsuario.Should().OnlyContain(q => !q);
    }

    [Fact]
    public async Task P30_ListarHistoricas_FiltraUsuarioYPreguntaSinFiltrarEstado()
    {
        var idea = IdeaConsolidada.Crear(
            "idea_1", "c_1", "u_1", "p_1", "conv_1", "resp_1", 1, Epoca);
        var container = new FakeResponsesCosmosContainer
        {
            Ideas = [IdeaConsolidadaCosmosDocument.FromDomain(idea)],
        };
        var repo = new RepositorioRespuestasCosmos(container);

        var resultado = await repo.ListarIdeasHistoricasAsync(
            "c_1", "u_1", "p_1", CancellationToken.None);

        resultado.Should().ContainSingle().Which.Id.Should().Be("idea_1");
        container.QueryTexts.Should().ContainSingle(query =>
            query.Contains("c.usuarioId = @usuarioId", StringComparison.Ordinal)
            && query.Contains("c.preguntaId = @preguntaId", StringComparison.Ordinal)
            && !query.Contains("estado", StringComparison.OrdinalIgnoreCase));
    }

    // P-34 §6 (H-10): las versiones de una pagina se piden en UNA consulta por particion.
    [Fact]
    public async Task P34_ListarVersionesDeCampania_UnaSolaConsultaPorIdsSinLecturasPuntuales()
    {
        var container = new FakeResponsesCosmosContainer
        {
            Versiones = [DocVersion("idea_1_v1", "idea_1", 1), DocVersion("idea_2_v1", "idea_2", 1)],
        };
        var repo = new RepositorioRespuestasCosmos(container);

        var resultado = await repo.ListarVersionesDeCampaniaAsync(
            "c_1", ["idea_1_v1", "idea_2_v1", "idea_1_v1", " "], CancellationToken.None);

        resultado.Select(version => version.Id).Should().BeEquivalentTo(["idea_1_v1", "idea_2_v1"]);
        container.QueryTexts.Should().ContainSingle(query =>
            query.Contains("ARRAY_CONTAINS(@versionIds, c.id)", StringComparison.Ordinal));
        container.LecturasPuntuales.Should().BeEmpty();
    }

    [Fact]
    public async Task P34_ListarVersionesDeCampania_SinIds_NoConsultaNada()
    {
        var container = new FakeResponsesCosmosContainer();
        var repo = new RepositorioRespuestasCosmos(container);

        var resultado = await repo.ListarVersionesDeCampaniaAsync("c_1", [], CancellationToken.None);

        resultado.Should().BeEmpty();
        container.QueryTexts.Should().BeEmpty();
    }

    // P-34 §6 (H-10): el detalle pide los aportes de la idea, no la particion completa.
    [Fact]
    public async Task P34_ListarRespuestasPorIdea_FiltraPorIdeaIdEnLaConsulta()
    {
        var container = new FakeResponsesCosmosContainer
        {
            Respuestas = [DocRespuesta("resp_1", "u_1")],
        };
        var repo = new RepositorioRespuestasCosmos(container);

        var resultado = await repo.ListarRespuestasPorIdeaAsync("c_1", "idea_1", CancellationToken.None);

        resultado.Should().ContainSingle().Which.Id.Should().Be("resp_1");
        container.QueryTexts.Should().ContainSingle(query =>
            query.Contains("c.ideaId = @ideaId", StringComparison.Ordinal));
    }

    private static RespuestaCosmosDocument DocRespuesta(string id, string usuarioId)
        => RespuestaCosmosDocument.FromDomain(
            Respuesta.Crear(id, "c_1", usuarioId, "p_1", "conv_1", "Idea", "whatsapp", false, EstadoRespuesta.Recibida, Epoca, null));

    private static EvaluacionCosmosDocument DocEvaluacion(string id, string respuestaId, string usuarioId)
        => DocEvaluacion(id, respuestaId, usuarioId, 3m, Epoca);

    private static EvaluacionCosmosDocument DocEvaluacion(string id, string respuestaId, string usuarioId, decimal calificacionTotal, DateTimeOffset fecha)
        => EvaluacionCosmosDocument.FromDomain(
            DominioEvaluacion.Crear(
                id, "c_1", respuestaId, usuarioId, "p_1", "r_general", 1, "pr_eval", 1, "llm_default",
                new ConfigLlmSnapshot("AzureOpenAI", "gpt-4o-mini", "https://x", new Dictionary<string, object?>()),
                null, null, calificacionTotal, "ok", "Bien", RecomendacionEvaluacion.Cerrar, null, null, null, false, fecha));

    private static VersionIdeaConsolidadaCosmosDocument DocVersion(string id, string ideaId, int numeroVersion)
        => VersionIdeaConsolidadaCosmosDocument.FromDomain(
            VersionIdeaConsolidada.Crear(
                id, "c_1", ideaId, numeroVersion, null, "Texto consolidado", ["resp_1"], ["resp_1"],
                TipoAporteIdea.Inicial, EstadoConfirmacionVersionIdea.Confirmada, null, null, null, null,
                Epoca, Epoca));

    private static ArtefactoMarkdownCosmosDocument DocArtefacto(string id, string respuestaId, string usuarioId, string blobPath)
        => ArtefactoMarkdownCosmosDocument.FromDomain(
            ArtefactoMarkdown.Crear(
                id, "c_1", TipoArtefactoMarkdown.Respuesta, usuarioId, "p_1", respuestaId, "eval_1",
                "# md", blobPath, EstadoArtefacto.Generado, 1, Epoca, Epoca));

    private sealed class FakeResponsesCosmosContainer : IResponsesCosmosContainer
    {
        public IReadOnlyCollection<RespuestaCosmosDocument> Respuestas { get; init; } = [];

        public IReadOnlyCollection<EvaluacionCosmosDocument> Evaluaciones { get; init; } = [];

        public IReadOnlyCollection<ArtefactoMarkdownCosmosDocument> Artefactos { get; init; } = [];

        public IReadOnlyCollection<IdeaConsolidadaCosmosDocument> Ideas { get; init; } = [];

        public IReadOnlyCollection<VersionIdeaConsolidadaCosmosDocument> Versiones { get; init; } = [];

        /// <summary>P-34 §6: cada lectura puntual es la RU que el listado dejo de gastar.</summary>
        public List<(string Id, string PartitionKey)> LecturasPuntuales { get; } = [];

        public List<(string Id, string PartitionKey)> Deletes { get; } = [];

        public List<bool> QueriesConUsuario { get; } = [];

        public List<string> QueryTexts { get; } = [];

        public Task UpsertAsync<T>(T document, string partitionKey, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<T?> ReadByIdAsync<T>(string id, string partitionKey, CancellationToken cancellationToken)
            where T : class
        {
            LecturasPuntuales.Add((id, partitionKey));
            return Task.FromResult<T?>(null);
        }

        public Task DeleteAsync(string id, string partitionKey, CancellationToken cancellationToken)
        {
            Deletes.Add((id, partitionKey));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<T>> QueryAsync<T>(QueryDefinition query, string partitionKey, CancellationToken cancellationToken)
        {
            QueryTexts.Add(query.QueryText);
            QueriesConUsuario.Add(query.QueryText.Contains("c.usuarioId", StringComparison.Ordinal));
            IReadOnlyCollection<object> resultado = typeof(T) switch
            {
                var t when t == typeof(RespuestaCosmosDocument) => Respuestas,
                var t when t == typeof(EvaluacionCosmosDocument) => OrdenarEvaluaciones(query),
                var t when t == typeof(ArtefactoMarkdownCosmosDocument) => Artefactos,
                var t when t == typeof(IdeaConsolidadaCosmosDocument) => Ideas,
                var t when t == typeof(VersionIdeaConsolidadaCosmosDocument) => Versiones,
                _ => Array.Empty<object>(),
            };
            return Task.FromResult<IReadOnlyCollection<T>>(resultado.Cast<T>().ToArray());
        }

        private IReadOnlyCollection<object> OrdenarEvaluaciones(QueryDefinition query)
        {
            if (query.QueryText.Contains("ORDER BY c.fecha DESC", StringComparison.OrdinalIgnoreCase))
            {
                return Evaluaciones.OrderByDescending(e => e.Fecha).Cast<object>().ToArray();
            }

            return Evaluaciones.Cast<object>().ToArray();
        }
    }
}
