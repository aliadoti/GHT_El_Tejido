using System.Globalization;
using System.Text;

namespace ElTejido.Application.Conversacion;

/// <summary>
/// Deteccion deterministica de la intencion del participante de <b>continuar</b> a la siguiente
/// pregunta sin seguir puliendo su respuesta (05 §4.4). Es la mitad "hibrida" de la salida
/// conversacional: primero se intenta este match barato de frases; si no coincide, el orquestador
/// trata el mensaje como una version mejorada y lo evalua como siempre.
/// <para>
/// Para evitar falsos positivos sobre una respuesta mejorada larga (que podria contener por casualidad
/// una frase de continuar), una coincidencia por contencion solo cuenta si el mensaje es <b>corto</b>
/// (<= <c>maxCaracteres</c>). Una igualdad exacta siempre cuenta. La comparacion ignora mayusculas,
/// acentos y puntuacion.
/// </para>
/// </summary>
public sealed class DetectorIntencionContinuar
{
    private readonly string[] _frasesNormalizadas;
    private readonly int _maxCaracteres;

    public DetectorIntencionContinuar(IEnumerable<string>? frases, int maxCaracteres)
    {
        _frasesNormalizadas = (frases ?? Array.Empty<string>())
            .Select(Normalizar)
            .Where(frase => frase.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        _maxCaracteres = maxCaracteres > 0 ? maxCaracteres : 0;
    }

    /// <summary>Frases por defecto (en lenguaje natural; se normalizan al construir el detector).</summary>
    public static readonly IReadOnlyList<string> FrasesPorDefecto = new[]
    {
        "listo",
        "sigamos",
        "continuemos",
        "continuar",
        "siguiente pregunta",
        "asi esta bien",
        "esta bien asi",
        "asi quedo bien",
        "asi lo dejo",
        "lo dejo asi",
        "estoy conforme",
        "ya estoy conforme",
        "ya quedo",
        "no quiero mejorar",
    };

    public bool DeseaContinuar(string? texto)
    {
        if (_frasesNormalizadas.Length == 0 || string.IsNullOrWhiteSpace(texto))
        {
            return false;
        }

        var normalizado = Normalizar(texto);
        if (normalizado.Length == 0)
        {
            return false;
        }

        var esCorto = _maxCaracteres > 0 && normalizado.Length <= _maxCaracteres;
        foreach (var frase in _frasesNormalizadas)
        {
            if (normalizado == frase)
            {
                return true;
            }

            if (esCorto && ContienePalabraCompleta(normalizado, frase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContienePalabraCompleta(string texto, string frase)
        // Limites de palabra con espacios centinela: " asi esta bien " dentro de " ... ".
        => (" " + texto + " ").Contains(" " + frase + " ", StringComparison.Ordinal);

    private static string Normalizar(string texto)
    {
        var sinAcentos = new StringBuilder(texto.Length);
        foreach (var caracter in texto.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD))
        {
            var categoria = CharUnicodeInfo.GetUnicodeCategory(caracter);
            if (categoria == UnicodeCategory.NonSpacingMark)
            {
                continue; // descarta el diacritico (tilde, dieresis)
            }

            if (char.IsLetterOrDigit(caracter))
            {
                sinAcentos.Append(caracter);
            }
            else if (char.IsWhiteSpace(caracter))
            {
                sinAcentos.Append(' ');
            }

            // Cualquier otro signo (puntuacion, emoji) se descarta.
        }

        return string.Join(' ', sinAcentos.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
