using Microsoft.Azure.Cosmos;

namespace ElTejido.Infrastructure.Configuracion;

internal sealed class ConfigCosmosContainer : IConfigCosmosContainer
{
    private readonly Container _container;

    public ConfigCosmosContainer(Container container)
    {
        _container = container;
    }

    public async Task UpsertAsync(
        ConfigCosmosDocument document,
        string partitionKey,
        CancellationToken cancellationToken)
    {
        await _container.UpsertItemAsync(
            document,
            new PartitionKey(partitionKey),
            cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyCollection<ConfigCosmosDocument>> QueryAsync(
        string type,
        string? familyId,
        string? estado,
        string? tipoPrompt,
        CancellationToken cancellationToken)
    {
        using var iterator = _container.GetItemQueryIterator<ConfigCosmosDocument>(
            CreateQuery(type, familyId, estado, tipoPrompt));

        var documents = new List<ConfigCosmosDocument>();
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            documents.AddRange(page);
        }

        return documents;
    }

    private static QueryDefinition CreateQuery(string type, string? familyId, string? estado, string? tipoPrompt)
    {
        var filters = new List<string> { "c.type = @type" };
        if (!string.IsNullOrWhiteSpace(familyId))
        {
            filters.Add("c.familiaId = @familyId");
        }

        if (!string.IsNullOrWhiteSpace(estado))
        {
            filters.Add("c.estado = @estado");
        }

        if (!string.IsNullOrWhiteSpace(tipoPrompt))
        {
            filters.Add("c.tipoPrompt = @tipoPrompt");
        }

        var query = new QueryDefinition($"SELECT * FROM c WHERE {string.Join(" AND ", filters)}")
            .WithParameter("@type", type);

        if (!string.IsNullOrWhiteSpace(familyId))
        {
            query.WithParameter("@familyId", familyId);
        }

        if (!string.IsNullOrWhiteSpace(estado))
        {
            query.WithParameter("@estado", estado);
        }

        if (!string.IsNullOrWhiteSpace(tipoPrompt))
        {
            query.WithParameter("@tipoPrompt", tipoPrompt);
        }

        return query;
    }
}
