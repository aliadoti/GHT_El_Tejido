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
}
