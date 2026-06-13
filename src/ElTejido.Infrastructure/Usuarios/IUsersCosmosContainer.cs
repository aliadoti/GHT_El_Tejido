namespace ElTejido.Infrastructure.Usuarios;

internal interface IUsersCosmosContainer
{
    Task UpsertUsuarioAsync(
        UsuarioCosmosDocument document,
        string partitionKey,
        CancellationToken cancellationToken);

    Task<UsuarioCosmosDocument?> ReadUsuarioByIdAsync(string id, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<UsuarioCosmosDocument>> QueryUsuariosAsync(
        FiltroUsuariosCosmos filtro,
        CancellationToken cancellationToken);

    Task UpsertTagAsync(
        TagCosmosDocument document,
        string partitionKey,
        CancellationToken cancellationToken);

    Task<TagCosmosDocument?> ReadTagByIdAsync(string id, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<TagCosmosDocument>> QueryTagsAsync(
        FiltroTagsCosmos filtro,
        CancellationToken cancellationToken);
}
