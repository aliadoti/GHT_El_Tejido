using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ElTejido.Domain.Configuracion;

namespace ElTejido.Application.Evaluacion;

/// <summary>
/// Salvaguarda determinista <b>siempre activa</b> (no es un feature configurable) que evita que la
/// salida del LLM revele la rubrica al participante (I-03, REQ §21, 08 §5). Es la capa 2 de defensa:
/// la capa 1 es la prohibicion explicita en el prompt (<see cref="ConstructorMensajesEvaluacion"/>);
/// esta capa verifica el texto ya generado contra una lista negra derivada de los nombres de criterio
/// de la rubrica activa, mas patrones de puntaje ("3/5", "3 de 5") y palabras que delatan el
/// mecanismo de evaluacion.
/// </summary>
public static class FiltroSalidaRubrica
{
    private static readonly Regex PatronPuntaje = new(
        @"\b\d+(\.\d+)?\s*(/|de)\s*\d+(\.\d+)?\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly string[] PalabrasProhibidas = ["rubrica", "criterio", "criterios", "calificacion"];

    /// <summary>
    /// Devuelve <c>true</c> si <paramref name="texto"/> nombra un criterio de <paramref name="rubrica"/>,
    /// contiene un patron de puntaje o menciona el mecanismo de evaluacion ("rubrica"/"criterio"/
    /// "calificacion"). Texto vacio o nulo nunca es una fuga.
    /// </summary>
    public static bool ContieneFuga(string? texto, Rubrica rubrica)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return false;
        }

        var normalizado = Normalizar(texto);

        foreach (var criterio in rubrica.Criterios)
        {
            if (ContienePalabra(normalizado, Normalizar(criterio.Nombre)))
            {
                return true;
            }
        }

        foreach (var palabra in PalabrasProhibidas)
        {
            if (ContienePalabra(normalizado, palabra))
            {
                return true;
            }
        }

        return PatronPuntaje.IsMatch(normalizado);
    }

    private static bool ContienePalabra(string normalizado, string palabra)
        => !string.IsNullOrWhiteSpace(palabra)
            && Regex.IsMatch(normalizado, $@"\b{Regex.Escape(palabra)}\b", RegexOptions.IgnoreCase);

    /// <summary>Minusculas y sin diacriticos, para no depender de tildes ("calificación" vs "calificacion").</summary>
    private static string Normalizar(string texto)
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

        return builder.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }
}
