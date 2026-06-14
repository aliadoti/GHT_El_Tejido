namespace ElTejido.Infrastructure.Configuracion;

internal interface IConfigCosmosContainer
{
    Task UpsertAsync(ConfigCosmosDocument document, string partitionKey, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ConfigCosmosDocument>> QueryAsync(
        string type,
        string? familyId,
        string? estado,
        string? tipoPrompt,
        CancellationToken cancellationToken);
}
