using ElTejido.Application.Respuestas;
using ElTejido.Domain.Respuestas;
using Microsoft.Azure.Cosmos;
using DominioEvaluacion = ElTejido.Domain.Evaluacion.Evaluacion;

namespace ElTejido.Infrastructure.Respuestas;

/// <summary>
/// Adaptador Cosmos del contenedor <c>responses</c> (pk <c>campaniaId</c>) para Respuesta,
/// Evaluacion y ArtefactoMarkdown (03 §3.8-§3.10). Upsert por id; la evaluacion se localiza por
/// <c>respuestaId</c> (una evaluacion por respuesta, 03 §4).
/// </summary>
public sealed class RepositorioRespuestasCosmos : IRepositorioRespuestas
{
    private readonly IResponsesCosmosContainer _container;

    public RepositorioRespuestasCosmos(Container container)
        : this(new ResponsesCosmosContainer(container))
    {
    }

    internal RepositorioRespuestasCosmos(IResponsesCosmosContainer container)
    {
        _container = container;
    }

    public Task GuardarRespuestaAsync(Respuesta respuesta, CancellationToken cancellationToken)
        => _container.UpsertAsync(RespuestaCosmosDocument.FromDomain(respuesta), respuesta.CampaniaId, cancellationToken);

    public async Task<Respuesta?> ObtenerRespuestaAsync(
        string campaniaId,
        string respuestaId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaniaId);
        ArgumentException.ThrowIfNullOrWhiteSpace(respuestaId);

        var documento = await _container.ReadByIdAsync<RespuestaCosmosDocument>(
            respuestaId.Trim(),
            campaniaId.Trim(),
            cancellationToken);
        return documento?.ToDomain();
    }

    public Task GuardarEvaluacionAsync(DominioEvaluacion evaluacion, CancellationToken cancellationToken)
        => _container.UpsertAsync(EvaluacionCosmosDocument.FromDomain(evaluacion), evaluacion.CampaniaId, cancellationToken);

    public async Task<DominioEvaluacion?> ObtenerEvaluacionPorRespuestaAsync(
        string campaniaId,
        string respuestaId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaniaId);
        ArgumentException.ThrowIfNullOrWhiteSpace(respuestaId);

        var query = new QueryDefinition("SELECT * FROM c WHERE c.type = @type AND c.respuestaId = @respuestaId")
            .WithParameter("@type", EvaluacionCosmosDocument.DocumentType)
            .WithParameter("@respuestaId", respuestaId.Trim());

        var documentos = await _container.QueryAsync<EvaluacionCosmosDocument>(query, campaniaId.Trim(), cancellationToken);
        return documentos.FirstOrDefault()?.ToDomain();
    }

    public async Task<DominioEvaluacion?> ObtenerEvaluacionPorIdAsync(
        string campaniaId,
        string evaluacionId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaniaId);
        ArgumentException.ThrowIfNullOrWhiteSpace(evaluacionId);

        var documento = await _container.ReadByIdAsync<EvaluacionCosmosDocument>(
            evaluacionId.Trim(),
            campaniaId.Trim(),
            cancellationToken);
        return documento?.ToDomain();
    }

    public async Task<IReadOnlyCollection<Respuesta>> ListarRespuestasAsync(
        string campaniaId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaniaId);

        var query = new QueryDefinition("SELECT * FROM c WHERE c.type = @type")
            .WithParameter("@type", RespuestaCosmosDocument.DocumentType);

        var documentos = await _container.QueryAsync<RespuestaCosmosDocument>(query, campaniaId.Trim(), cancellationToken);
        return documentos.Select(d => d.ToDomain()).ToArray();
    }

    public Task GuardarArtefactoAsync(ArtefactoMarkdown artefacto, CancellationToken cancellationToken)
        => _container.UpsertAsync(ArtefactoMarkdownCosmosDocument.FromDomain(artefacto), artefacto.CampaniaId, cancellationToken);

    public async Task<ArtefactoMarkdown?> ObtenerArtefactoAsync(
        string campaniaId,
        string artefactoId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaniaId);
        ArgumentException.ThrowIfNullOrWhiteSpace(artefactoId);

        var documento = await _container.ReadByIdAsync<ArtefactoMarkdownCosmosDocument>(
            artefactoId.Trim(),
            campaniaId.Trim(),
            cancellationToken);
        return documento?.ToDomain();
    }

    public async Task<IReadOnlyCollection<ArtefactoMarkdown>> ListarArtefactosAsync(
        string campaniaId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaniaId);

        var query = new QueryDefinition("SELECT * FROM c WHERE c.type = @type")
            .WithParameter("@type", ArtefactoMarkdownCosmosDocument.DocumentType);

        var documentos = await _container.QueryAsync<ArtefactoMarkdownCosmosDocument>(query, campaniaId.Trim(), cancellationToken);
        return documentos.Select(d => d.ToDomain()).ToArray();
    }
}
