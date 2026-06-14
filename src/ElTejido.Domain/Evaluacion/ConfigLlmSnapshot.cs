namespace ElTejido.Domain.Evaluacion;

/// <summary>
/// Instantanea de la configuracion LLM efectiva usada en una evaluacion (03 §3.9), para
/// reproducibilidad (ARQ §8.3). <b>Nunca</b> incluye la API key ni su referencia; solo proveedor,
/// modelo, endpoint y parametros (REQ §25.3.7).
/// </summary>
public sealed record ConfigLlmSnapshot(
    string Proveedor,
    string Modelo,
    string Endpoint,
    IReadOnlyDictionary<string, object?> Parametros);
