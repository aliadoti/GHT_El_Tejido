using ElTejido.Domain.Identidad;
using ElTejido.Domain.Participantes;

namespace ElTejido.Application.Participantes;

/// <summary>
/// Puerto de persistencia del contenedor Cosmos <c>participants</c> para
/// ParticipanteCampania y EnvioMensaje (partition key <c>campaniaId</c>).
/// Cubre REQ §14, §29.4, §29.6, la idempotencia de envio saliente de 03 §4 y ARQ §8-§9.
/// </summary>
public interface IRepositorioParticipantes
{
    Task GuardarParticipanteAsync(ParticipanteCampania participante, CancellationToken cancellationToken);

    Task<ParticipanteCampania?> ObtenerParticipantePorNumeroAsync(
        string campaniaId,
        NumeroWhatsApp numero,
        CancellationToken cancellationToken);

    Task<ParticipanteCampania?> ObtenerParticipantePorUsuarioAsync(
        string campaniaId,
        string usuarioId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ParticipanteCampania>> ListarParticipantesAsync(
        string campaniaId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Busca, cruzando particiones, todos los participantes asociados a un numero.
    /// Lo usa la resolucion de participante (06 §3.2 paso 5) para elegir la campania activa.
    /// </summary>
    Task<IReadOnlyCollection<ParticipanteCampania>> BuscarParticipantesPorNumeroAsync(
        NumeroWhatsApp numero,
        CancellationToken cancellationToken);

    /// <summary>
    /// Registra (append-only) un mensaje saliente. Cubre ARQ §13.
    /// </summary>
    Task RegistrarEnvioAsync(EnvioMensaje envio, CancellationToken cancellationToken);

    /// <summary>
    /// Idempotencia de envio saliente (03 §4): verifica si ya existe un envio
    /// para la clave (campaniaId, usuarioId, tipo, mensajeInicialId).
    /// </summary>
    Task<bool> ExisteEnvioAsync(
        string campaniaId,
        string usuarioId,
        TipoEnvioMensaje tipo,
        string? mensajeInicialId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<EnvioMensaje>> ListarEnviosAsync(
        string campaniaId,
        CancellationToken cancellationToken);
}
