using Microsoft.Azure.Cosmos;

namespace ElTejido.Infrastructure.Seguridad;

internal sealed class SecurityCosmosContainer : ISecurityCosmosContainer
{
    private readonly Container _container;

    public SecurityCosmosContainer(Container container)
    {
        _container = container;
    }

    public async Task UpsertCodigoAsync(
        CodigoAuthAdminCosmosDocument document,
        string partitionKey,
        CancellationToken cancellationToken)
    {
        await _container.UpsertItemAsync(
            document,
            new PartitionKey(partitionKey),
            cancellationToken: cancellationToken);
    }

    public async Task<CodigoAuthAdminCosmosDocument?> QueryCodigoMasRecienteAsync(
        string numero,
        CancellationToken cancellationToken)
    {
        var query = new QueryDefinition(
                "SELECT TOP 1 * FROM c WHERE c.type = @type AND c.pk = @pk AND c.numero = @numero " +
                "ORDER BY c.creadoEn DESC")
            .WithParameter("@type", CodigoAuthAdminCosmosDocument.DocumentType)
            .WithParameter("@pk", CodigoAuthAdminCosmosDocument.PartitionKeyValue)
            .WithParameter("@numero", numero);

        using var iterator = _container.GetItemQueryIterator<CodigoAuthAdminCosmosDocument>(
            query,
            requestOptions: new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(CodigoAuthAdminCosmosDocument.PartitionKeyValue),
            });

        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            var document = page.FirstOrDefault();
            if (document is not null)
            {
                return document;
            }
        }

        return null;
    }

    public async Task CreateLogAsync(
        LogSeguridadCosmosDocument document,
        string partitionKey,
        CancellationToken cancellationToken)
    {
        await _container.CreateItemAsync(
            document,
            new PartitionKey(partitionKey),
            cancellationToken: cancellationToken);
    }
}
