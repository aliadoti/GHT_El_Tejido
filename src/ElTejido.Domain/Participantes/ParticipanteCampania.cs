using ElTejido.Domain.Common;
using ElTejido.Domain.Identidad;

namespace ElTejido.Domain.Participantes;

/// <summary>
/// Participante asociado a una campania (contenedor participants, partition key campaniaId).
/// Cubre 03 seccion 3.4 y REQ 29.4.
/// </summary>
public sealed class ParticipanteCampania
{
    private ParticipanteCampania(
        string id,
        string campaniaId,
        string usuarioId,
        NumeroWhatsApp whatsappNormalizado,
        EstadoRegistro estado,
        EstadoEnvio estadoEnvio,
        EstadoRespuestaParticipante estadoRespuesta,
        DateTimeOffset fechaInclusion,
        DateTimeOffset? fechaPrimerEnvio,
        DateTimeOffset? fechaUltimaRespuesta)
    {
        Id = id;
        CampaniaId = campaniaId;
        UsuarioId = usuarioId;
        WhatsappNormalizado = whatsappNormalizado;
        Estado = estado;
        EstadoEnvio = estadoEnvio;
        EstadoRespuesta = estadoRespuesta;
        FechaInclusion = fechaInclusion;
        FechaPrimerEnvio = fechaPrimerEnvio;
        FechaUltimaRespuesta = fechaUltimaRespuesta;
    }

    public string Id { get; }

    public string CampaniaId { get; }

    public string UsuarioId { get; }

    public NumeroWhatsApp WhatsappNormalizado { get; }

    public EstadoRegistro Estado { get; }

    public EstadoEnvio EstadoEnvio { get; }

    public EstadoRespuestaParticipante EstadoRespuesta { get; }

    public DateTimeOffset FechaInclusion { get; }

    public DateTimeOffset? FechaPrimerEnvio { get; }

    public DateTimeOffset? FechaUltimaRespuesta { get; }

    public static ParticipanteCampania Crear(
        string id,
        string campaniaId,
        string usuarioId,
        NumeroWhatsApp whatsappNormalizado,
        EstadoRegistro estado,
        EstadoEnvio estadoEnvio,
        EstadoRespuestaParticipante estadoRespuesta,
        DateTimeOffset fechaInclusion,
        DateTimeOffset? fechaPrimerEnvio,
        DateTimeOffset? fechaUltimaRespuesta)
    {
        return new ParticipanteCampania(
            DomainGuards.Required(id, nameof(id)),
            DomainGuards.Required(campaniaId, nameof(campaniaId)),
            DomainGuards.Required(usuarioId, nameof(usuarioId)),
            whatsappNormalizado,
            estado,
            estadoEnvio,
            estadoRespuesta,
            fechaInclusion.ToUniversalTime(),
            fechaPrimerEnvio?.ToUniversalTime(),
            fechaUltimaRespuesta?.ToUniversalTime());
    }
}
