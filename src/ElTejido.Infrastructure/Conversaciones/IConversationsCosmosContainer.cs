namespace ElTejido.Infrastructure.Conversaciones;

/// <summary>
/// Abstraccion del contenedor Cosmos <c>conversations</c> (pk <c>campaniaId</c>) para aislar el SDK
/// en pruebas. Conversacion por upsert; Mensaje append-only (03 §3.6-§3.7).
/// </summary>
internal interface IConversationsCosmosContainer
{
    Task UpsertConversacionAsync(ConversacionCosmosDocument document, string partitionKey, CancellationToken cancellationToken);

    Task<ConversacionCosmosDocument?> ReadConversacionAsync(string id, string partitionKey, CancellationToken cancellationToken);

    Task CreateMensajeAsync(MensajeCosmosDocument document, string partitionKey, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<T>> QueryAsync<T>(
        Microsoft.Azure.Cosmos.QueryDefinition query,
        string partitionKey,
        CancellationToken cancellationToken);

    /// <summary>Consulta sin particion (cross-partition); usada por barridos de bajo volumen.</summary>
    Task<IReadOnlyCollection<T>> QueryCrossPartitionAsync<T>(
        Microsoft.Azure.Cosmos.QueryDefinition query,
        CancellationToken cancellationToken);
}
