namespace ElTejido.Infrastructure.WhatsApp;

internal interface ILeasesCosmosContainer
{
    Task<bool> TryCreateAsync(
        WebhookDedupeCosmosDocument document,
        string partitionKey,
        CancellationToken cancellationToken);
}
