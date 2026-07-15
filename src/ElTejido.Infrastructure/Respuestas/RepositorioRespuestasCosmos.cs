using ElTejido.Application.Respuestas;
using ElTejido.Domain.Respuestas;
using Microsoft.Azure.Cosmos;
using DominioEvaluacion = ElTejido.Domain.Evaluacion.Evaluacion;

namespace ElTejido.Infrastructure.Respuestas;

/// <summary>
/// Adaptador Cosmos del contenedor <c>responses</c> (pk <c>campaniaId</c>) para Respuesta,
/// Evaluacion y ArtefactoMarkdown (03 §3.8-§3.10). Upsert por id; al localizar por
/// <c>respuestaId</c>, se toma la evaluacion mas reciente para tolerar datos legacy (I-16).
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

        var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.type = @type AND c.respuestaId = @respuestaId ORDER BY c.fecha DESC")
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

    public async Task<int> ContarEvaluacionesUsuarioAsync(
        string campaniaId,
        string usuarioId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaniaId);
        ArgumentException.ThrowIfNullOrWhiteSpace(usuarioId);

        var query = new QueryDefinition("SELECT * FROM c WHERE c.type = @type AND c.usuarioId = @usuarioId")
            .WithParameter("@type", EvaluacionCosmosDocument.DocumentType)
            .WithParameter("@usuarioId", usuarioId.Trim());

        var documentos = await _container.QueryAsync<EvaluacionCosmosDocument>(query, campaniaId.Trim(), cancellationToken);
        return documentos.Count;
    }

    public async Task<long> SumarTokensCampaniaAsync(string campaniaId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaniaId);

        var query = new QueryDefinition("SELECT * FROM c WHERE c.type = @type")
            .WithParameter("@type", EvaluacionCosmosDocument.DocumentType);

        var documentos = await _container.QueryAsync<EvaluacionCosmosDocument>(query, campaniaId.Trim(), cancellationToken);
        return documentos.Sum(d => (long)((d.UsoTokens?.PromptTokens ?? 0) + (d.UsoTokens?.CompletionTokens ?? 0)));
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

    public async Task<ConteoBorradoRespuestas> EliminarPorUsuarioAsync(
        string campaniaId,
        string? usuarioId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaniaId);
        var pk = campaniaId.Trim();
        var usuario = string.IsNullOrWhiteSpace(usuarioId) ? null : usuarioId.Trim();

        var respuestas = await ConsultarPorTipoAsync<RespuestaCosmosDocument>(RespuestaCosmosDocument.DocumentType, pk, usuario, cancellationToken);
        var evaluaciones = await ConsultarPorTipoAsync<EvaluacionCosmosDocument>(EvaluacionCosmosDocument.DocumentType, pk, usuario, cancellationToken);
        var artefactos = await ConsultarPorTipoAsync<ArtefactoMarkdownCosmosDocument>(ArtefactoMarkdownCosmosDocument.DocumentType, pk, usuario, cancellationToken);

        foreach (var respuesta in respuestas)
        {
            await _container.DeleteAsync(respuesta.Id, pk, cancellationToken);
        }

        foreach (var evaluacion in evaluaciones)
        {
            await _container.DeleteAsync(evaluacion.Id, pk, cancellationToken);
        }

        foreach (var artefacto in artefactos)
        {
            await _container.DeleteAsync(artefacto.Id, pk, cancellationToken);
        }

        var rutas = artefactos
            .Select(a => a.BlobPath)
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return new ConteoBorradoRespuestas(respuestas.Count, evaluaciones.Count, artefactos.Count, rutas);
    }

    // Consulta acotada a la particion (campaniaId), filtrando por tipo y, si se pide, por usuario.
    private Task<IReadOnlyCollection<T>> ConsultarPorTipoAsync<T>(
        string tipo,
        string partitionKey,
        string? usuarioId,
        CancellationToken cancellationToken)
    {
        var query = usuarioId is null
            ? new QueryDefinition("SELECT * FROM c WHERE c.type = @type")
                .WithParameter("@type", tipo)
            : new QueryDefinition("SELECT * FROM c WHERE c.type = @type AND c.usuarioId = @usuarioId")
                .WithParameter("@type", tipo)
                .WithParameter("@usuarioId", usuarioId);
        return _container.QueryAsync<T>(query, partitionKey, cancellationToken);
    }
}
