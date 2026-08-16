using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ElTejido.Application.Evaluacion;

/// <summary>Fragmento visible generado por el LLM que se valida (DT-I20-02 §5.1).</summary>
public enum TipoFragmentoVisible
{
    /// <summary><c>retroalimentacion_usuario</c> del evaluador (08 §4). Obligatoria.</summary>
    Retroalimentacion,

    /// <summary><c>repregunta_sugerida</c> del evaluador cuando el flujo la exige. Obligatoria y con una sola pregunta.</summary>
    Repregunta,

    /// <summary>Puente de I-20. Opcional: ausente es una salida legítima y no se valida.</summary>
    Puente,

    /// <summary>Pregunta de I-20. Opcional por acto; el máximo de una pregunta lo comparte con <see cref="Repregunta"/>.</summary>
    Pregunta,
}

/// <summary>
/// Contexto tipado de la validación (DT-I20-02 §5.1). No lleva idioma: las reglas estructurales son
/// independientes del idioma y las etiquetas internas se buscan <b>siempre</b> en español e inglés,
/// de modo que una etiqueta española filtrada en un hilo inglés también se rechaza.
/// </summary>
/// <param name="Tipo">Fragmento que se valida; decide obligatoriedad y regla de preguntas.</param>
/// <param name="MaxCaracteres">Máximo aplicable; <c>0</c> o negativo desactiva la regla de longitud.</param>
public sealed record ContextoFragmentoVisible(TipoFragmentoVisible Tipo, int MaxCaracteres)
{
    /// <summary>
    /// ¿Este fragmento puede llevar una pregunta? En la retroalimentación es <c>false</c> cuando el
    /// turno ya enviará <c>repregunta_sugerida</c> por separado (§4.1): dos preguntas en el mismo
    /// mensaje rompen el presupuesto de I-18.
    /// </summary>
    public bool AdmitePregunta { get; init; }
}

/// <summary>
/// Veredicto de la validación. <see cref="Motivo"/> es un código fijo de baja cardinalidad (§8):
/// nunca contiene —ni permite reconstruir— el texto evaluado.
/// </summary>
public sealed record ResultadoFragmentoVisible(bool EsValido, string? Motivo)
{
    public static readonly ResultadoFragmentoVisible Valido = new(true, null);

    public static ResultadoFragmentoVisible Invalido(string motivo) => new(false, motivo);
}

/// <summary>
/// DT-I20-02 §5.1: guarda <b>pura y determinista</b> del contrato visible en texto plano. Valida
/// únicamente fragmentos <b>generados por el LLM</b> (retroalimentación, repregunta y los fragmentos
/// de I-20); nunca el mensaje final, la idea consolidada (P-33), la respuesta del participante, los
/// textos del catálogo P-32 ni los mensajes configurados de campaña (§4.4).
/// <para>
/// El criterio es el de I-03 e I-20: una salida que incumple el contrato <b>no se corrige ni se
/// limpia</b>, se rechaza y el llamador usa su respaldo seguro, campo por campo (§3). Detecta
/// estructura editorial —encabezado, viñeta, lista numerada, cita, separador, tabla y cerca de
/// código— siempre <b>al inicio de línea</b>, para que contenido legítimo como <c>caja #3</c> siga
/// siendo válido (§4.1).
/// </para>
/// </summary>
public static class ValidadorFragmentoVisibleLlm
{
    /// <summary>El fragmento obligatorio llegó vacío.</summary>
    public const string MotivoVacio = "vacio";

    /// <summary>Estructura Markdown (encabezado, lista, tabla, cita, separador o bloque de código).</summary>
    public const string MotivoMarkdownEstructural = "markdown_estructural";

    /// <summary>Etiqueta interna de proceso o título de sección del prompt.</summary>
    public const string MotivoEtiquetaInterna = "etiqueta_interna";

    /// <summary>Número de preguntas incompatible con el tipo de fragmento.</summary>
    public const string MotivoCantidadPreguntas = "cantidad_preguntas";

    /// <summary>Excede el máximo aplicable al fragmento.</summary>
    public const string MotivoLongitud = "longitud";

    private const RegexOptions OpcionesLinea =
        RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.CultureInvariant;

    /// <summary>
    /// Estructura editorial anclada al inicio de línea (§4.1). No se listan énfasis ni caracteres
    /// sueltos: <c>caja #3</c> o <c>2x3</c> son contenido legítimo del participante.
    /// </summary>
    private static readonly Regex[] PatronesMarkdown =
    [
        // Encabezado ATX: "### Lo que ya queda claro".
        new(@"^[ \t]{0,3}#{1,6}[ \t]+\S", OpcionesLinea),
        // Viñeta.
        new(@"^[ \t]{0,3}[-*+][ \t]+\S", OpcionesLinea),
        // Lista numerada.
        new(@"^[ \t]{0,3}\d{1,3}[.)][ \t]+\S", OpcionesLinea),
        // Cita.
        new(@"^[ \t]{0,3}>[ \t]*\S", OpcionesLinea),
        // Separador temático: "---", "***", "___".
        new(@"^[ \t]{0,3}([-*_][ \t]*){3,}[ \t]*$", OpcionesLinea),
        // Cerca de código.
        new(@"^[ \t]{0,3}(```|~~~)", OpcionesLinea),
        // Fila de tabla o su separador.
        new(@"^[ \t]{0,3}\|.*\|[ \t]*$", OpcionesLinea),
        new(@"^[ \t]{0,3}\|?[ \t]*:?-{3,}:?[ \t]*\|", OpcionesLinea),
    ];

    /// <summary>
    /// Etiquetas internas inequívocas: nombres del contrato JSON y órdenes de proceso que el servidor
    /// decide (§4.1). Se buscan en cualquier posición porque ningún participante ni coach las escribe
    /// de forma legítima.
    /// </summary>
    private static readonly string[] EtiquetasInternas =
    [
        "ready_to_save", "ready to save", "save_now", "save now", "listo para guardar", "guardar ahora",
        "retroalimentacion_usuario", "repregunta_sugerida", "calificacion_total", "calificacion_por_criterio",
        "anomalia_seguridad", "parafraseo_devuelto",
        // DT-RUB-01: clave del contrato de salida vigente (08 §4). Las anteriores se conservan para
        // que un modelo que arrastre el formato viejo tampoco pueda filtrarlas al participante.
        // "calificaciones" no entra aqui: es una palabra corriente, y como fuga de rubrica la cubre
        // FiltroSalidaRubrica.
        "criterio_id",
    ];

    /// <summary>
    /// Títulos de sección del prompt de runtime, en español e inglés. Solo cuentan como etiqueta
    /// cuando ocupan la línea completa o abren la línea con dos puntos: así "Lo que ya queda claro es
    /// que definiste el alcance." sigue siendo una frase conversacional válida y "Estado" no bloquea
    /// una idea sobre el estado de un proceso.
    /// </summary>
    private static readonly string[] TitulosInternos =
    [
        "estado", "status", "state",
        "pregunta clave", "key question",
        "recomendacion", "recommendation",
        "lo que ya queda claro", "lo que todavia falta", "lo que falta",
        "what is already clear", "what is still missing",
        "siguiente ajuste recomendado", "next recommended adjustment", "siguiente paso", "next step",
        "resumen", "summary",
    ];

    private static readonly Regex PatronTituloInterno = ConstruirPatronTitulos();

    /// <summary>Cierre de pregunta; la apertura <c>¿</c> del español no abre una segunda pregunta.</summary>
    private const char CierrePregunta = '?';

    /// <summary>
    /// Valida un fragmento visible contra el contrato de texto plano. Devuelve
    /// <see cref="ResultadoFragmentoVisible.Valido"/> o el motivo fijo del rechazo, sin devolver ni
    /// registrar el texto.
    /// </summary>
    public static ResultadoFragmentoVisible Validar(string? texto, ContextoFragmentoVisible contexto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            // Puente y pregunta de I-20 son opcionales por acto: su ausencia la resuelve el llamador.
            return EsObligatorio(contexto.Tipo) ? ResultadoFragmentoVisible.Invalido(MotivoVacio) : ResultadoFragmentoVisible.Valido;
        }

        var normalizado = texto.Trim();

        if (PatronesMarkdown.Any(patron => patron.IsMatch(normalizado)))
        {
            return ResultadoFragmentoVisible.Invalido(MotivoMarkdownEstructural);
        }

        var sinDiacriticos = Normalizar(normalizado);
        if (EtiquetasInternas.Any(etiqueta => sinDiacriticos.Contains(etiqueta, StringComparison.Ordinal))
            || PatronTituloInterno.IsMatch(sinDiacriticos))
        {
            return ResultadoFragmentoVisible.Invalido(MotivoEtiquetaInterna);
        }

        if (!CantidadDePreguntasValida(normalizado, contexto))
        {
            return ResultadoFragmentoVisible.Invalido(MotivoCantidadPreguntas);
        }

        return contexto.MaxCaracteres > 0 && normalizado.Length > contexto.MaxCaracteres
            ? ResultadoFragmentoVisible.Invalido(MotivoLongitud)
            : ResultadoFragmentoVisible.Valido;
    }

    private static bool EsObligatorio(TipoFragmentoVisible tipo)
        => tipo is TipoFragmentoVisible.Retroalimentacion or TipoFragmentoVisible.Repregunta;

    /// <summary>
    /// §4.1/§4.2: la repregunta lleva exactamente una pregunta; la retroalimentación no lleva ninguna
    /// cuando el turno ya enviará la repregunta por separado, y nunca más de una. El puente no
    /// pregunta.
    /// </summary>
    private static bool CantidadDePreguntasValida(string texto, ContextoFragmentoVisible contexto)
    {
        var preguntas = texto.Count(caracter => caracter == CierrePregunta);
        return contexto.Tipo switch
        {
            TipoFragmentoVisible.Repregunta => preguntas == 1,
            TipoFragmentoVisible.Pregunta => preguntas <= 1,
            _ => contexto.AdmitePregunta ? preguntas <= 1 : preguntas == 0,
        };
    }

    private static Regex ConstruirPatronTitulos()
    {
        var alternativas = string.Join('|', TitulosInternos.Select(Regex.Escape));
        // Línea completa (con o sin dos puntos y con o sin negrita) o apertura de línea con dos puntos.
        return new Regex(
            $@"^[ \t]{{0,3}}(\*\*|__)?({alternativas})(\*\*|__)?[ \t]*(:[ \t]*)?$"
            + $@"|^[ \t]{{0,3}}(\*\*|__)?({alternativas})(\*\*|__)?[ \t]*:",
            OpcionesLinea);
    }

    /// <summary>
    /// Minúsculas y sin diacríticos, igual que I-03 y DT-I20-01, <b>conservando los saltos de línea</b>
    /// para que las reglas ancladas al inicio de línea sigan siendo exactas.
    /// </summary>
    private static string Normalizar(string texto)
    {
        var descompuesto = texto.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(descompuesto.Length);
        foreach (var caracter in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caracter) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToLowerInvariant(caracter));
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
