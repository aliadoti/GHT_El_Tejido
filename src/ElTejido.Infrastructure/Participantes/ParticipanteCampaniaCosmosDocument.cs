using ElTejido.Domain.Common;
using ElTejido.Domain.Identidad;
using ElTejido.Domain.Participantes;
using Newtonsoft.Json;

namespace ElTejido.Infrastructure.Participantes;

internal sealed class ParticipanteCampaniaCosmosDocument
{
    public const string DocumentType = "ParticipanteCampania";

    [JsonProperty("id")]
    public string Id { get; init; } = string.Empty;

    [JsonProperty("type")]
    public string Type { get; init; } = DocumentType;

    [JsonProperty("campaniaId")]
    public string CampaniaId { get; init; } = string.Empty;

    [JsonProperty("usuarioId")]
    public string UsuarioId { get; init; } = string.Empty;

    [JsonProperty("whatsappNormalizado")]
    public string WhatsappNormalizado { get; init; } = string.Empty;

    [JsonProperty("estado")]
    public string Estado { get; init; } = string.Empty;

    [JsonProperty("estadoEnvio")]
    public string EstadoEnvio { get; init; } = string.Empty;

    [JsonProperty("estadoRespuesta")]
    public string EstadoRespuesta { get; init; } = string.Empty;

    [JsonProperty("fechaInclusion")]
    public DateTimeOffset FechaInclusion { get; init; }

    [JsonProperty("fechaPrimerEnvio", NullValueHandling = NullValueHandling.Ignore)]
    public DateTimeOffset? FechaPrimerEnvio { get; init; }

    [JsonProperty("fechaUltimaRespuesta", NullValueHandling = NullValueHandling.Ignore)]
    public DateTimeOffset? FechaUltimaRespuesta { get; init; }

    public static ParticipanteCampaniaCosmosDocument FromDomain(ParticipanteCampania participante)
    {
        return new ParticipanteCampaniaCosmosDocument
        {
            Id = participante.Id,
            Type = DocumentType,
            CampaniaId = participante.CampaniaId,
            UsuarioId = participante.UsuarioId,
            WhatsappNormalizado = participante.WhatsappNormalizado.Valor,
            Estado = CosmosEnumMaps.FromEstadoRegistro(participante.Estado),
            EstadoEnvio = CosmosEnumMaps.FromEstadoEnvio(participante.EstadoEnvio),
            EstadoRespuesta = CosmosEnumMaps.FromEstadoRespuesta(participante.EstadoRespuesta),
            FechaInclusion = participante.FechaInclusion,
            FechaPrimerEnvio = participante.FechaPrimerEnvio,
            FechaUltimaRespuesta = participante.FechaUltimaRespuesta,
        };
    }

    public ParticipanteCampania ToDomain()
    {
        return ParticipanteCampania.Crear(
            Id,
            CampaniaId,
            UsuarioId,
            NumeroWhatsApp.FromNormalized(WhatsappNormalizado),
            CosmosEnumMaps.ToEstadoRegistro(Estado),
            CosmosEnumMaps.ToEstadoEnvio(EstadoEnvio),
            CosmosEnumMaps.ToEstadoRespuesta(EstadoRespuesta),
            FechaInclusion,
            FechaPrimerEnvio,
            FechaUltimaRespuesta);
    }
}
