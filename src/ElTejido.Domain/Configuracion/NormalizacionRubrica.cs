using System.Globalization;
using System.Text;

namespace ElTejido.Domain.Configuracion;

/// <summary>
/// Normalizacion canonica de identificadores y nombres de criterio (DT-RUB-01 §3.1, 03 §3.11).
/// Es pura y compartida por dominio, validador, compilador y rehidratacion legacy para que la
/// unicidad se juzgue siempre con el mismo criterio: sin mayusculas y sin tildes.
/// </summary>
public static class NormalizacionRubrica
{
    /// <summary>Techo de longitud del id; evita claves abusivas sin volverse un numero de negocio.</summary>
    public const int MaxLongitudId = 64;

    /// <summary>
    /// Convierte un texto libre en un id canonico: minusculas, sin diacriticos y con cualquier
    /// caracter no alfanumerico colapsado en un solo guion bajo. Se usa para derivar el id de un
    /// documento historico que solo tiene <c>nombre</c> (03 §3.11, compatibilidad de lectura).
    /// </summary>
    public static string NormalizarId(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return string.Empty;
        }

        var sinTildes = QuitarDiacriticos(valor).ToLowerInvariant();
        var builder = new StringBuilder(sinTildes.Length);
        var separadorPendiente = false;
        foreach (var c in sinTildes)
        {
            if (char.IsAsciiLetterOrDigit(c))
            {
                if (separadorPendiente && builder.Length > 0)
                {
                    builder.Append('_');
                }

                separadorPendiente = false;
                builder.Append(c);
            }
            else
            {
                separadorPendiente = true;
            }
        }

        var normalizado = builder.ToString();
        return normalizado.Length <= MaxLongitudId ? normalizado : normalizado[..MaxLongitudId];
    }

    /// <summary>
    /// <c>true</c> si el id ya viene en forma canonica. Un id que no lo esta se rechaza con
    /// <c>formato_invalido</c> en vez de normalizarse en silencio: el id es una clave estable y el
    /// autor debe ver exactamente la que va a quedar persistida.
    /// </summary>
    public static bool EsIdCanonico(string? valor)
        => !string.IsNullOrEmpty(valor)
            && valor.Length <= MaxLongitudId
            && string.Equals(valor, NormalizarId(valor), StringComparison.Ordinal);

    /// <summary>
    /// Clave de comparacion para detectar nombres visibles duplicados: sin mayusculas, sin tildes y
    /// con espacios colapsados. No se persiste; solo decide unicidad.
    /// </summary>
    public static string ClaveComparacion(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return string.Empty;
        }

        var sinTildes = QuitarDiacriticos(valor).ToLowerInvariant();
        var builder = new StringBuilder(sinTildes.Length);
        var espacioPendiente = false;
        foreach (var c in sinTildes.Trim())
        {
            if (char.IsWhiteSpace(c))
            {
                espacioPendiente = builder.Length > 0;
                continue;
            }

            if (espacioPendiente)
            {
                builder.Append(' ');
                espacioPendiente = false;
            }

            builder.Append(c);
        }

        return builder.ToString();
    }

    private static string QuitarDiacriticos(string texto)
    {
        var descompuesto = texto.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(descompuesto.Length);
        foreach (var c in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
