namespace ElTejido.Infrastructure.Participantes;

internal interface IParticipantsCosmosContainer
{
    Task UpsertParticipanteAsync(
        ParticipanteCampaniaCosmosDocument document,
        string partitionKey,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ParticipanteCampaniaCosmosDocument>> QueryParticipantesAsync(
        string? campaniaId,
        string? usuarioId,
        string? whatsappNormalizado,
        CancellationToken cancellationToken);

    Task CreateEnvioAsync(
        EnvioMensajeCosmosDocument document,
        string partitionKey,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<EnvioMensajeCosmosDocument>> QueryEnviosAsync(
        string campaniaId,
        string? usuarioId,
        string? tipo,
        string? mensajeInicialId,
        CancellationToken cancellationToken);

    /// <summary>Borra un documento por id dentro de su particion <c>campaniaId</c> (P-15). Tolera 404.</summary>
    Task DeleteAsync(string id, string partitionKey, CancellationToken cancellationToken);
}
