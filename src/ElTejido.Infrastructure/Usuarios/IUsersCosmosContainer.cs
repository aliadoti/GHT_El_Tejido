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

    /// <summary>Borra un usuario por id dentro de su particion fija <c>usuario</c> (P-15). Tolera 404.</summary>
    Task DeleteUsuarioAsync(string id, CancellationToken cancellationToken);
}
