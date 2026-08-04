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

    Task<int> ContarClasificacionesIntencionControlUsuarioAsync(
        string campaniaId,
        string usuarioId,
        DateTimeOffset? desde,
        CancellationToken cancellationToken);

    Task<long> SumarTokensClasificacionesIntencionControlCampaniaAsync(
        string campaniaId,
        CancellationToken cancellationToken);
}
