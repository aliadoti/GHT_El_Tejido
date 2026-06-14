using ElTejido.Domain.Conversaciones;
using Newtonsoft.Json;

namespace ElTejido.Infrastructure.Conversaciones;

internal sealed class ConversacionCosmosDocument
{
    public const string DocumentType = "Conversacion";

    [JsonProperty("id")]
    public string Id { get; init; } = string.Empty;

    [JsonProperty("type")]
    public string Type { get; init; } = DocumentType;

    [JsonProperty("campaniaId")]
    public string CampaniaId { get; init; } = string.Empty;

    [JsonProperty("usuarioId")]
    public string UsuarioId { get; init; } = string.Empty;

    [JsonProperty("preguntaId")]
    public string PreguntaId { get; init; } = string.Empty;

    [JsonProperty("canal")]
    public string Canal { get; init; } = "whatsapp";

    [JsonProperty("estado")]
    public string Estado { get; init; } = "abierta";

    [JsonProperty("estadoMaquina")]
    public string EstadoMaquina { get; init; } = "esperandoRespuestaInicial";

    [JsonProperty("repreguntasUsadas")]
    public int RepreguntasUsadas { get; init; }

    [JsonProperty("ventanaServicioVenceEn")]
    public DateTimeOffset VentanaServicioVenceEn { get; init; }

    [JsonProperty("correlationId", NullValueHandling = NullValueHandling.Ignore)]
    public string? CorrelationId { get; init; }

    [JsonProperty("fechaInicio")]
    public DateTimeOffset FechaInicio { get; init; }

    [JsonProperty("fechaCierre", NullValueHandling = NullValueHandling.Ignore)]
    public DateTimeOffset? FechaCierre { get; init; }

    public static ConversacionCosmosDocument FromDomain(Conversacion conversacion)
        => new()
        {
            Id = conversacion.Id,
            Type = DocumentType,
            CampaniaId = conversacion.CampaniaId,
            UsuarioId = conversacion.UsuarioId,
            PreguntaId = conversacion.PreguntaId,
            Canal = conversacion.Canal,
            Estado = conversacion.Estado == EstadoConversacion.Cerrada ? "cerrada" : "abierta",
            EstadoMaquina = MapearMaquina(conversacion.EstadoMaquina),
            RepreguntasUsadas = conversacion.RepreguntasUsadas,
            VentanaServicioVenceEn = conversacion.VentanaServicioVenceEn,
            CorrelationId = conversacion.CorrelationId,
            FechaInicio = conversacion.FechaInicio,
            FechaCierre = conversacion.FechaCierre,
        };

    public Conversacion ToDomain()
        => Conversacion.Crear(
            Id,
            CampaniaId,
            UsuarioId,
            PreguntaId,
            Canal,
            Estado == "cerrada" ? EstadoConversacion.Cerrada : EstadoConversacion.Abierta,
            MapearMaquina(EstadoMaquina),
            RepreguntasUsadas,
            VentanaServicioVenceEn,
            CorrelationId,
            FechaInicio,
            FechaCierre);

    private static string MapearMaquina(EstadoMaquinaConversacion estado)
        => estado switch
        {
            EstadoMaquinaConversacion.EsperandoRespuestaInicial => "esperandoRespuestaInicial",
            EstadoMaquinaConversacion.Evaluando => "evaluando",
            EstadoMaquinaConversacion.EsperandoRepregunta => "esperandoRepregunta",
            EstadoMaquinaConversacion.Cerrada => "cerrada",
            _ => throw new InvalidOperationException($"Estado de maquina no soportado: {estado}."),
        };

    private static EstadoMaquinaConversacion MapearMaquina(string estado)
        => estado switch
        {
            "esperandoRespuestaInicial" => EstadoMaquinaConversacion.EsperandoRespuestaInicial,
            "evaluando" => EstadoMaquinaConversacion.Evaluando,
            "esperandoRepregunta" => EstadoMaquinaConversacion.EsperandoRepregunta,
            "cerrada" => EstadoMaquinaConversacion.Cerrada,
            _ => throw new InvalidOperationException($"Estado de maquina no soportado en Cosmos: {estado}."),
        };
}
