using System.Net;
using Microsoft.Azure.Cosmos;

namespace ElTejido.Infrastructure.Campanas;

internal sealed class CampaniasCosmosContainer : ICampaniasCosmosContainer
{
    private readonly Container _container;

    public CampaniasCosmosContainer(Container container)
    {
        _container = container;
    }

    public async Task UpsertAsync(
        CampaniaCosmosDocument document,
        string partitionKey,
        CancellationToken cancellationToken)
    {
        await _container.UpsertItemAsync(
            document,
            new PartitionKey(partitionKey),
            cancellationToken: cancellationToken);
    }

    public async Task<CampaniaCosmosDocument?> ReadByIdAsync(string id, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _container.ReadItemAsync<CampaniaCosmosDocument>(
                id,
                new PartitionKey(id),
                cancellationToken: cancellationToken);

            return response.Resource;
        }
        catch (CosmosException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken)
    {
        try
        {
            await _container.DeleteItemAsync<CampaniaCosmosDocument>(
                id,
                new PartitionKey(id),
                cancellationToken: cancellationToken);
        }
        catch (CosmosException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            // Idempotente: ya estaba borrada.
        }
    }

    public async Task<IReadOnlyCollection<CampaniaCosmosDocument>> QueryAsync(
        FiltroCampaniasCosmos filtro,
        CancellationToken cancellationToken)
    {
        using var iterator = _container.GetItemQueryIterator<CampaniaCosmosDocument>(
            CreateQueryDefinition(filtro));

        var documents = new List<CampaniaCosmosDocument>();
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            documents.AddRange(page);
        }

        return documents;
    }

    private static QueryDefinition CreateQueryDefinition(FiltroCampaniasCosmos filtro)
    {
        var filters = new List<string> { "c.type = @type" };

        if (filtro.Estado is not null)
        {
            filters.Add("c.estado = @estado");
        }

        if (filtro.Busqueda is not null)
        {
            filters.Add("(CONTAINS(LOWER(c.nombre), @busqueda) OR CONTAINS(LOWER(c.descripcion), @busqueda))");
        }

        var query = new QueryDefinition($"SELECT * FROM c WHERE {string.Join(" AND ", filters)}")
            .WithParameter("@type", CampaniaCosmosDocument.DocumentType);

        if (filtro.Estado is not null)
        {
            query.WithParameter("@estado", filtro.Estado);
        }

        if (filtro.Busqueda is not null)
        {
            query.WithParameter("@busqueda", filtro.Busqueda.ToLowerInvariant());
        }

        return query;
    }
}
