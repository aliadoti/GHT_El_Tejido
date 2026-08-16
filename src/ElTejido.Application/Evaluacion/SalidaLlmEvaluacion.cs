using System.Text.Json.Serialization;

namespace ElTejido.Application.Evaluacion;

/// <summary>
/// Contrato de salida estructurada del LLM (08 §4, ARQ §6.1). Es el limite que desacopla el sistema
/// del proveedor: el modelo DEBE devolver exactamente esta forma. Se trata como <b>dato no
/// confiable</b> (08 §5.6): se parsea y valida; si no cumple, se descarta a favor del fallback.
/// </summary>
public sealed record SalidaLlmEvaluacion
{
    /// <summary>
    /// DT-RUB-01 (08 §4.1): una entrada por cada <c>criterio_id</c> de la version efectiva, ni una
    /// mas ni una menos. El emparejamiento es por id, nunca por el nombre visible.
    /// </summary>
    [JsonPropertyName("calificaciones")]
    public IReadOnlyList<SalidaCalificacionCriterio>? Calificaciones { get; init; }

    /// <summary>
    /// Compatibilidad: el modelo puede seguir devolviendo un total, pero el servidor lo
    /// <b>ignora</b> para decisiones y persistencia (08 §4.1). Solo alimenta la metrica
    /// <c>total_modelo_difiere</c>, sin texto ni PII.
    /// </summary>
    [JsonPropertyName("calificacion_total")]
    public decimal? CalificacionTotal { get; init; }

    [JsonPropertyName("explicacion")]
    public string? Explicacion { get; init; }

    [JsonPropertyName("retroalimentacion_usuario")]
    public string? RetroalimentacionUsuario { get; init; }

    [JsonPropertyName("parafraseo_devuelto")]
    public string? ParafraseoDevuelto { get; init; }

    [JsonPropertyName("recomendacion")]
    public string? Recomendacion { get; init; }

    [JsonPropertyName("repregunta_sugerida")]
    public string? RepreguntaSugerida { get; init; }

    [JsonPropertyName("temas")]
    public IReadOnlyList<string>? Temas { get; init; }

    [JsonPropertyName("entidades")]
    public IReadOnlyList<string>? Entidades { get; init; }

    [JsonPropertyName("anomalia_seguridad")]
    public bool AnomaliaSeguridad { get; init; }
}

public sealed record SalidaCalificacionCriterio
{
    /// <summary>Clave estable del criterio en la version efectiva (03 §3.11).</summary>
    [JsonPropertyName("criterio_id")]
    public string? CriterioId { get; init; }

    [JsonPropertyName("puntaje")]
    public decimal Puntaje { get; init; }

    [JsonPropertyName("justificacion")]
    public string? Justificacion { get; init; }
}
