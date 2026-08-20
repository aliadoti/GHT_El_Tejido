namespace ElTejido.Application.Conversacion;

/// <summary>
/// P-33: reconoce una petición pura de ver la idea. A diferencia de los detectores genéricos no
/// acepta subfrases: un aporte con contenido siempre conserva su ruta normal.
/// </summary>
public sealed class DetectorConsultaIdea
{
    private readonly HashSet<string> _frases;
    private readonly int _maxCaracteres;

    public DetectorConsultaIdea(IEnumerable<string>? frases, int maxCaracteres)
    {
        _frases = (frases ?? Array.Empty<string>())
            .Select(DetectorIntencionContinuar.Normalizar)
            .Where(x => x.Length > 0)
            .ToHashSet(StringComparer.Ordinal);
        _maxCaracteres = maxCaracteres > 0 ? maxCaracteres : 0;
    }

    public static readonly IReadOnlyCollection<string> FrasesPorDefecto = new[]
    {
        "dime como va escrita mi idea hasta ahora", "muestrame mi idea",
        "como va mi idea", "quiero ver mi idea", "dime mi idea",
    };

    public static readonly IReadOnlyCollection<string> FrasesAcusePorDefecto = new[]
    {
        "gracias", "muchas gracias", "ok", "entendido", "perfecto",
    };

    public static readonly IReadOnlyCollection<string> FrasesNuevaIdeaPorDefecto = new[]
    {
        "nueva idea", "tengo otra idea", "quiero otra idea",
    };

    public bool Coincide(string? texto)
    {
        var normalizado = DetectorIntencionContinuar.Normalizar(texto);
        return _maxCaracteres > 0
            && normalizado.Length > 0
            && normalizado.Length <= _maxCaracteres
            && (_frases.Contains(normalizado) || EsPatronInequivoco(normalizado));
    }

    /// <summary>
    /// P-33 §4.1: reconoce equivalentes inequívocos aunque una versión de catálogo activa todavía no
    /// enumere cada variante natural. Se conserva coincidencia de mensaje completo para que una
    /// corrección que además diga "mi idea" nunca se pierda como consulta de solo lectura.
    /// </summary>
    private static bool EsPatronInequivoco(string normalizado)
        => normalizado is "how is my idea coming along so far";

    public static bool EsAcuse(string? texto, IEnumerable<string> frases)
        => CoincideExacto(texto, frases);

    public static bool EsNuevaIdea(string? texto, IEnumerable<string> frases)
        => CoincideExacto(texto, frases);

    private static bool CoincideExacto(string? texto, IEnumerable<string> frases)
    {
        var normalizado = DetectorIntencionContinuar.Normalizar(texto);
        return normalizado.Length > 0 && frases
            .Select(DetectorIntencionContinuar.Normalizar)
            .Contains(normalizado, StringComparer.Ordinal);
    }
}
