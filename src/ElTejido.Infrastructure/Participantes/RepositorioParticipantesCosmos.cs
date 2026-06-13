using ElTejido.Application.Participantes;
using ElTejido.Domain.Identidad;
using ElTejido.Domain.Participantes;
using Microsoft.Azure.Cosmos;

namespace ElTejido.Infrastructure.Participantes;

/// <summary>
/// Adaptador Cosmos del contenedor participants para ParticipanteCampania y EnvioMensaje.
/// Cubre REQ §14, §29.4, §29.6, la idempotencia de envio saliente de 03 §4 y ARQ §8-§9.
/// </summary>
public sealed class RepositorioParticipantesCosmos : IRepositorioParticipantes
{
    private readonly IParticipantsCosmosContainer _container;

    public RepositorioParticipantesCosmos(Container container)
        : this(new ParticipantsCosmosContainer(container))
    {
    }

    internal RepositorioParticipantesCosmos(IParticipantsCosmosContainer container)
    {
        _container = container;
    }

    public async Task GuardarParticipanteAsync(
        ParticipanteCampania participante,
        CancellationToken cancellationToken)
    {
        var document = ParticipanteCampaniaCosmosDocument.FromDomain(participante);
        await _container.UpsertParticipanteAsync(document, document.CampaniaId, cancellationToken);
    }

    public async Task<ParticipanteCampania?> ObtenerParticipantePorNumeroAsync(
        string campaniaId,
        NumeroWhatsApp numero,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaniaId);

        var documents = await _container.QueryParticipantesAsync(
            campaniaId.Trim(),
            null,
            numero.Valor,
            cancellationToken);

        return documents.FirstOrDefault()?.ToDomain();
    }

    public async Task<ParticipanteCampania?> ObtenerParticipantePorUsuarioAsync(
        string campaniaId,
        string usuarioId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaniaId);
        ArgumentException.ThrowIfNullOrWhiteSpace(usuarioId);

        var documents = await _container.QueryParticipantesAsync(
            campaniaId.Trim(),
            usuarioId.Trim(),
            null,
            cancellationToken);

        return documents.FirstOrDefault()?.ToDomain();
    }

    public async Task<IReadOnlyCollection<ParticipanteCampania>> ListarParticipantesAsync(
        string campaniaId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaniaId);

        var documents = await _container.QueryParticipantesAsync(
            campaniaId.Trim(),
            null,
            null,
            cancellationToken);

        return documents.Select(document => document.ToDomain()).ToArray();
    }

    public async Task<IReadOnlyCollection<ParticipanteCampania>> BuscarParticipantesPorNumeroAsync(
        NumeroWhatsApp numero,
        CancellationToken cancellationToken)
    {
        var documents = await _container.QueryParticipantesAsync(
            null,
            null,
            numero.Valor,
            cancellationToken);

        return documents.Select(document => document.ToDomain()).ToArray();
    }

    public async Task RegistrarEnvioAsync(EnvioMensaje envio, CancellationToken cancellationToken)
    {
        var document = EnvioMensajeCosmosDocument.FromDomain(envio);
        await _container.CreateEnvioAsync(document, document.CampaniaId, cancellationToken);
    }

    public async Task<bool> ExisteEnvioAsync(
        string campaniaId,
        string usuarioId,
        TipoEnvioMensaje tipo,
        string? mensajeInicialId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaniaId);
        ArgumentException.ThrowIfNullOrWhiteSpace(usuarioId);

        var documents = await _container.QueryEnviosAsync(
            campaniaId.Trim(),
            usuarioId.Trim(),
            CosmosEnumMaps.FromTipoEnvio(tipo),
            string.IsNullOrWhiteSpace(mensajeInicialId) ? null : mensajeInicialId.Trim(),
            cancellationToken);

        return documents.Count > 0;
    }

    public async Task<IReadOnlyCollection<EnvioMensaje>> ListarEnviosAsync(
        string campaniaId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaniaId);

        var documents = await _container.QueryEnviosAsync(
            campaniaId.Trim(),
            null,
            null,
            null,
            cancellationToken);

        return documents.Select(document => document.ToDomain()).ToArray();
    }
}
