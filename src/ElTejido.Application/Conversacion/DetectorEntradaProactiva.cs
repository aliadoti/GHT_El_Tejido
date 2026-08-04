using System.Globalization;
using System.Text;

namespace ElTejido.Application.Conversacion;

/// <summary>
/// P-28: reconoce exclusivamente saludos o peticiones breves de iniciar cuando el servidor ya
/// comprobó que no hay flujo activo. No clasifica aportes ni toma decisiones de alcance.
/// </summary>
public sealed class DetectorEntradaProactiva
{
    public static readonly IReadOnlyList<string> FrasesPorDefecto = new[]
    {
        "hola", "buenas", "buenos dias", "buenas tardes", "buenas noches",
        "quiero participar", "quiero empezar", "quiero comenzar", "quiero iniciar",
        "quiero continuar", "como participo", "como empiezo",
    };

    private readonly HashSet<string> _frases;
    private readonly int _maxCaracteres;

    public DetectorEntradaProactiva(IEnumerable<string> frases, int maxCaracteres)
    {
        _frases = frases.Select(Normalizar).Where(f => f.Length > 0).ToHashSet(StringComparer.Ordinal);
        _maxCaracteres = Math.Max(1, maxCaracteres);
    }

    public bool Coincide(string? texto)
    {
        var normalizado = Normalizar(texto);
        return normalizado.Length > 0
            && normalizado.Length <= _maxCaracteres
            && _frases.Contains(normalizado);
    }

    private static string Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return string.Empty;
        var descompuesto = texto.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sinAcentos = new string(descompuesto.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
        var limpio = new StringBuilder(sinAcentos.Length);
        foreach (var caracter in sinAcentos)
        {
            limpio.Append(char.IsLetterOrDigit(caracter) ? caracter : ' ');
        }

        return string.Join(' ', limpio.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
