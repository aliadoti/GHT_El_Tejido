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

    /// <summary>P-27: alias inequívocos para dejar solo la unidad activa.</summary>
    public static readonly IReadOnlyList<string> FrasesFinalizarIdeaPorDefecto = new[]
    {
        "quiero parar aqui",
        "quiero pasar a otra idea",
        "pasar a otra idea",
        "pasemos a otra idea",
        "dejemos esta idea",
        "dejar esta idea",
    };

    /// <summary>P-27: alias inequívocos para finalizar la participación actual.</summary>
    public static readonly IReadOnlyList<string> FrasesFinalizarParticipacionPorDefecto = new[]
    {
        "stop now",
        "quiero terminar por ahora",
        "terminar por ahora",
        "quiero parar por hoy",
        "parar por hoy",
        "finalizar participacion",
    };

    /// <summary>
    /// P-24 — frases breves que piden acompañamiento sobre la propuesta vigente. El orquestador solo
    /// las interpreta en <c>pendienteConfirmacion</c>, por lo que no convierten una mejora con contenido
    /// en confirmación ni cambian la intención de una idea ya evaluada.
    /// </summary>
    public static readonly IReadOnlyList<string> FrasesSolicitarMejoraPorDefecto = new[]
    {
        "vamos a mejorarla",
        "vamos a mejorar",
        "quiero mejorarla",
        "quiero mejorar",
        "ayudame a mejorarla",
        "ayudame a mejorar",
        "me gustaria mejorarla",
        "me gustaria mejorar",
    };

    /// <summary>
    /// I-17 §5.4 — frases con las que el participante <b>rechaza explícitamente</b> que su idea madura
    /// se guarde ("guardar salvo que diga no"). Se usan con este mismo matcher (lista distinta): al
    /// coincidir en <c>esperandoRepregunta</c> el orquestador degrada la respuesta madura a incubación y
    /// cierra con un acuse. Se normalizan al construir el detector.
    /// </summary>
    public static readonly IReadOnlyList<string> FrasesRechazoGuardadoPorDefecto = new[]
    {
        "no",
        "no es eso",
        "no es asi",
        "eso no es lo que quise decir",
        "no era eso",
        "no lo guardes",
        "borralo",
        "eliminalo",
    };

    /// <summary>
    /// I-19 §4.7 — frases con las que el participante pide volver a la <b>idea cerrada más reciente</b>.
    /// Son inequívocas por sí mismas: aunque haya varias ideas cerradas, “la anterior” resuelve
    /// determinísticamente la última. Se normalizan al construir el detector.
    /// </summary>
    public static readonly IReadOnlyList<string> FrasesRevisitarAnteriorPorDefecto = new[]
    {
        "la anterior",
        "quiero complementar la anterior",
        "complementar la anterior",
        "quiero volver a la anterior",
        "volver a la anterior",
        "quiero retomar la anterior",
        "retomar la anterior",
        "quiero corregir la anterior",
        "corregir la anterior",
    };

    /// <summary>
    /// I-19 §4.7 — frases con las que el participante pide revisitar <b>alguna</b> idea previa sin
    /// señalar cuál. Con una sola candidata se reabre esa; con varias, el servidor ofrece una lista
    /// breve numerada y espera el número. Se normalizan al construir el detector.
    /// </summary>
    public static readonly IReadOnlyList<string> FrasesRevisitarIdeaPorDefecto = new[]
    {
        "quiero volver a una idea",
        "volver a una idea",
        "quiero volver a una idea anterior",
        "quiero retomar una idea",
        "retomar una idea",
        "quiero revisar una idea",
        "revisar una idea",
        "quiero complementar una idea",
        "complementar una idea",
        "quiero corregir una idea",
    };

    /// <summary>
    /// P-26 §5.1 paso 3 — frases con las que el participante pide explicitamente cambiar de campania.
    /// Suspenden la afinidad vigente sin cerrar la idea y recalculan las opciones. Se normalizan al
    /// construir el detector (mayusculas/acentos no importan).
    /// </summary>
    public static readonly IReadOnlyList<string> FrasesCambiarCampaniaPorDefecto = new[]
    {
        "otra campaña",
        "cambiar de campaña",
        "quiero cambiar de campaña",
        "cambiemos de campaña",
        "quiero otra campaña",
        "cambio de campaña",
        "ver otras campañas",
    };

    /// <summary>
    /// Coincidencia deterministica de una intención por frases (match barato con guarda de longitud).
    /// Es el mecanismo generico que usan tanto la intención de continuar como (I-17) la de rechazo.
    /// </summary>
    public bool Coincide(string? texto) => DeseaContinuar(texto);

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
