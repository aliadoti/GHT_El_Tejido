using Microsoft.Azure.Cosmos;

namespace ElTejido.Infrastructure.Participantes;

internal sealed class ParticipantsCosmosContainer : IParticipantsCosmosContainer
{
    private readonly Container _container;

    public ParticipantsCosmosContainer(Container container)
    {
        _container = container;
    }

    public async Task UpsertParticipanteAsync(
        ParticipanteCampaniaCosmosDocument document,
        string partitionKey,
        CancellationToken cancellationToken)
    {
        await _container.UpsertItemAsync(
            document,
            new PartitionKey(partitionKey),
            cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyCollection<ParticipanteCampaniaCosmosDocument>> QueryParticipantesAsync(
        string? campaniaId,
        string? usuarioId,
        string? whatsappNormalizado,
        CancellationToken cancellationToken)
    {
        var filters = new List<string> { "c.type = @type" };

        if (campaniaId is not null)
        {
            filters.Add("c.campaniaId = @campaniaId");
        }

        if (usuarioId is not null)
        {
            filters.Add("c.usuarioId = @usuarioId");
        }

        if (whatsappNormalizado is not null)
        {
            filters.Add("c.whatsappNormalizado = @whatsappNormalizado");
        }

        var query = new QueryDefinition($"SELECT * FROM c WHERE {string.Join(" AND ", filters)}")
            .WithParameter("@type", ParticipanteCampaniaCosmosDocument.DocumentType);

        if (campaniaId is not null)
        {
            query.WithParameter("@campaniaId", campaniaId);
        }

        if (usuarioId is not null)
        {
            query.WithParameter("@usuarioId", usuarioId);
        }

        if (whatsappNormalizado is not null)
        {
            query.WithParameter("@whatsappNormalizado", whatsappNormalizado);
        }

        return await ReadAllAsync<ParticipanteCampaniaCosmosDocument>(query, campaniaId, cancellationToken);
    }

    public async Task CreateEnvioAsync(
        EnvioMensajeCosmosDocument document,
        string partitionKey,
        CancellationToken cancellationToken)
    {
        await _container.CreateItemAsync(
            document,
            new PartitionKey(partitionKey),
            cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyCollection<EnvioMensajeCosmosDocument>> QueryEnviosAsync(
        string campaniaId,
        string? usuarioId,
        string? tipo,
        string? mensajeInicialId,
        CancellationToken cancellationToken)
    {
        var filters = new List<string>
        {
            "c.type = @type",
            "c.campaniaId = @campaniaId",
        };

        if (usuarioId is not null)
        {
            filters.Add("c.usuarioId = @usuarioId");
        }

        if (tipo is not null)
        {
            filters.Add("c.tipo = @tipo");
        }

        if (mensajeInicialId is not null)
        {
            filters.Add("c.mensajeInicialId = @mensajeInicialId");
        }

        var query = new QueryDefinition($"SELECT * FROM c WHERE {string.Join(" AND ", filters)}")
            .WithParameter("@type", EnvioMensajeCosmosDocument.DocumentType)
            .WithParameter("@campaniaId", campaniaId);

        if (usuarioId is not null)
        {
            query.WithParameter("@usuarioId", usuarioId);
        }

        if (tipo is not null)
        {
            query.WithParameter("@tipo", tipo);
        }

        if (mensajeInicialId is not null)
        {
            query.WithParameter("@mensajeInicialId", mensajeInicialId);
        }

        return await ReadAllAsync<EnvioMensajeCosmosDocument>(query, campaniaId, cancellationToken);
    }

    private async Task<IReadOnlyCollection<T>> ReadAllAsync<T>(
        QueryDefinition query,
        string? partitionKey,
        CancellationToken cancellationToken)
    {
        var options = partitionKey is null
            ? null
            : new QueryRequestOptions { PartitionKey = new PartitionKey(partitionKey) };

        using var iterator = _container.GetItemQueryIterator<T>(query, requestOptions: options);

        var documents = new List<T>();
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            documents.AddRange(page);
        }

        return documents;
    }
}
