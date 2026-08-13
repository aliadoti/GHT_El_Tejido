using System.Globalization;
using System.Text;

namespace ElTejido.Application.Conversacion;

/// <summary>
/// DT-I20-01 §4.2: guarda <b>determinista</b> de no duplicación al componer el turno visible
/// (<c>puente → cuerpo → pregunta</c>). El LLM puede elegir su fórmula de reconocimiento, pero el
/// servidor conserva la propiedad del contenido validado y descarta el adorno que lo repita.
/// <para>
/// Es deliberadamente conservadora: compara equivalencia de oraciones y prefijos tras normalizar, no
/// infiere similitud semántica ni llama a otro modelo (§9). Ante duda conserva el cuerpo validado y
/// solo elimina el fragmento redundante.
/// </para>
/// </summary>
public static class FiltroDuplicacionTurno
{
    /// <summary>Motivo técnico no sensible para la auditoría de redacción (§5). Nunca lleva la frase.</summary>
    public const string MotivoPuenteOmitido = "puente_duplicado_omitido";

    public const string MotivoPreguntaOmitida = "pregunta_duplicada_omitida";

    /// <summary>La pregunta duplicaba y el acto la exige: se descarta la redacción entera (§4.2 regla 3).</summary>
    public const string MotivoRespaldoPorDuplicacion = "duplicacion_sin_salida_valida";

    /// <summary>Cierres de oración; <c>¿</c> y <c>¡</c> abren y no separan.</summary>
    private static readonly char[] FinDeOracion = ['.', '!', '?', '\n', '\r'];

    /// <summary>
    /// Compone el turno omitiendo lo redundante. <paramref name="preguntaExigida"/> viene de
    /// <see cref="PoliticaRedaccionConversacional.ExigePregunta"/>: si el acto no puede quedarse sin
    /// pregunta, una pregunta duplicada invalida toda la redacción y el llamador usa su respaldo.
    /// </summary>
    public static ComposicionTurno Componer(
        string? puente,
        string? cuerpo,
        string? pregunta,
        bool preguntaExigida)
    {
        var puenteOmitido = !string.IsNullOrWhiteSpace(puente) && PuenteRedundante(puente, cuerpo);
        var visible = Unir(puenteOmitido ? null : puente, cuerpo);

        var preguntaOmitida = false;
        if (!string.IsNullOrWhiteSpace(pregunta) && RepiteUnaOracion(pregunta, visible))
        {
            if (preguntaExigida)
            {
                return new ComposicionTurno(string.Empty, puenteOmitido, PreguntaOmitida: false, RequiereRespaldo: true);
            }

            preguntaOmitida = true;
        }

        var texto = Unir(visible, preguntaOmitida ? null : pregunta);
        return new ComposicionTurno(texto, puenteOmitido, preguntaOmitida, string.IsNullOrWhiteSpace(texto));
    }

    /// <summary>
    /// §4.2 reglas 1 y 2: el puente coincide con una oración del cuerpo, es prefijo del cuerpo o el
    /// cuerpo es prefijo del puente. En los tres casos manda el cuerpo, que es el texto validado.
    /// </summary>
    public static bool PuenteRedundante(string? puente, string? cuerpo)
    {
        var palabrasPuente = Palabras(puente);
        var palabrasCuerpo = Palabras(cuerpo);
        if (palabrasPuente.Length == 0 || palabrasCuerpo.Length == 0)
        {
            return false;
        }

        return EsPrefijo(palabrasPuente, palabrasCuerpo)
            || EsPrefijo(palabrasCuerpo, palabrasPuente)
            || RepiteUnaOracion(puente, cuerpo);
    }

    /// <summary>
    /// ¿Alguna oración de <paramref name="fragmento"/> ya aparece, equivalente, en
    /// <paramref name="texto"/>? Se compara por oraciones completas: un adorno que repite una frase
    /// visible sobra, pero una palabra suelta compartida no basta para descartar nada.
    /// </summary>
    public static bool RepiteUnaOracion(string? fragmento, string? texto)
    {
        if (string.IsNullOrWhiteSpace(fragmento) || string.IsNullOrWhiteSpace(texto))
        {
            return false;
        }

        var oracionesTexto = Oraciones(texto);
        return oracionesTexto.Count != 0
            && Oraciones(fragmento).Any(oracion => oracionesTexto.Contains(oracion, StringComparer.Ordinal));
    }

    /// <summary>Une dos fragmentos visibles con la separación de siempre, sin dejar separadores colgando.</summary>
    public static string Unir(string? primero, string? segundo)
    {
        if (string.IsNullOrWhiteSpace(primero))
        {
            return segundo?.Trim() ?? string.Empty;
        }

        return string.IsNullOrWhiteSpace(segundo)
            ? primero.Trim()
            : primero.Trim() + "\n\n" + segundo.Trim();
    }

    private static bool EsPrefijo(string[] posiblePrefijo, string[] completo)
    {
        if (posiblePrefijo.Length > completo.Length)
        {
            return false;
        }

        for (var i = 0; i < posiblePrefijo.Length; i++)
        {
            if (!string.Equals(posiblePrefijo[i], completo[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static List<string> Oraciones(string texto)
        => texto.Split(FinDeOracion, StringSplitOptions.RemoveEmptyEntries)
            .Select(Normalizar)
            .Where(oracion => oracion.Length > 0)
            .ToList();

    private static string[] Palabras(string? texto)
        => string.IsNullOrWhiteSpace(texto)
            ? []
            : Normalizar(texto).Split(' ', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    /// Minúsculas, sin diacríticos y sin puntuación, con espacios colapsados: así "¡Ya queda claro!" y
    /// "Ya queda claro." son la misma oración, igual que el criterio de I-03.
    /// </summary>
    private static string Normalizar(string texto)
    {
        var descompuesto = texto.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(descompuesto.Length);
        var espacioPendiente = false;
        foreach (var caracter in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caracter) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(caracter))
            {
                if (espacioPendiente && builder.Length > 0)
                {
                    builder.Append(' ');
                }

                espacioPendiente = false;
                builder.Append(char.ToLowerInvariant(caracter));
            }
            else
            {
                espacioPendiente = true;
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}

/// <summary>
/// Resultado de la composición (DT-I20-01 §4.2/§5). <see cref="Motivo"/> es un código técnico no
/// sensible para la auditoría: nunca contiene el texto omitido.
/// </summary>
public sealed record ComposicionTurno(
    string Texto,
    bool PuenteOmitido,
    bool PreguntaOmitida,
    bool RequiereRespaldo)
{
    public string? Motivo
    {
        get
        {
            var motivos = new List<string>(3);
            if (PuenteOmitido)
            {
                motivos.Add(FiltroDuplicacionTurno.MotivoPuenteOmitido);
            }

            if (PreguntaOmitida)
            {
                motivos.Add(FiltroDuplicacionTurno.MotivoPreguntaOmitida);
            }

            if (RequiereRespaldo)
            {
                motivos.Add(FiltroDuplicacionTurno.MotivoRespaldoPorDuplicacion);
            }

            return motivos.Count == 0 ? null : string.Join("+", motivos);
        }
    }
}
