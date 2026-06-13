using ElTejido.Domain.Seguridad;
using Newtonsoft.Json;

namespace ElTejido.Infrastructure.Seguridad;

internal sealed class LogSeguridadCosmosDocument
{
    public const string DocumentType = "LogSeguridad";
    public const string PartitionKeyValue = "LogSeguridad";

    [JsonProperty("id")]
    public string Id { get; init; } = string.Empty;

    [JsonProperty("type")]
    public string Type { get; init; } = DocumentType;

    [JsonProperty("pk")]
    public string Pk { get; init; } = PartitionKeyValue;

    [JsonProperty("tipoEvento")]
    public string TipoEvento { get; init; } = string.Empty;

    [JsonProperty("usuarioId")]
    public string? UsuarioId { get; init; }

    [JsonProperty("numero", NullValueHandling = NullValueHandling.Ignore)]
    public string? Numero { get; init; }

    [JsonProperty("resultado")]
    public string Resultado { get; init; } = string.Empty;

    [JsonProperty("detalle", NullValueHandling = NullValueHandling.Ignore)]
    public string? Detalle { get; init; }

    [JsonProperty("correlationId", NullValueHandling = NullValueHandling.Ignore)]
    public string? CorrelationId { get; init; }

    [JsonProperty("timestamp")]
    public DateTimeOffset Timestamp { get; init; }

    public static LogSeguridadCosmosDocument FromDomain(LogSeguridad log)
    {
        return new LogSeguridadCosmosDocument
        {
            Id = log.Id,
            Type = DocumentType,
            Pk = PartitionKeyValue,
            TipoEvento = MapTipoEvento(log.TipoEvento),
            UsuarioId = log.UsuarioId,
            Numero = log.Numero,
            Resultado = log.Resultado,
            Detalle = log.Detalle,
            CorrelationId = log.CorrelationId,
            Timestamp = log.Timestamp,
        };
    }

    public static string MapTipoEvento(TipoEventoSeguridad tipo)
    {
        return tipo switch
        {
            TipoEventoSeguridad.SolicitudOtp => "solicitudOtp",
            TipoEventoSeguridad.LoginExitoso => "loginExitoso",
            TipoEventoSeguridad.LoginFallido => "loginFallido",
            TipoEventoSeguridad.RechazoParticipacion => "rechazoParticipacion",
            TipoEventoSeguridad.RateLimit => "rateLimit",
            TipoEventoSeguridad.AnomaliaLlm => "anomaliaLlm",
            TipoEventoSeguridad.PromptInjectionSospechoso => "promptInjectionSospechoso",
            TipoEventoSeguridad.ErrorEnvio => "errorEnvio",
            _ => throw new InvalidOperationException($"Tipo de evento de seguridad no soportado: {tipo}."),
        };
    }
}
