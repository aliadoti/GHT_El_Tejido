using ElTejido.Domain.Common;

namespace ElTejido.Domain.Evaluacion;

/// <summary>
/// Evaluacion de una respuesta producida por el LLM (contenedor <c>responses</c>, 03 §3.9, REQ §20).
/// Guarda <b>snapshots de version</b> (rubrica, prompt, configLLM, pesos) para reproducibilidad
/// (ARQ §8.3). El contenido lo arma el modulo de Evaluacion (08); la persistencia la realiza el
/// orquestador (05 §4.3).
/// </summary>
public sealed class Evaluacion
{
    private Evaluacion(
        string id,
        string campaniaId,
        string respuestaId,
        string usuarioId,
        string preguntaId,
        string rubricaRef,
        int versionRubrica,
        string promptRef,
        int versionPrompt,
        string configLlmRef,
        ConfigLlmSnapshot configLlmSnapshot,
        IReadOnlyDictionary<string, decimal> pesosUsados,
        IReadOnlyCollection<CalificacionCriterio> calificacionPorCriterio,
        decimal calificacionTotal,
        string explicacion,
        string retroalimentacionEnviada,
        string? parafraseoDevuelto,
        RecomendacionEvaluacion recomendacion,
        string? repreguntaSugerida,
        IReadOnlyCollection<string> temas,
        IReadOnlyCollection<string> entidades,
        bool anomaliaSeguridad,
        DateTimeOffset fecha,
        UsoTokensLlm? usoTokens,
        string? ideaId,
        string? versionIdeaId,
        string? origenTextoEvaluado,
        SeedThoughtsSnapshot? seedThoughtsSnapshot)
    {
        Id = id;
        CampaniaId = campaniaId;
        RespuestaId = respuestaId;
        UsuarioId = usuarioId;
        PreguntaId = preguntaId;
        RubricaRef = rubricaRef;
        VersionRubrica = versionRubrica;
        PromptRef = promptRef;
        VersionPrompt = versionPrompt;
        ConfigLlmRef = configLlmRef;
        ConfigLlmSnapshot = configLlmSnapshot;
        PesosUsados = pesosUsados;
        CalificacionPorCriterio = calificacionPorCriterio;
        CalificacionTotal = calificacionTotal;
        Explicacion = explicacion;
        RetroalimentacionEnviada = retroalimentacionEnviada;
        ParafraseoDevuelto = parafraseoDevuelto;
        Recomendacion = recomendacion;
        RepreguntaSugerida = repreguntaSugerida;
        Temas = temas;
        Entidades = entidades;
        AnomaliaSeguridad = anomaliaSeguridad;
        Fecha = fecha;
        UsoTokens = usoTokens;
        IdeaId = ideaId;
        VersionIdeaId = versionIdeaId;
        OrigenTextoEvaluado = origenTextoEvaluado;
        SeedThoughtsSnapshot = seedThoughtsSnapshot;
    }

    public string Id { get; }

    public string CampaniaId { get; }

    public string RespuestaId { get; }

    public string UsuarioId { get; }

    public string PreguntaId { get; }

    public string RubricaRef { get; }

    public int VersionRubrica { get; }

    public string PromptRef { get; }

    public int VersionPrompt { get; }

    public string ConfigLlmRef { get; }

    public ConfigLlmSnapshot ConfigLlmSnapshot { get; }

    public IReadOnlyDictionary<string, decimal> PesosUsados { get; }

    public IReadOnlyCollection<CalificacionCriterio> CalificacionPorCriterio { get; }

    public decimal CalificacionTotal { get; }

    public string Explicacion { get; }

    public string RetroalimentacionEnviada { get; }

    /// <summary>
    /// I-05: resumen fiel y breve que el modelo devolvio para transparentar lo entendido. Es
    /// opcional para mantener compatibles las evaluaciones y documentos anteriores.
    /// </summary>
    public string? ParafraseoDevuelto { get; }

    public RecomendacionEvaluacion Recomendacion { get; }

    public string? RepreguntaSugerida { get; }

    public IReadOnlyCollection<string> Temas { get; }

    public IReadOnlyCollection<string> Entidades { get; }

    public bool AnomaliaSeguridad { get; }

    public DateTimeOffset Fecha { get; }

    /// <summary>P-10 — tokens consumidos por esta evaluación (null si el proveedor no lo reportó o doc previo).</summary>
    public UsoTokensLlm? UsoTokens { get; }

    /// <summary>I-19: relación opcional con la idea y versión canónica evaluada.</summary>
    public string? IdeaId { get; }
    public string? VersionIdeaId { get; }
    public string? OrigenTextoEvaluado { get; }
    public SeedThoughtsSnapshot? SeedThoughtsSnapshot { get; }

    public static Evaluacion Crear(
        string id,
        string campaniaId,
        string respuestaId,
        string usuarioId,
        string preguntaId,
        string rubricaRef,
        int versionRubrica,
        string promptRef,
        int versionPrompt,
        string configLlmRef,
        ConfigLlmSnapshot configLlmSnapshot,
        IReadOnlyDictionary<string, decimal>? pesosUsados,
        IEnumerable<CalificacionCriterio>? calificacionPorCriterio,
        decimal calificacionTotal,
        string explicacion,
        string retroalimentacionEnviada,
        RecomendacionEvaluacion recomendacion,
        string? repreguntaSugerida,
        IEnumerable<string>? temas,
        IEnumerable<string>? entidades,
        bool anomaliaSeguridad,
        DateTimeOffset fecha,
        UsoTokensLlm? usoTokens = null,
        string? parafraseoDevuelto = null,
        string? ideaId = null,
        string? versionIdeaId = null,
        string? origenTextoEvaluado = null,
        SeedThoughtsSnapshot? seedThoughtsSnapshot = null)
    {
        if (versionRubrica <= 0 || versionPrompt <= 0)
        {
            throw new DomainValidationException(
                "VERSION_EVALUACION_INVALIDA",
                "Las versiones de rubrica y prompt deben ser mayores que cero.");
        }

        var repregunta = string.IsNullOrWhiteSpace(repreguntaSugerida) ? null : repreguntaSugerida.Trim();
        if (recomendacion == RecomendacionEvaluacion.Repreguntar && repregunta is null)
        {
            throw new DomainValidationException(
                "REPREGUNTA_REQUERIDA",
                "Una recomendacion de repreguntar exige una repregunta sugerida.");
        }

        if (string.IsNullOrWhiteSpace(ideaId) != string.IsNullOrWhiteSpace(versionIdeaId))
        {
            throw new DomainValidationException(
                "TRAZABILIDAD_EVALUACION_IDEA_INCOMPLETA",
                "ideaId y versionIdeaId deben informarse juntos.");
        }

        if (!string.IsNullOrWhiteSpace(origenTextoEvaluado)
            && origenTextoEvaluado != "ideaConsolidada")
        {
            throw new DomainValidationException(
                "ORIGEN_TEXTO_EVALUADO_INVALIDO",
                "El origen de texto evaluado no es válido.");
        }

        return new Evaluacion(
            DomainGuards.Required(id, nameof(id)),
            DomainGuards.Required(campaniaId, nameof(campaniaId)),
            DomainGuards.Required(respuestaId, nameof(respuestaId)),
            DomainGuards.Required(usuarioId, nameof(usuarioId)),
            DomainGuards.Required(preguntaId, nameof(preguntaId)),
            DomainGuards.Required(rubricaRef, nameof(rubricaRef)),
            versionRubrica,
            DomainGuards.Required(promptRef, nameof(promptRef)),
            versionPrompt,
            DomainGuards.Required(configLlmRef, nameof(configLlmRef)),
            configLlmSnapshot,
            pesosUsados is null
                ? new Dictionary<string, decimal>()
                : new Dictionary<string, decimal>(pesosUsados),
            (calificacionPorCriterio ?? Array.Empty<CalificacionCriterio>()).ToArray(),
            calificacionTotal,
            DomainGuards.Required(explicacion, nameof(explicacion)),
            DomainGuards.Required(retroalimentacionEnviada, nameof(retroalimentacionEnviada)),
            string.IsNullOrWhiteSpace(parafraseoDevuelto) ? null : parafraseoDevuelto.Trim(),
            recomendacion,
            repregunta,
            NormalizarLista(temas),
            NormalizarLista(entidades),
            anomaliaSeguridad,
            fecha.ToUniversalTime(),
            usoTokens,
            Normalizar(ideaId),
            Normalizar(versionIdeaId),
            Normalizar(origenTextoEvaluado),
            seedThoughtsSnapshot);
    }

    /// <summary>I-19: adjunta la procedencia canónica cuando un adaptador de evaluación no la provee.</summary>
    public Evaluacion ConProcedenciaIdea(string ideaId, string versionIdeaId, SeedThoughtsSnapshot? seedThoughtsSnapshot = null)
        => Crear(
            Id, CampaniaId, RespuestaId, UsuarioId, PreguntaId, RubricaRef, VersionRubrica, PromptRef,
            VersionPrompt, ConfigLlmRef, ConfigLlmSnapshot, PesosUsados, CalificacionPorCriterio,
            CalificacionTotal, Explicacion, RetroalimentacionEnviada, Recomendacion, RepreguntaSugerida,
            Temas, Entidades, AnomaliaSeguridad, Fecha, UsoTokens, ParafraseoDevuelto, ideaId,
            versionIdeaId, "ideaConsolidada", seedThoughtsSnapshot ?? SeedThoughtsSnapshot);

    private static IReadOnlyCollection<string> NormalizarLista(IEnumerable<string>? valores)
        => valores is null
            ? Array.Empty<string>()
            : valores
                .Select(valor => valor.Trim())
                .Where(valor => valor.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

    private static string? Normalizar(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
