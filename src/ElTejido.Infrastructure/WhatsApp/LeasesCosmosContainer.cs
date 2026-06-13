using System.Net;
using Microsoft.Azure.Cosmos;

namespace ElTejido.Infrastructure.WhatsApp;

internal sealed class LeasesCosmosContainer : ILeasesCosmosContainer
{
    private readonly Container _container;

    public LeasesCosmosContainer(Container container)
    {
        _container = container;
    }

    public async Task<bool> TryCreateAsync(
        WebhookDedupeCosmosDocument document,
        string partitionKey,
        CancellationToken cancellationToken)
    {
        try
        {
            await _container.CreateItemAsync(
                document,
                new PartitionKey(partitionKey),
                cancellationToken: cancellationToken);

            return true;
        }
        catch (CosmosException exception) when (exception.StatusCode == HttpStatusCode.Conflict)
        {
            return false;
        }
    }
}
