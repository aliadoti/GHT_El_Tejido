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

    [JsonProperty("coachingIdeas", NullValueHandling = NullValueHandling.Ignore)]
    public CoachingIdeasDocument? CoachingIdeas { get; init; }

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
            CoachingIdeas = conversacion.CoachingIdeas is null
                ? null
                : CoachingIdeasDocument.FromDomain(conversacion.CoachingIdeas),
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
            FechaCierre,
            CoachingIdeas?.ToDomain());

    private static string MapearMaquina(EstadoMaquinaConversacion estado)
        => estado switch
        {
            EstadoMaquinaConversacion.EsperandoRespuestaInicial => "esperandoRespuestaInicial",
            EstadoMaquinaConversacion.Evaluando => "evaluando",
            EstadoMaquinaConversacion.EsperandoRepregunta => "esperandoRepregunta",
            EstadoMaquinaConversacion.EsperandoSeleccionIdea => "esperandoSeleccionIdea",
            EstadoMaquinaConversacion.Cerrada => "cerrada",
            _ => throw new InvalidOperationException($"Estado de maquina no soportado: {estado}."),
        };

    private static EstadoMaquinaConversacion MapearMaquina(string estado)
        => estado switch
        {
            "esperandoRespuestaInicial" => EstadoMaquinaConversacion.EsperandoRespuestaInicial,
            "evaluando" => EstadoMaquinaConversacion.Evaluando,
            "esperandoRepregunta" => EstadoMaquinaConversacion.EsperandoRepregunta,
            "esperandoSeleccionIdea" => EstadoMaquinaConversacion.EsperandoSeleccionIdea,
            "cerrada" => EstadoMaquinaConversacion.Cerrada,
            _ => throw new InvalidOperationException($"Estado de maquina no soportado en Cosmos: {estado}."),
        };

    internal sealed class CoachingIdeasDocument
    {
        [JsonProperty("estado")]
        public string Estado { get; init; } = "activo";

        [JsonProperty("respuestaPadreId")]
        public string RespuestaPadreId { get; init; } = string.Empty;

        [JsonProperty("ideaActivaIndice")]
        public int? IdeaActivaIndice { get; init; }

        [JsonProperty("ideas")]
        public IReadOnlyList<IdeaCoachingDocument> Ideas { get; init; } = Array.Empty<IdeaCoachingDocument>();

        public static CoachingIdeasDocument FromDomain(CoachingIdeas cola)
            => new()
            {
                Estado = cola.Estado == EstadoCoachingIdeas.Finalizado ? "finalizado" : "activo",
                RespuestaPadreId = cola.RespuestaPadreId,
                IdeaActivaIndice = cola.IdeaActivaIndice,
                Ideas = cola.Ideas.Select(IdeaCoachingDocument.FromDomain).ToArray(),
            };

        public CoachingIdeas ToDomain()
            => Domain.Conversaciones.CoachingIdeas.Crear(
                Estado == "finalizado" ? EstadoCoachingIdeas.Finalizado : EstadoCoachingIdeas.Activo,
                RespuestaPadreId,
                IdeaActivaIndice,
                Ideas.Select(idea => idea.ToDomain()));
    }

    internal sealed class IdeaCoachingDocument
    {
        [JsonProperty("ideaIndice")]
        public int IdeaIndice { get; init; }

        [JsonProperty("respuestaRaizId")]
        public string RespuestaRaizId { get; init; } = string.Empty;

        [JsonProperty("respuestaVigenteId")]
        public string RespuestaVigenteId { get; init; } = string.Empty;

        [JsonProperty("ideaId", NullValueHandling = NullValueHandling.Ignore)]
        public string? IdeaId { get; init; }

        [JsonProperty("versionIdeaVigenteId", NullValueHandling = NullValueHandling.Ignore)]
        public string? VersionIdeaVigenteId { get; init; }

        [JsonProperty("estado")]
        public string Estado { get; init; } = "pendiente";

        [JsonProperty("motivoFinalizacion")]
        public string? MotivoFinalizacion { get; init; }

        [JsonProperty("repreguntasUsadas")]
        public int RepreguntasUsadas { get; init; }

        [JsonProperty("iniciadaEn")]
        public DateTimeOffset? IniciadaEn { get; init; }

        [JsonProperty("finalizadaEn")]
        public DateTimeOffset? FinalizadaEn { get; init; }

        public static IdeaCoachingDocument FromDomain(IdeaCoaching idea)
            => new()
            {
                IdeaIndice = idea.IdeaIndice,
                RespuestaRaizId = idea.RespuestaRaizId,
                RespuestaVigenteId = idea.RespuestaVigenteId,
                IdeaId = idea.IdeaId,
                VersionIdeaVigenteId = idea.VersionIdeaVigenteId,
                Estado = idea.Estado switch
                {
                    EstadoIdeaCoaching.Activa => "activa",
                    EstadoIdeaCoaching.Finalizada => "finalizada",
                    _ => "pendiente",
                },
                MotivoFinalizacion = idea.MotivoFinalizacion is null
                    ? null
                    : MinusculaInicial(idea.MotivoFinalizacion.Value.ToString()),
                RepreguntasUsadas = idea.RepreguntasUsadas,
                IniciadaEn = idea.IniciadaEn,
                FinalizadaEn = idea.FinalizadaEn,
            };

        public IdeaCoaching ToDomain()
            => IdeaCoaching.Crear(
                IdeaIndice,
                RespuestaRaizId,
                RespuestaVigenteId,
                Estado switch
                {
                    "activa" => EstadoIdeaCoaching.Activa,
                    "finalizada" => EstadoIdeaCoaching.Finalizada,
                    _ => EstadoIdeaCoaching.Pendiente,
                },
                ParseMotivo(MotivoFinalizacion),
                RepreguntasUsadas,
                IniciadaEn,
                FinalizadaEn,
                IdeaId,
                VersionIdeaVigenteId);

        private static MotivoFinalizacionIdea? ParseMotivo(string? motivo)
            => string.IsNullOrWhiteSpace(motivo)
                ? null
                : Enum.Parse<MotivoFinalizacionIdea>(motivo, ignoreCase: true);

        private static string MinusculaInicial(string valor)
            => char.ToLowerInvariant(valor[0]) + valor[1..];
    }
}
