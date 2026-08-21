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

    public Task GuardarIdeaConsolidadaAsync(IdeaConsolidada idea, CancellationToken cancellationToken)
        => _container.UpsertAsync(IdeaConsolidadaCosmosDocument.FromDomain(idea), idea.CampaniaId, cancellationToken);

    public async Task<IdeaConsolidada?> ObtenerIdeaConsolidadaAsync(
        string campaniaId, string ideaId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaniaId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ideaId);
        var documento = await _container.ReadByIdAsync<IdeaConsolidadaCosmosDocument>(ideaId.Trim(), campaniaId.Trim(), cancellationToken);
        return documento?.ToDomain();
    }

    public async Task<IReadOnlyCollection<IdeaConsolidada>> ListarIdeasConsolidadasAsync(
        string campaniaId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaniaId);
        var query = new QueryDefinition("SELECT * FROM c WHERE c.type = @type")
            .WithParameter("@type", IdeaConsolidadaCosmosDocument.DocumentType);
        var documentos = await _container.QueryAsync<IdeaConsolidadaCosmosDocument>(query, campaniaId.Trim(), cancellationToken);
        return documentos.Select(documento => documento.ToDomain()).ToArray();
    }

    public async Task<IReadOnlyCollection<IdeaConsolidada>> ListarIdeasHistoricasAsync(
        string campaniaId,
        string usuarioId,
        string preguntaId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaniaId);
        ArgumentException.ThrowIfNullOrWhiteSpace(usuarioId);
        ArgumentException.ThrowIfNullOrWhiteSpace(preguntaId);
        var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.type = @type AND c.usuarioId = @usuarioId AND c.preguntaId = @preguntaId")
            .WithParameter("@type", IdeaConsolidadaCosmosDocument.DocumentType)
            .WithParameter("@usuarioId", usuarioId.Trim())
            .WithParameter("@preguntaId", preguntaId.Trim());
        var documentos = await _container.QueryAsync<IdeaConsolidadaCosmosDocument>(
            query, campaniaId.Trim(), cancellationToken);
        return documentos.Select(documento => documento.ToDomain()).ToArray();
    }

    public Task GuardarVersionIdeaAsync(VersionIdeaConsolidada version, CancellationToken cancellationToken)
        => _container.UpsertAsync(VersionIdeaConsolidadaCosmosDocument.FromDomain(version), version.CampaniaId, cancellationToken);

    public async Task<VersionIdeaConsolidada?> ObtenerVersionIdeaAsync(
        string campaniaId, string versionId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaniaId);
        ArgumentException.ThrowIfNullOrWhiteSpace(versionId);
        var documento = await _container.ReadByIdAsync<VersionIdeaConsolidadaCosmosDocument>(versionId.Trim(), campaniaId.Trim(), cancellationToken);
        return documento?.ToDomain();
    }

    public async Task<IReadOnlyCollection<VersionIdeaConsolidada>> ListarVersionesIdeaAsync(
        string campaniaId, string ideaId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaniaId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ideaId);
        var query = new QueryDefinition("SELECT * FROM c WHERE c.type = @type AND c.ideaId = @ideaId ORDER BY c.numeroVersion")
            .WithParameter("@type", VersionIdeaConsolidadaCosmosDocument.DocumentType)
            .WithParameter("@ideaId", ideaId.Trim());
        var documentos = await _container.QueryAsync<VersionIdeaConsolidadaCosmosDocument>(query, campaniaId.Trim(), cancellationToken);
        return documentos.Select(documento => documento.ToDomain()).ToArray();
    }

    /// <summary>P-34 §4.5: todas las versiones de un conjunto de ideas, en una sola consulta.</summary>
    public async Task<IReadOnlyCollection<VersionIdeaConsolidada>> ListarVersionesDeIdeasAsync(
        string campaniaId, IReadOnlyCollection<string> ideaIds, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaniaId);
        ArgumentNullException.ThrowIfNull(ideaIds);

        var ids = ideaIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (ids.Length == 0)
        {
            return Array.Empty<VersionIdeaConsolidada>();
        }

        var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.type = @type AND ARRAY_CONTAINS(@ideaIds, c.ideaId) ORDER BY c.numeroVersion")
            .WithParameter("@type", VersionIdeaConsolidadaCosmosDocument.DocumentType)
            .WithParameter("@ideaIds", ids);
        var documentos = await _container.QueryAsync<VersionIdeaConsolidadaCosmosDocument>(query, campaniaId.Trim(), cancellationToken);
        return documentos.Select(documento => documento.ToDomain()).ToArray();
    }

    /// <summary>
    /// P-34 §6 (H-10): una sola consulta dentro de la particion en vez de una lectura puntual por
    /// version. Sin ids no consulta nada; los ids se filtran en el servidor con <c>ARRAY_CONTAINS</c>,
    /// igual que el conteo de consolidaciones.
    /// </summary>
    public async Task<IReadOnlyCollection<VersionIdeaConsolidada>> ListarVersionesDeCampaniaAsync(
        string campaniaId, IReadOnlyCollection<string> versionIds, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaniaId);
        ArgumentNullException.ThrowIfNull(versionIds);

        var ids = versionIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (ids.Length == 0)
        {
            return Array.Empty<VersionIdeaConsolidada>();
        }

        var query = new QueryDefinition("SELECT * FROM c WHERE c.type = @type AND ARRAY_CONTAINS(@versionIds, c.id)")
            .WithParameter("@type", VersionIdeaConsolidadaCosmosDocument.DocumentType)
            .WithParameter("@versionIds", ids);
        var documentos = await _container.QueryAsync<VersionIdeaConsolidadaCosmosDocument>(query, campaniaId.Trim(), cancellationToken);
        return documentos.Select(documento => documento.ToDomain()).ToArray();
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

    /// <summary>
    /// P-34 §5: calificaciones vigentes en una sola consulta por particion, con el mismo patron de
    /// ids que las versiones. Sin ids no consulta nada.
    /// </summary>
    public async Task<IReadOnlyCollection<DominioEvaluacion>> ListarEvaluacionesPorIdsAsync(
        string campaniaId,
        IReadOnlyCollection<string> evaluacionIds,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaniaId);
        ArgumentNullException.ThrowIfNull(evaluacionIds);

        var ids = evaluacionIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (ids.Length == 0)
        {
            return Array.Empty<DominioEvaluacion>();
        }

        var query = new QueryDefinition("SELECT * FROM c WHERE c.type = @type AND ARRAY_CONTAINS(@evaluacionIds, c.id)")
            .WithParameter("@type", EvaluacionCosmosDocument.DocumentType)
            .WithParameter("@evaluacionIds", ids);
        var documentos = await _container.QueryAsync<EvaluacionCosmosDocument>(query, campaniaId.Trim(), cancellationToken);
        return documentos.Select(documento => documento.ToDomain()).ToArray();
    }

    public async Task<IReadOnlyCollection<DominioEvaluacion>> ListarEvaluacionesAsync(
        string campaniaId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaniaId);

        var query = new QueryDefinition("SELECT * FROM c WHERE c.type = @type ORDER BY c.fecha DESC")
            .WithParameter("@type", EvaluacionCosmosDocument.DocumentType);
        var documentos = await _container.QueryAsync<EvaluacionCosmosDocument>(query, campaniaId.Trim(), cancellationToken);
        return documentos.Select(documento => documento.ToDomain()).ToArray();
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

    /// <summary>
    /// P-34 §6 (H-10): los aportes de una idea se piden por <c>ideaId</c>; antes el detalle traia la
    /// particion completa de respuestas para descartar casi todo en memoria.
    /// </summary>
    public async Task<IReadOnlyCollection<Respuesta>> ListarRespuestasPorIdeaAsync(
        string campaniaId, string ideaId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaniaId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ideaId);

        var query = new QueryDefinition("SELECT * FROM c WHERE c.type = @type AND c.ideaId = @ideaId")
            .WithParameter("@type", RespuestaCosmosDocument.DocumentType)
            .WithParameter("@ideaId", ideaId.Trim());

        var documentos = await _container.QueryAsync<RespuestaCosmosDocument>(query, campaniaId.Trim(), cancellationToken);
        return documentos.Select(d => d.ToDomain()).ToArray();
    }

    public Task<int> ContarEvaluacionesUsuarioAsync(
        string campaniaId,
        string usuarioId,
        CancellationToken cancellationToken)
        => ContarEvaluacionesUsuarioAsync(campaniaId, usuarioId, desde: null, cancellationToken);

    /// <inheritdoc />
    public Task<int> ContarEvaluacionesUsuarioAsync(
        string campaniaId,
        string usuarioId,
        DateTimeOffset desde,
        CancellationToken cancellationToken)
        => ContarEvaluacionesUsuarioAsync(campaniaId, usuarioId, (DateTimeOffset?)desde, cancellationToken);

    private async Task<int> ContarEvaluacionesUsuarioAsync(
        string campaniaId,
        string usuarioId,
        DateTimeOffset? desde,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaniaId);
        ArgumentException.ThrowIfNullOrWhiteSpace(usuarioId);

        // P-26 §9: la ventana movil se filtra en la consulta (no en memoria) para no traer documentos
        // fuera de alcance; sin `desde` el conteo sigue siendo el acumulado historico.
        var sql = "SELECT * FROM c WHERE c.type = @type AND c.usuarioId = @usuarioId"
            + (desde is null ? string.Empty : " AND c.fecha >= @desde");
        var query = new QueryDefinition(sql)
            .WithParameter("@type", EvaluacionCosmosDocument.DocumentType)
            .WithParameter("@usuarioId", usuarioId.Trim());
        if (desde is not null)
        {
            query = query.WithParameter("@desde", desde.Value.UtcDateTime);
        }

        var documentos = await _container.QueryAsync<EvaluacionCosmosDocument>(query, campaniaId.Trim(), cancellationToken);
        return documentos.Count;
    }

    /// <inheritdoc />
    public Task<int> ContarConsolidacionesUsuarioAsync(
        string campaniaId,
        string usuarioId,
        CancellationToken cancellationToken)
        => ContarConsolidacionesUsuarioAsync(campaniaId, usuarioId, desde: null, cancellationToken);

    /// <inheritdoc />
    public Task<int> ContarConsolidacionesUsuarioAsync(
        string campaniaId,
        string usuarioId,
        DateTimeOffset desde,
        CancellationToken cancellationToken)
        => ContarConsolidacionesUsuarioAsync(campaniaId, usuarioId, (DateTimeOffset?)desde, cancellationToken);

    private async Task<int> ContarConsolidacionesUsuarioAsync(
        string campaniaId,
        string usuarioId,
        DateTimeOffset? desde,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaniaId);
        ArgumentException.ThrowIfNullOrWhiteSpace(usuarioId);

        // Las versiones no llevan usuarioId; se acotan por las ideas del usuario dentro de la misma
        // particion (campaniaId), asi que son dos consultas acotadas y sin fan-out entre particiones.
        var particion = campaniaId.Trim();
        var ideas = new QueryDefinition("SELECT c.id FROM c WHERE c.type = @type AND c.usuarioId = @usuarioId")
            .WithParameter("@type", IdeaConsolidadaCosmosDocument.DocumentType)
            .WithParameter("@usuarioId", usuarioId.Trim());
        var documentosIdeas = await _container.QueryAsync<IdeaConsolidadaCosmosDocument>(ideas, particion, cancellationToken);
        if (documentosIdeas.Count == 0)
        {
            return 0;
        }

        var idsIdeas = documentosIdeas.Select(documento => documento.Id).ToArray();
        var sqlVersiones = "SELECT c.id FROM c WHERE c.type = @type AND ARRAY_CONTAINS(@ideaIds, c.ideaId)"
            + (desde is null ? string.Empty : " AND c.generadaEn >= @desde");
        var versiones = new QueryDefinition(sqlVersiones)
            .WithParameter("@type", VersionIdeaConsolidadaCosmosDocument.DocumentType)
            .WithParameter("@ideaIds", idsIdeas);
        if (desde is not null)
        {
            versiones = versiones.WithParameter("@desde", desde.Value.UtcDateTime);
        }

        var documentosVersiones = await _container.QueryAsync<VersionIdeaConsolidadaCosmosDocument>(
            versiones, particion, cancellationToken);
        return documentosVersiones.Count;
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
        var ideas = await ConsultarPorTipoAsync<IdeaConsolidadaCosmosDocument>(IdeaConsolidadaCosmosDocument.DocumentType, pk, usuario, cancellationToken);
        // Las versiones no duplican usuarioId: solo si hay ideas autorizadas se consulta y filtra por
        // sus ids. Así el reinicio histórico conserva sus consultas acotadas por usuario.
        var ideaIds = ideas.Select(idea => idea.Id).ToHashSet(StringComparer.Ordinal);
        var versiones = ideaIds.Count == 0
            ? Array.Empty<VersionIdeaConsolidadaCosmosDocument>()
            : (await ConsultarPorTipoAsync<VersionIdeaConsolidadaCosmosDocument>(
                VersionIdeaConsolidadaCosmosDocument.DocumentType, pk, null, cancellationToken))
                .Where(version => ideaIds.Contains(version.IdeaId))
                .ToArray();

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

        foreach (var idea in ideas)
        {
            await _container.DeleteAsync(idea.Id, pk, cancellationToken);
        }

        foreach (var version in versiones)
        {
            await _container.DeleteAsync(version.Id, pk, cancellationToken);
        }

        var rutas = artefactos
            .Select(a => a.BlobPath)
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return new ConteoBorradoRespuestas(respuestas.Count, evaluaciones.Count, artefactos.Count, rutas, ideas.Count, versiones.Length);
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
