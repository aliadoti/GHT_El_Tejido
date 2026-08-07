using System.Globalization;
using System.Text;
using ElTejido.Application.Common;

namespace ElTejido.Application.Usuarios.CargaMasiva;

/// <summary>
/// Definicion compartida de la plantilla oficial de GHT (I-08 §3): nombres y orden de las columnas,
/// validacion de cabecera y las conversiones que deben dar el mismo resultado en <c>.xlsx</c> y en
/// <c>.csv</c>. Vive en Application para que el lector de Infraestructura no duplique reglas.
/// </summary>
public static class PlantillaParticipantes
{
    /// <summary>Las 9 columnas, en el orden exacto en que deben venir.</summary>
    public static readonly IReadOnlyList<string> Cabecera =
    [
        "Empresa",
        "ID Empresa",
        "Sede",
        "Nombre",
        "Cargo",
        "Email",
        "Antigüedad en la empresa en años",
        "Idioma",
        "Telefono",
    ];

    public const int IndiceEmpresa = 0;
    public const int IndiceEmpresaId = 1;
    public const int IndiceSede = 2;
    public const int IndiceNombre = 3;
    public const int IndiceCargo = 4;
    public const int IndiceEmail = 5;
    public const int IndiceAntiguedad = 6;
    public const int IndiceIdioma = 7;
    public const int IndiceTelefono = 8;

    /// <summary>
    /// Exige las 9 columnas en orden. La comparacion ignora mayusculas, espacios de sobra y tildes
    /// —Excel y los exportadores CSV las maltratan— pero <b>no</b> el orden ni la ausencia de una
    /// columna: en ese caso el lote entero no se procesa (I-08 §7).
    /// </summary>
    public static void ValidarCabecera(IReadOnlyList<string?> cabecera)
    {
        var valida = cabecera.Count >= Cabecera.Count
            && Cabecera
                .Select((esperada, indice) => SonEquivalentes(cabecera[indice], esperada))
                .All(coincide => coincide);

        if (valida)
        {
            return;
        }

        throw new ErrorValidacion(
            "La cabecera del archivo no coincide con la plantilla oficial: " +
            string.Join(" | ", Cabecera) + ".",
            new[] { new DetalleError("cabecera", "invalida") });
    }

    /// <summary>Recorta y convierte a <c>null</c> lo vacio, que es como viaja toda celda opcional.</summary>
    public static string? Normalizar(string? valor)
        => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    /// <summary>
    /// Interpreta la antiguedad sin redondear. Acepta separador decimal punto o coma (la plantilla
    /// llega en invariante, pero un Excel en es-CO exporta coma). Celda vacia => sin dato;
    /// texto no numerico => <paramref name="ilegible"/>, que el servicio reporta como
    /// <c>antiguedad_invalida</c> en vez de perder el valor en silencio.
    /// </summary>
    public static void ParsearAntiguedad(string? texto, out decimal? valor, out bool ilegible)
    {
        valor = null;
        ilegible = false;

        var crudo = Normalizar(texto);
        if (crudo is null)
        {
            return;
        }

        // Solo se reemplaza la coma cuando actua como separador decimal (no hay punto en el texto).
        var normalizado = crudo.Contains('.') ? crudo : crudo.Replace(',', '.');
        if (decimal.TryParse(normalizado, NumberStyles.Float, CultureInfo.InvariantCulture, out var parseado))
        {
            valor = parseado;
            return;
        }

        ilegible = true;
    }

    private static bool SonEquivalentes(string? actual, string esperada)
        => string.Equals(Plegar(actual), Plegar(esperada), StringComparison.OrdinalIgnoreCase);

    /// <summary>Colapsa espacios y quita tildes para comparar nombres de columna, no para guardarlos.</summary>
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

        return string.Join(
            ' ',
            new string(sinTildes).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }
}
