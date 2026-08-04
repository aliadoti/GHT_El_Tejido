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

    public async Task<int> ContarClasificacionesIntencionControlUsuarioAsync(
        string campaniaId,
        string usuarioId,
        DateTimeOffset? desde,
        CancellationToken cancellationToken)
    {
        var consulta = "SELECT VALUE COUNT(1) FROM c WHERE c.type = @type AND c.pk = @pk " +
                       "AND c.tipoEvento = @tipoEvento AND c.esLlamadaLlm = true " +
                       "AND c.campaniaId = @campaniaId AND c.usuarioId = @usuarioId";
        if (desde is not null)
        {
            consulta += " AND c.timestamp >= @desde";
        }

        var query = new QueryDefinition(consulta)
            .WithParameter("@type", LogSeguridadCosmosDocument.DocumentType)
            .WithParameter("@pk", LogSeguridadCosmosDocument.PartitionKeyValue)
            .WithParameter("@tipoEvento", "clasificacionIntencionControl")
            .WithParameter("@campaniaId", campaniaId)
            .WithParameter("@usuarioId", usuarioId);
        if (desde is not null)
        {
            query.WithParameter("@desde", desde.Value);
        }

        using var iterator = _container.GetItemQueryIterator<int>(
            query,
            requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(LogSeguridadCosmosDocument.PartitionKeyValue) });
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            return page.FirstOrDefault();
        }

        return 0;
    }

    public async Task<long> SumarTokensClasificacionesIntencionControlCampaniaAsync(
        string campaniaId,
        CancellationToken cancellationToken)
    {
        var query = new QueryDefinition(
                "SELECT VALUE SUM(c.promptTokens + c.completionTokens) FROM c WHERE c.type = @type AND c.pk = @pk " +
                "AND c.tipoEvento = @tipoEvento AND c.esLlamadaLlm = true AND c.campaniaId = @campaniaId")
            .WithParameter("@type", LogSeguridadCosmosDocument.DocumentType)
            .WithParameter("@pk", LogSeguridadCosmosDocument.PartitionKeyValue)
            .WithParameter("@tipoEvento", "clasificacionIntencionControl")
            .WithParameter("@campaniaId", campaniaId);

        using var iterator = _container.GetItemQueryIterator<long?>(
            query,
            requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(LogSeguridadCosmosDocument.PartitionKeyValue) });
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            return page.FirstOrDefault() ?? 0L;
        }

        return 0L;
    }
}
