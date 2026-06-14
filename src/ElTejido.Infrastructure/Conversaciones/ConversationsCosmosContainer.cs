using System.Net;
using Microsoft.Azure.Cosmos;

namespace ElTejido.Infrastructure.Conversaciones;

internal sealed class ConversationsCosmosContainer : IConversationsCosmosContainer
{
    private readonly Container _container;

    public ConversationsCosmosContainer(Container container)
    {
        _container = container;
    }

    public async Task UpsertConversacionAsync(
        ConversacionCosmosDocument document,
        string partitionKey,
        CancellationToken cancellationToken)
    {
        await _container.UpsertItemAsync(document, new PartitionKey(partitionKey), cancellationToken: cancellationToken);
    }

    public async Task<ConversacionCosmosDocument?> ReadConversacionAsync(
        string id,
        string partitionKey,
        CancellationToken cancellationToken)
    {
        try
        {
            var respuesta = await _container.ReadItemAsync<ConversacionCosmosDocument>(
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

    public async Task CreateMensajeAsync(
        MensajeCosmosDocument document,
        string partitionKey,
        CancellationToken cancellationToken)
    {
        await _container.CreateItemAsync(document, new PartitionKey(partitionKey), cancellationToken: cancellationToken);
    }
}
