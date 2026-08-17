using ElTejido.Domain.Common;

namespace ElTejido.Domain.Localizacion;

/// <summary>
/// Idioma editorial interno de la conversación. Los códigos de canal (por ejemplo, <c>es_CO</c>)
/// pertenecen a sus adaptadores y no forman parte de este tipo.
/// </summary>
public sealed record IdiomaConversacion
{
    public const string CodigoEspanol = "es";
    public const string CodigoIngles = "en";

    private static readonly string[] Codigos = [CodigoEspanol, CodigoIngles];

    private IdiomaConversacion(string codigo)
    {
        Codigo = codigo;
    }

    public string Codigo { get; }

    public static IdiomaConversacion Espanol { get; } = new(CodigoEspanol);

    public static IdiomaConversacion Ingles { get; } = new(CodigoIngles);

    public static IReadOnlyCollection<string> CodigosSoportados { get; } = Array.AsReadOnly(Codigos);

    public static IdiomaConversacion Crear(string codigo)
    {
        if (!TryCrear(codigo, out var idioma))
        {
            throw new DomainValidationException(
                "IDIOMA_NO_SOPORTADO",
                "El idioma de conversación debe ser 'es' o 'en'.");
        }

        return idioma;
    }

    public static bool TryCrear(string? codigo, out IdiomaConversacion idioma)
    {
        var normalizado = codigo?.Trim().ToLowerInvariant();
        idioma = normalizado switch
        {
            CodigoEspanol => Espanol,
            CodigoIngles => Ingles,
            _ => null!,
        };

        return idioma is not null;
    }

    /// <summary>
    /// Aplica el único default histórico: un campo ausente al cruzar una frontera de lectura equivale
    /// a español. Un valor presente pero no soportado continúa siendo inválido.
    /// </summary>
    public static IdiomaConversacion DesdeFronteraHistorica(string? codigo)
        => string.IsNullOrWhiteSpace(codigo) ? Espanol : Crear(codigo);

    public override string ToString() => Codigo;
}
