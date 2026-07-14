using Microsoft.Azure.Cosmos;

namespace ElTejido.Infrastructure.Respuestas;

/// <summary>
/// Abstraccion del contenedor Cosmos <c>responses</c> (pk <c>campaniaId</c>) para aislar el SDK en
/// pruebas. Aloja Respuesta, Evaluacion y ArtefactoMarkdown (03 §3.8-§3.10).
/// </summary>
internal interface IResponsesCosmosContainer
{
    Task UpsertAsync<T>(T document, string partitionKey, CancellationToken cancellationToken);

    Task<T?> ReadByIdAsync<T>(string id, string partitionKey, CancellationToken cancellationToken)
        where T : class;

    /// <summary>Borra un documento por id dentro de su particion (P-03). Tolera 404 (ya borrado).</summary>
    Task DeleteAsync(string id, string partitionKey, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<T>> QueryAsync<T>(
        QueryDefinition query,
        string partitionKey,
        CancellationToken cancellationToken);
}
