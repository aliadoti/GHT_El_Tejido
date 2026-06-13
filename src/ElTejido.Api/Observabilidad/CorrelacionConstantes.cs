namespace ElTejido.Api.Observabilidad;

/// <summary>
/// Constantes de correlacion de peticiones (04 §8, 10 §6.2): nombre de cabecera, clave en
/// <c>HttpContext.Items</c> y prefijo del identificador generado.
/// </summary>
internal static class CorrelacionConstantes
{
    public const string Header = "X-Correlation-Id";
    public const string ClaveItems = "CorrelationId";
    public const string Prefijo = "corr_";
}
