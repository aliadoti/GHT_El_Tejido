namespace ElTejido.Api.Errores;

/// <summary>
/// Resultado del mapeo de una excepcion al modelo de errores (04 §3): estado HTTP, codigo,
/// mensaje y detalles. Es independiente de HTTP para poder probarlo en unitarias.
/// </summary>
internal sealed record ResultadoMapeoError(
    int Status,
    string Code,
    string Message,
    IReadOnlyList<CampoErrorRespuesta> Details);
