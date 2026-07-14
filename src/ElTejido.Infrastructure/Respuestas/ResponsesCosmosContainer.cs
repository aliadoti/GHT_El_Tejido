using System.Net;
using Microsoft.Azure.Cosmos;

namespace ElTejido.Infrastructure.Respuestas;

internal sealed class ResponsesCosmosContainer : IResponsesCosmosContainer
{
    private readonly Container _container;

    public ResponsesCosmosContainer(Container container)
    {
        _container = container;
    }

    public async Task UpsertAsync<T>(T document, string partitionKey, CancellationToken cancellationToken)
    {
        await _container.UpsertItemAsync(
            document,
            new PartitionKey(partitionKey),
            cancellationToken: cancellationToken);
    }

    public async Task<T?> ReadByIdAsync<T>(string id, string partitionKey, CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            var respuesta = await _container.ReadItemAsync<T>(
                id,
                new PartitionKey(partitionKey),
                cancellationToken: cancellationToken);
            return respuesta.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task DeleteAsync(string id, string partitionKey, CancellationToken cancellationToken)
    {
        try
        {
            await _container.DeleteItemAsync<object>(id, new PartitionKey(partitionKey), cancellationToken: cancellationToken);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // Idempotente: si otro barrido ya lo borro, no es un error.
        }
    }

    public async Task<IReadOnlyCollection<T>> QueryAsync<T>(
        QueryDefinition query,
        string partitionKey,
        CancellationToken cancellationToken)
    {
        using var iterator = _container.GetItemQueryIterator<T>(
            query,
            requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(partitionKey) });

        var documents = new List<T>();
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            documents.AddRange(page);
        }

        return documents;
    }
}
