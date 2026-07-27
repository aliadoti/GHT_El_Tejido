using ElTejido.Domain.Evaluacion;
using Newtonsoft.Json;
using DominioEvaluacion = ElTejido.Domain.Evaluacion.Evaluacion;

namespace ElTejido.Infrastructure.Respuestas;

internal sealed class EvaluacionCosmosDocument
{
    public const string DocumentType = "Evaluacion";

    [JsonProperty("id")]
    public string Id { get; init; } = string.Empty;

    [JsonProperty("type")]
    public string Type { get; init; } = DocumentType;

    [JsonProperty("campaniaId")]
    public string CampaniaId { get; init; } = string.Empty;

    [JsonProperty("respuestaId")]
    public string RespuestaId { get; init; } = string.Empty;

    [JsonProperty("usuarioId")]
    public string UsuarioId { get; init; } = string.Empty;

    [JsonProperty("preguntaId")]
    public string PreguntaId { get; init; } = string.Empty;

    [JsonProperty("rubricaRef")]
    public string RubricaRef { get; init; } = string.Empty;

    [JsonProperty("versionRubrica")]
    public int VersionRubrica { get; init; }

    [JsonProperty("promptRef")]
    public string PromptRef { get; init; } = string.Empty;

    [JsonProperty("versionPrompt")]
    public int VersionPrompt { get; init; }

    [JsonProperty("configLLMRef")]
    public string ConfigLlmRef { get; init; } = string.Empty;

    [JsonProperty("configLLMSnapshot")]
    public ConfigSnapshotDocument ConfigLlmSnapshot { get; init; } = new();

    [JsonProperty("pesosUsados")]
    public Dictionary<string, decimal> PesosUsados { get; init; } = new();

    [JsonProperty("calificacionPorCriterio")]
    public List<CalificacionCriterioDocument> CalificacionPorCriterio { get; init; } = new();

    [JsonProperty("calificacionTotal")]
    public decimal CalificacionTotal { get; init; }

    [JsonProperty("explicacion")]
    public string Explicacion { get; init; } = string.Empty;

    [JsonProperty("retroalimentacionEnviada")]
    public string RetroalimentacionEnviada { get; init; } = string.Empty;

    // I-05: opcional; un documento previo sin el campo conserva null y la retro clásica.
    [JsonProperty("parafraseoDevuelto", NullValueHandling = NullValueHandling.Ignore)]
    public string? ParafraseoDevuelto { get; init; }

    [JsonProperty("recomendacion")]
    public string Recomendacion { get; init; } = "cerrar";

    [JsonProperty("repreguntaSugerida", NullValueHandling = NullValueHandling.Ignore)]
    public string? RepreguntaSugerida { get; init; }

    [JsonProperty("temas")]
    public List<string> Temas { get; init; } = new();

    [JsonProperty("entidades")]
    public List<string> Entidades { get; init; } = new();

    [JsonProperty("anomaliaSeguridad")]
    public bool AnomaliaSeguridad { get; init; }

    [JsonProperty("fecha")]
    public DateTimeOffset Fecha { get; init; }

    // P-10: uso de tokens (aditivo). Ausente en documentos previos -> uso null -> suma 0.
    [JsonProperty("usoTokens", NullValueHandling = NullValueHandling.Ignore)]
    public UsoTokensDocument? UsoTokens { get; init; }

    [JsonProperty("ideaId", NullValueHandling = NullValueHandling.Ignore)]
    public string? IdeaId { get; init; }

    [JsonProperty("versionIdeaId", NullValueHandling = NullValueHandling.Ignore)]
    public string? VersionIdeaId { get; init; }

    [JsonProperty("origenTextoEvaluado", NullValueHandling = NullValueHandling.Ignore)]
    public string? OrigenTextoEvaluado { get; init; }

    [JsonProperty("seedThoughtsSnapshot", NullValueHandling = NullValueHandling.Ignore)]
    public SeedThoughtsSnapshotDocument? SeedThoughtsSnapshot { get; init; }

    public static EvaluacionCosmosDocument FromDomain(DominioEvaluacion evaluacion)
        => new()
        {
            Id = evaluacion.Id,
            Type = DocumentType,
            CampaniaId = evaluacion.CampaniaId,
            RespuestaId = evaluacion.RespuestaId,
            UsuarioId = evaluacion.UsuarioId,
            PreguntaId = evaluacion.PreguntaId,
            RubricaRef = evaluacion.RubricaRef,
            VersionRubrica = evaluacion.VersionRubrica,
            PromptRef = evaluacion.PromptRef,
            VersionPrompt = evaluacion.VersionPrompt,
            ConfigLlmRef = evaluacion.ConfigLlmRef,
            ConfigLlmSnapshot = new ConfigSnapshotDocument
            {
                Proveedor = evaluacion.ConfigLlmSnapshot.Proveedor,
                Modelo = evaluacion.ConfigLlmSnapshot.Modelo,
                Endpoint = evaluacion.ConfigLlmSnapshot.Endpoint,
                Parametros = new Dictionary<string, object?>(evaluacion.ConfigLlmSnapshot.Parametros),
            },
            PesosUsados = new Dictionary<string, decimal>(evaluacion.PesosUsados),
            CalificacionPorCriterio = evaluacion.CalificacionPorCriterio
                .Select(c => new CalificacionCriterioDocument
                {
                    Criterio = c.Criterio,
                    Puntaje = c.Puntaje,
                    Justificacion = c.Justificacion,
                })
                .ToList(),
            CalificacionTotal = evaluacion.CalificacionTotal,
            Explicacion = evaluacion.Explicacion,
            RetroalimentacionEnviada = evaluacion.RetroalimentacionEnviada,
            ParafraseoDevuelto = evaluacion.ParafraseoDevuelto,
            Recomendacion = MapearRecomendacion(evaluacion.Recomendacion),
            RepreguntaSugerida = evaluacion.RepreguntaSugerida,
            Temas = evaluacion.Temas.ToList(),
            Entidades = evaluacion.Entidades.ToList(),
            AnomaliaSeguridad = evaluacion.AnomaliaSeguridad,
            Fecha = evaluacion.Fecha,
            UsoTokens = evaluacion.UsoTokens is null
                ? null
                : new UsoTokensDocument
                {
                    PromptTokens = evaluacion.UsoTokens.PromptTokens,
                    CompletionTokens = evaluacion.UsoTokens.CompletionTokens,
                },
            IdeaId = evaluacion.IdeaId,
            VersionIdeaId = evaluacion.VersionIdeaId,
            OrigenTextoEvaluado = evaluacion.OrigenTextoEvaluado,
            SeedThoughtsSnapshot = evaluacion.SeedThoughtsSnapshot is null ? null : new SeedThoughtsSnapshotDocument
            {
                Usadas = evaluacion.SeedThoughtsSnapshot.Usadas,
                Contenido = evaluacion.SeedThoughtsSnapshot.Contenido.ToList(),
                Truncadas = evaluacion.SeedThoughtsSnapshot.Truncadas,
            },
        };

    public DominioEvaluacion ToDomain()
        => DominioEvaluacion.Crear(
            Id,
            CampaniaId,
            RespuestaId,
            UsuarioId,
            PreguntaId,
            RubricaRef,
            VersionRubrica,
            PromptRef,
            VersionPrompt,
            ConfigLlmRef,
            new ConfigLlmSnapshot(
                ConfigLlmSnapshot.Proveedor,
                ConfigLlmSnapshot.Modelo,
                ConfigLlmSnapshot.Endpoint,
                ConfigLlmSnapshot.Parametros),
            PesosUsados,
            CalificacionPorCriterio
                .Select(c => CalificacionCriterio.Crear(c.Criterio, c.Puntaje, c.Justificacion))
                .ToArray(),
            CalificacionTotal,
            Explicacion,
            RetroalimentacionEnviada,
            MapearRecomendacion(Recomendacion),
            RepreguntaSugerida,
            Temas,
            Entidades,
            AnomaliaSeguridad,
            Fecha,
            UsoTokens is null ? null : UsoTokensLlm.Crear(UsoTokens.PromptTokens, UsoTokens.CompletionTokens),
            ParafraseoDevuelto,
            IdeaId,
            VersionIdeaId,
            OrigenTextoEvaluado,
            SeedThoughtsSnapshot is null
                ? null
                : new SeedThoughtsSnapshot(SeedThoughtsSnapshot.Usadas, SeedThoughtsSnapshot.Contenido, SeedThoughtsSnapshot.Truncadas));

    private static string MapearRecomendacion(RecomendacionEvaluacion recomendacion)
        => recomendacion == RecomendacionEvaluacion.Repreguntar ? "repreguntar" : "cerrar";

    private static RecomendacionEvaluacion MapearRecomendacion(string recomendacion)
        => recomendacion == "repreguntar" ? RecomendacionEvaluacion.Repreguntar : RecomendacionEvaluacion.Cerrar;

    internal sealed class ConfigSnapshotDocument
    {
        [JsonProperty("proveedor")]
        public string Proveedor { get; init; } = string.Empty;

        [JsonProperty("modelo")]
        public string Modelo { get; init; } = string.Empty;

        [JsonProperty("endpoint")]
        public string Endpoint { get; init; } = string.Empty;

        [JsonProperty("parametros")]
        public Dictionary<string, object?> Parametros { get; init; } = new();
    }

    internal sealed class CalificacionCriterioDocument
    {
        [JsonProperty("criterio")]
        public string Criterio { get; init; } = string.Empty;

        [JsonProperty("puntaje")]
        public decimal Puntaje { get; init; }

        [JsonProperty("justificacion")]
        public string Justificacion { get; init; } = string.Empty;
    }

    internal sealed class UsoTokensDocument
    {
        [JsonProperty("promptTokens")]
        public int PromptTokens { get; init; }

        [JsonProperty("completionTokens")]
        public int CompletionTokens { get; init; }
    }

    internal sealed class SeedThoughtsSnapshotDocument
    {
        [JsonProperty("usadas")]
        public bool Usadas { get; init; }

        [JsonProperty("contenido")]
        public List<string> Contenido { get; init; } = new();

        [JsonProperty("truncadas")]
        public bool Truncadas { get; init; }
    }
}
