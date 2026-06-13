namespace ElTejido.Infrastructure.Campanas;

internal interface ICampaniasCosmosContainer
{
    Task UpsertAsync(
        CampaniaCosmosDocument document,
        string partitionKey,
        CancellationToken cancellationToken);

    Task<CampaniaCosmosDocument?> ReadByIdAsync(string id, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<CampaniaCosmosDocument>> QueryAsync(
        FiltroCampaniasCosmos filtro,
        CancellationToken cancellationToken);
}
