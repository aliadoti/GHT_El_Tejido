namespace ElTejido.Infrastructure.Seguridad;

internal interface ISecurityCosmosContainer
{
    Task UpsertCodigoAsync(
        CodigoAuthAdminCosmosDocument document,
        string partitionKey,
        CancellationToken cancellationToken);

    Task<CodigoAuthAdminCosmosDocument?> QueryCodigoMasRecienteAsync(
        string numero,
        CancellationToken cancellationToken);

    Task CreateLogAsync(
        LogSeguridadCosmosDocument document,
        string partitionKey,
        CancellationToken cancellationToken);
}
