using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ElTejido.Application.Evaluacion;
using ElTejido.Domain.Configuracion;

namespace ElTejido.Application.Conversacion;

/// <summary>
/// I-20 §4.1: validación <b>determinista</b> de lo que el redactor devuelve, antes de que llegue al
/// participante. Es la capa 2 (la 1 es la prohibición en el prompt), con el mismo criterio que I-03:
/// una salida sospechosa no se corrige ni se recorta, se <b>rechaza</b> y el llamador usa su respaldo.
/// <para>
/// Reutiliza <see cref="FiltroSalidaRubrica"/> —criterios de la rúbrica, léxico del mecanismo de
/// evaluación y patrones de puntaje <c>N/M</c>— y añade lo propio de I-20: umbral/nota/puntos y
/// promesas de implementación.
/// </para>
/// <para>
/// DT-I20-02 §5.3: cierra con <see cref="ValidadorFragmentoVisibleLlm"/>, el mismo contrato de texto
/// plano que aplica el evaluador, para que ni el puente ni la pregunta lleguen a WhatsApp con
/// encabezados, listas, tablas o etiquetas internas.
/// </para>
/// </summary>
public static class GuardasRedaccionTurno
{
    /// <summary>Léxico que delata la mecánica de evaluación y que I-03 no cubre (§4.1).</summary>
    private static readonly string[] PalabrasProhibidas =
        ["umbral", "puntaje", "puntajes", "puntos", "nota", "notas", "escala", "madura", "madurez"];

    /// <summary>
    /// Promesas que el sistema no puede hacer: I-19 §16 deja explícito que ninguna idea pasa
    /// automáticamente a implementación, conocimiento ni acta.
    /// </summary>
    private static readonly Regex PatronPromesa = new(
        @"\b(implementar[eé]mos|lo implementaremos|ser[aá] implementad[oa]|vamos a implementar|"
        + @"queda aprobad[oa]|lo aprobamos|garantizamos|te aseguramos)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex PatronPregunta = new(@"[?¿]", RegexOptions.Compiled);

    /// <summary>
    /// Valida puente y pregunta para un acto. Devuelve <c>null</c> si la redacción es utilizable, o el
    /// motivo técnico del rechazo (sin incluir el texto rechazado, §4.1).
    /// </summary>
    public static string? Rechazar(
        string? puente,
        string? pregunta,
        ActoConversacional acto,
        int maxCaracteres,
        Rubrica? rubrica)
    {
        if (string.IsNullOrWhiteSpace(puente) && string.IsNullOrWhiteSpace(pregunta))
        {
            return "salida_vacia";
        }

        if (Excede(puente, maxCaracteres) || Excede(pregunta, maxCaracteres))
        {
            return "excede_longitud";
        }

        // §4.1: como máximo una pregunta visible y solo en el acto que la exige.
        if (!PoliticaRedaccionConversacional.AdmitePregunta(acto) && !string.IsNullOrWhiteSpace(pregunta))
        {
            return "pregunta_en_acto_sin_pregunta";
        }

        if (ContienePregunta(puente))
        {
            return "pregunta_en_el_puente";
        }

        if (CuentaPreguntas(pregunta) > 1)
        {
            return "mas_de_una_pregunta";
        }

        if (ContieneFuga(puente, rubrica) || ContieneFuga(pregunta, rubrica))
        {
            return "fuga_de_rubrica";
        }

        // DT-I20-02 §5.3: el contrato visible en texto plano se aplica a los fragmentos de I-20 antes
        // de componer el turno; si uno lo incumple, se usa el fallback de I-20 y DT-I20-01 ni siquiera
        // llega a ejecutarse sobre un texto con estructura editorial.
        return ValidadorFragmentoVisibleLlm.Validar(
                puente,
                new ContextoFragmentoVisible(TipoFragmentoVisible.Puente, maxCaracteres)).Motivo
            ?? ValidadorFragmentoVisibleLlm.Validar(
                pregunta,
                new ContextoFragmentoVisible(TipoFragmentoVisible.Pregunta, maxCaracteres)
                {
                    AdmitePregunta = true,
                }).Motivo;
    }

    /// <summary>¿El texto revela la mecánica de evaluación o promete algo que el sistema no decide?</summary>
    public static bool ContieneFuga(string? texto, Rubrica? rubrica)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return false;
        }

        if (rubrica is not null && FiltroSalidaRubrica.ContieneFuga(texto, rubrica))
        {
            return true;
        }

        var normalizado = Normalizar(texto);
        foreach (var palabra in PalabrasProhibidas)
        {
            if (Regex.IsMatch(normalizado, $@"\b{Regex.Escape(palabra)}\b", RegexOptions.IgnoreCase))
            {
                return true;
            }
        }

        return PatronPromesa.IsMatch(normalizado);
    }

    private static bool Excede(string? texto, int maxCaracteres)
        => !string.IsNullOrWhiteSpace(texto) && texto.Trim().Length > maxCaracteres;

    private static bool ContienePregunta(string? texto)
        => !string.IsNullOrWhiteSpace(texto) && PatronPregunta.IsMatch(texto);

    /// <summary>Cuenta preguntas por cierre <c>?</c>, ignorando la apertura <c>¿</c> del español.</summary>
    private static int CuentaPreguntas(string? texto)
        => string.IsNullOrWhiteSpace(texto) ? 0 : texto.Count(caracter => caracter == '?');

    /// <summary>Minúsculas y sin diacríticos, igual que I-03, para no depender de tildes.</summary>
    private static string Normalizar(string texto)
    {
        var descompuesto = texto.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(descompuesto.Length);
        foreach (var caracter in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caracter) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(caracter);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }
}
