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
            // Bug confirmado (descubierto al implementar I-17): estos eventos ya en uso (P-03/I-01/I-06/
            // I-09) no estaban mapeados y lanzaban al persistir en Cosmos. No habian estallado porque sus
            // features estan con flag OFF; la clasificacion de madurez (I-17) es siempre-on y expone el
            // hueco. Se completan todos los valores del enum. Ver AVANCES.md 2026-07-22.
            TipoEventoSeguridad.AccionAdministrativa => "accionAdministrativa",
            TipoEventoSeguridad.CierreUmbralAnticipado => "cierreUmbralAnticipado",
            TipoEventoSeguridad.SegmentacionIdeas => "segmentacionIdeas",
            TipoEventoSeguridad.TejidoColectivo => "tejidoColectivo",
            TipoEventoSeguridad.ClasificacionMadurez => "clasificacionMadurez",
            _ => throw new InvalidOperationException($"Tipo de evento de seguridad no soportado: {tipo}."),
        };
    }
}
