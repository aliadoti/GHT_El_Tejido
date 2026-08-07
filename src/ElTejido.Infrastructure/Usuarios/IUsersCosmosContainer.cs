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

    /// <summary>Lee el contador de la particion <c>secuencia</c> (03 §3.1.1). Devuelve null si aun no existe.</summary>
    Task<SecuenciaCosmosDocument?> ReadSecuenciaAsync(string id, CancellationToken cancellationToken);

    /// <summary>
    /// Guarda el contador con concurrencia optimista: <paramref name="etag"/> nulo crea el documento y
    /// falla con 409 si otro lo creo primero; con etag actualiza con <c>If-Match</c> y falla con 412 si
    /// alguien lo movio. En ambos casos el llamador reintenta releyendo (03 §3.1.1).
    /// </summary>
    Task GuardarSecuenciaAsync(
        SecuenciaCosmosDocument document,
        string? etag,
        CancellationToken cancellationToken);
}
