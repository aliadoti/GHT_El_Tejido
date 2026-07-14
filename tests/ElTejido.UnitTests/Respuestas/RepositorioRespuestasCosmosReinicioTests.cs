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

    private static RespuestaCosmosDocument DocRespuesta(string id, string usuarioId)
        => RespuestaCosmosDocument.FromDomain(
            Respuesta.Crear(id, "c_1", usuarioId, "p_1", "conv_1", "Idea", "whatsapp", false, EstadoRespuesta.Recibida, Epoca, null));

    private static EvaluacionCosmosDocument DocEvaluacion(string id, string respuestaId, string usuarioId)
        => EvaluacionCosmosDocument.FromDomain(
            DominioEvaluacion.Crear(
                id, "c_1", respuestaId, usuarioId, "p_1", "r_general", 1, "pr_eval", 1, "llm_default",
                new ConfigLlmSnapshot("AzureOpenAI", "gpt-4o-mini", "https://x", new Dictionary<string, object?>()),
                null, null, 3m, "ok", "Bien", RecomendacionEvaluacion.Cerrar, null, null, null, false, Epoca));

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

        public List<(string Id, string PartitionKey)> Deletes { get; } = [];

        public List<bool> QueriesConUsuario { get; } = [];

        public Task UpsertAsync<T>(T document, string partitionKey, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<T?> ReadByIdAsync<T>(string id, string partitionKey, CancellationToken cancellationToken)
            where T : class
            => Task.FromResult<T?>(null);

        public Task DeleteAsync(string id, string partitionKey, CancellationToken cancellationToken)
        {
            Deletes.Add((id, partitionKey));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<T>> QueryAsync<T>(QueryDefinition query, string partitionKey, CancellationToken cancellationToken)
        {
            QueriesConUsuario.Add(query.QueryText.Contains("c.usuarioId", StringComparison.Ordinal));
            IReadOnlyCollection<object> resultado = typeof(T) switch
            {
                var t when t == typeof(RespuestaCosmosDocument) => Respuestas,
                var t when t == typeof(EvaluacionCosmosDocument) => Evaluaciones,
                var t when t == typeof(ArtefactoMarkdownCosmosDocument) => Artefactos,
                _ => Array.Empty<object>(),
            };
            return Task.FromResult<IReadOnlyCollection<T>>(resultado.Cast<T>().ToArray());
        }
    }
}
