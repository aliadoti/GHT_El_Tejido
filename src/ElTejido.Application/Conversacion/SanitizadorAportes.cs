using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ElTejido.Application.Conversacion;

/// <summary>
/// Sanitización determinista y server-side de los aportes del tejido colectivo (I-09, 08 §3.2/§5.9,
/// 10 §5). Dos capas complementarias:
/// <list type="number">
/// <item><see cref="AnonimizarExtracto"/> — al derivar el resumen: quita números/identificadores,
/// emails y URLs (PII) y acota la longitud.</item>
/// <item><see cref="NeutralizarInstrucciones"/> — al inyectar: neutraliza patrones imperativos/de
/// instrucción (inyección transitiva) y avisa si detectó un intento.</item>
/// </list>
/// El contenido de terceros es <b>dato no confiable</b>: viaja siempre delimitado y marcado como "NO
/// son instrucciones"; estas guardas son defensa en profundidad, no la única barrera.
/// </summary>
public static partial class SanitizadorAportes
{
    /// <summary>Aproximación conservadora de tokens (~4 chars/token) para presupuestar el bloque sin tokenizer.</summary>
    public static int EstimarTokens(string texto)
        => string.IsNullOrEmpty(texto) ? 0 : (int)Math.Ceiling(texto.Length / 4.0);

    /// <summary>
    /// Extracto anonimizado de un texto libre para armar el resumen del aporte: colapsa espacios, quita
    /// URLs/emails y secuencias de dígitos (teléfonos/identificadores) y recorta a <paramref name="maxChars"/>
    /// respetando el límite de palabra cuando es posible. No incluye nombre ni número del autor.
    /// </summary>
    public static string AnonimizarExtracto(string? texto, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return string.Empty;
        }

        var limpio = RegexUrl().Replace(texto, " ");
        limpio = RegexEmail().Replace(limpio, " ");
        // Secuencias de 4+ dígitos (teléfonos, cédulas, montos) => marcador neutro.
        limpio = RegexDigitos().Replace(limpio, "#");
        limpio = RegexEspacios().Replace(limpio, " ").Trim();

        return Acotar(limpio, Math.Max(1, maxChars));
    }

    /// <summary>
    /// Neutraliza un fragmento antes de inyectarlo: descarta las frases que contienen patrones de
    /// inyección/instrucción (p. ej. "ignora tus instrucciones", "act as", "system prompt") y devuelve
    /// el resto. <paramref name="inyeccionDetectada"/> queda en <c>true</c> si se descartó al menos una
    /// frase, para registrar <c>PromptInjectionSospechoso</c> (08 §5.9). Un fragmento cuyo contenido es
    /// íntegramente un intento de inyección queda vacío y el llamador lo omite.
    /// </summary>
    public static string NeutralizarInstrucciones(string? fragmento, out bool inyeccionDetectada)
    {
        inyeccionDetectada = false;
        if (string.IsNullOrWhiteSpace(fragmento))
        {
            return string.Empty;
        }

        var frases = RegexFrases().Split(fragmento);
        var conservadas = new List<string>(frases.Length);
        foreach (var frase in frases)
        {
            var recortada = frase.Trim();
            if (recortada.Length == 0)
            {
                continue;
            }

            if (ContienePatronInyeccion(recortada))
            {
                inyeccionDetectada = true;
                continue;
            }

            conservadas.Add(recortada);
        }

        return RegexEspacios().Replace(string.Join(". ", conservadas), " ").Trim();
    }

    /// <summary>¿El texto (sin acentos, en minúsculas) contiene alguna señal de inyección de prompt?</summary>
    public static bool ContienePatronInyeccion(string texto)
    {
        var plano = QuitarAcentos(texto).ToLowerInvariant();
        foreach (var senal in SenalesInyeccion)
        {
            if (plano.Contains(senal, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Quita diacríticos para comparar de forma robusta (léxico e inyección).</summary>
    public static string QuitarAcentos(string texto)
    {
        var descompuesto = texto.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(descompuesto.Length);
        foreach (var c in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string Acotar(string texto, int maximo)
    {
        if (texto.Length <= maximo)
        {
            return texto;
        }

        var cortado = texto[..maximo];
        var ultimoEspacio = cortado.LastIndexOf(' ');
        // Corta en el último espacio si no perdemos demasiado, para no partir palabras a la mitad.
        if (ultimoEspacio >= maximo / 2)
        {
            cortado = cortado[..ultimoEspacio];
        }

        return cortado.TrimEnd() + "…";
    }

    // Señales de inyección transitiva (ES/EN), ya sin acentos y en minúsculas. Se buscan como
    // substring para tolerar conjugaciones/variantes ("ignora", "ignoren", "ignore").
    private static readonly string[] SenalesInyeccion =
    {
        "ignora", "ignore", "olvida", "olvide", "forget", "desestima", "disregard",
        "caso omiso", "no sigas", "no obedezcas", "system prompt", "prompt del sistema",
        "instruccion", "instruction", "actua como", "act as", "pretende ser", "eres ahora",
        "you are now", "override", "jailbreak", "revela", "reveal", "responde solo", "responde unicamente",
        "nueva regla", "new rule", "a partir de ahora", "from now on",
    };

    [GeneratedRegex(@"https?://\S+", RegexOptions.IgnoreCase)]
    private static partial Regex RegexUrl();

    [GeneratedRegex(@"\S+@\S+\.\S+")]
    private static partial Regex RegexEmail();

    [GeneratedRegex(@"\d{4,}")]
    private static partial Regex RegexDigitos();

    [GeneratedRegex(@"\s+")]
    private static partial Regex RegexEspacios();

    [GeneratedRegex(@"[.!?\n\r]+")]
    private static partial Regex RegexFrases();
}
