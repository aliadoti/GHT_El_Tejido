using System.Globalization;
using System.Text;

namespace ElTejido.Application.Usuarios.CargaMasiva;

/// <summary>
/// Decide si dos nombres sobre el mismo telefono son la misma persona (I-08 §4.4). Un typo no debe
/// inactivar a nadie, y un cambio real de titular no debe pasar como correccion: por eso la
/// comparacion es tolerante pero con un umbral explicito, y todo lo que quede por debajo se le
/// devuelve al admin en vez de resolverse solo.
/// </summary>
public static class ComparadorNombres
{
    /// <summary>Similitud a partir de la cual se considera el mismo nombre con un typo (I-08 §4.4).</summary>
    public const decimal UmbralMismaPersona = 0.85m;

    public static bool EsMismaPersona(string? nombreRegistrado, string? nombreArchivo)
        => Similitud(nombreRegistrado, nombreArchivo) >= UmbralMismaPersona;

    /// <summary>
    /// Levenshtein normalizado (0 = nada que ver, 1 = identicos) sobre los nombres plegados: sin
    /// tildes, sin dobles espacios y en mayusculas, porque la plantilla llega en mayusculas y con
    /// espaciado irregular.
    /// </summary>
    public static decimal Similitud(string? primero, string? segundo)
    {
        var a = Plegar(primero);
        var b = Plegar(segundo);

        if (a.Length == 0 && b.Length == 0)
        {
            return 1m;
        }

        if (a.Length == 0 || b.Length == 0)
        {
            return 0m;
        }

        if (string.Equals(a, b, StringComparison.Ordinal))
        {
            return 1m;
        }

        var distancia = Distancia(a, b);
        var mayor = Math.Max(a.Length, b.Length);
        return 1m - ((decimal)distancia / mayor);
    }

    private static int Distancia(string a, string b)
    {
        // Dos filas en vez de la matriz completa: los nombres son cortos y esto corre por cada fila
        // del lote.
        var anterior = new int[b.Length + 1];
        var actual = new int[b.Length + 1];

        for (var j = 0; j <= b.Length; j++)
        {
            anterior[j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            actual[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var costo = a[i - 1] == b[j - 1] ? 0 : 1;
                actual[j] = Math.Min(
                    Math.Min(actual[j - 1] + 1, anterior[j] + 1),
                    anterior[j - 1] + costo);
            }

            (anterior, actual) = (actual, anterior);
        }

        return anterior[b.Length];
    }

    private static string Plegar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return string.Empty;
        }

        var sinTildes = valor
            .Normalize(NormalizationForm.FormD)
            .Where(caracter => CharUnicodeInfo.GetUnicodeCategory(caracter) != UnicodeCategory.NonSpacingMark)
            .ToArray();

        var colapsado = string.Join(
            ' ',
            new string(sinTildes).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        return colapsado.ToUpperInvariant();
    }
}
