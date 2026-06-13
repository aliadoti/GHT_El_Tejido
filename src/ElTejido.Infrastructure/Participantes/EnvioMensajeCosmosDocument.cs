using ElTejido.Domain.Identidad;
using ElTejido.Domain.Participantes;
using Newtonsoft.Json;

namespace ElTejido.Infrastructure.Participantes;

internal sealed class EnvioMensajeCosmosDocument
{
    public const string DocumentType = "EnvioMensaje";

    [JsonProperty("id")]
    public string Id { get; init; } = string.Empty;

    [JsonProperty("type")]
    public string Type { get; init; } = DocumentType;

    [JsonProperty("campaniaId")]
    public string CampaniaId { get; init; } = string.Empty;

    [JsonProperty("usuarioId")]
    public string UsuarioId { get; init; } = string.Empty;

    [JsonProperty("mensajeInicialId", NullValueHandling = NullValueHandling.Ignore)]
    public string? MensajeInicialId { get; init; }

    [JsonProperty("numero")]
    public string Numero { get; init; } = string.Empty;

    [JsonProperty("estadoEnvio")]
    public string EstadoEnvio { get; init; } = string.Empty;

    [JsonProperty("tipo")]
    public string Tipo { get; init; } = string.Empty;

    [JsonProperty("whatsappMessageId", NullValueHandling = NullValueHandling.Ignore)]
    public string? WhatsappMessageId { get; init; }

    [JsonProperty("fechaEnvio")]
    public DateTimeOffset FechaEnvio { get; init; }

    [JsonProperty("error")]
    public string? Error { get; init; }

    public static EnvioMensajeCosmosDocument FromDomain(EnvioMensaje envio)
    {
        return new EnvioMensajeCosmosDocument
        {
            Id = envio.Id,
            Type = DocumentType,
            CampaniaId = envio.CampaniaId,
            UsuarioId = envio.UsuarioId,
            MensajeInicialId = envio.MensajeInicialId,
            Numero = envio.Numero.Valor,
            EstadoEnvio = CosmosEnumMaps.FromEstadoEnvio(envio.EstadoEnvio),
            Tipo = CosmosEnumMaps.FromTipoEnvio(envio.Tipo),
            WhatsappMessageId = envio.WhatsappMessageId,
            FechaEnvio = envio.FechaEnvio,
            Error = envio.Error,
        };
    }

    public EnvioMensaje ToDomain()
    {
        return EnvioMensaje.Crear(
            Id,
            CampaniaId,
            UsuarioId,
            MensajeInicialId,
            NumeroWhatsApp.FromNormalized(Numero),
            CosmosEnumMaps.ToEstadoEnvio(EstadoEnvio),
            CosmosEnumMaps.ToTipoEnvio(Tipo),
            WhatsappMessageId,
            FechaEnvio,
            Error);
    }
}
