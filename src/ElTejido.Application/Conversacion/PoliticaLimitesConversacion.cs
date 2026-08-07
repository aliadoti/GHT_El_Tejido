using ElTejido.Domain.Campanas;
using ElTejido.Domain.Configuracion;
using ElTejido.Domain.Respuestas;
using DominioConversacion = ElTejido.Domain.Conversaciones.Conversacion;

namespace ElTejido.Application.Conversacion;

/// <summary>
/// P-15 (CAL-001) — política determinista y <b>sin E/S</b> del orquestador conversacional. Concentra la
/// resolución del umbral (P-13 + I-17, precedencia <b>pregunta → campaña → global</b>), la clasificación
/// de madurez (I-17, 03 §3.8), el corte por calificación alta (05 §4.4) y la elegibilidad de una nueva
/// mejora (05 §4.4). No lee configuración, reloj, repositorios ni servicios: recibe sus entradas de forma
/// explícita y devuelve decisiones tipadas, de modo que puede probarse aislada. Es un colaborador interno
/// del orquestador; los llamadores externos no lo conocen (la fachada sigue siendo
/// <see cref="IOrquestadorConversacion"/>). No cambia el comportamiento observable: reproduce exactamente
/// las mismas decisiones que antes vivían inline en <see cref="OrquestadorConversacion"/>.
/// </summary>
public sealed class PoliticaLimitesConversacion
{
    private readonly double _umbralBaseGlobal;
    private readonly bool _cierreAnticipadoHabilitado;
    private readonly double _umbralResumenGlobal;
    private readonly bool _resumenConsolidacionHabilitado;

    /// <param name="umbralBaseGlobal">
    /// Default global del umbral compartido (<c>Conversacion:UmbralCierreAnticipado</c>): fracción de la
    /// escala en [0,1] que se usa cuando ni la pregunta ni la campaña definen un override.
    /// </param>
    /// <param name="cierreAnticipadoHabilitado">
    /// Kill-switch global del cierre anticipado (<c>Conversacion:CierreAnticipadoHabilitado</c>). En
    /// <c>false</c> apaga todo cierre anticipado sin afectar la clasificación de madurez.
    /// </param>
    public PoliticaLimitesConversacion(double umbralBaseGlobal, bool cierreAnticipadoHabilitado,
        double umbralResumenGlobal = 0, bool resumenConsolidacionHabilitado = false)
    {
        _umbralBaseGlobal = umbralBaseGlobal;
        _cierreAnticipadoHabilitado = cierreAnticipadoHabilitado;
        _umbralResumenGlobal = umbralResumenGlobal;
        _resumenConsolidacionHabilitado = resumenConsolidacionHabilitado;
    }

    /// <summary>
    /// I-17: umbral base compartido con precedencia <b>pregunta → campaña → default global</b>. Gobierna
    /// tanto la clasificación de madurez (siempre, sin depender del kill-switch de cierre) como, cuando el
    /// cierre anticipado está habilitado, el corte temprano. Un valor menor o igual a cero desactiva el
    /// efecto (nada supera el umbral).
    /// </summary>
    public double ResolverUmbralBase(Campania campania, Pregunta pregunta)
        => pregunta.UmbralCierreAnticipado
            ?? campania.ConfigConversacional.UmbralCierreAnticipado
            ?? _umbralBaseGlobal;

    /// <summary>
    /// Resuelve P-13 + I-17: el kill-switch global de cierre prevalece (apaga el cierre sin afectar la
    /// clasificación de madurez); de lo contrario usa el umbral base (pregunta → campaña → global).
    /// </summary>
    public double ResolverUmbralCierreAnticipado(Campania campania, Pregunta pregunta)
        => !_cierreAnticipadoHabilitado
            ? 0
            : ResolverUmbralBase(campania, pregunta);

    /// <summary>Origen del umbral efectivo (para telemetría): <c>pregunta</c>, <c>campania</c> o <c>global</c>.</summary>
    public string OrigenUmbral(Campania campania, Pregunta pregunta)
        => pregunta.UmbralCierreAnticipado.HasValue
            ? "pregunta"
            : campania.ConfigConversacional.UmbralCierreAnticipado.HasValue
                ? "campania"
                : "global";

    /// <summary>P-31: umbral independiente para mostrar la consolidacion, con la misma precedencia.</summary>
    public double ResolverUmbralResumen(Campania campania, Pregunta pregunta)
        => !_resumenConsolidacionHabilitado || !campania.ConfigConversacional.ResumenConsolidacion
            ? 0
            : pregunta.UmbralResumenConsolidacion
                ?? campania.ConfigConversacional.UmbralResumenConsolidacion
                ?? _umbralResumenGlobal;

    public string OrigenUmbralResumen(Campania campania, Pregunta pregunta)
        => pregunta.UmbralResumenConsolidacion.HasValue
            ? "pregunta"
            : campania.ConfigConversacional.UmbralResumenConsolidacion.HasValue
                ? "campania"
                : "global";

    /// <summary>
    /// I-17 (03 §3.8): clasificación determinista de madurez sellada al evaluar, server-side. Usa el
    /// umbral base (independiente del kill-switch de cierre): <c>Maduro</c> si la calificación válida
    /// supera el umbral; <c>Incubacion</c> en caso contrario o en fallback/pendiente.
    /// </summary>
    public NivelMadurez ClasificarMadurez(
        bool esFallback,
        decimal calificacionTotal,
        EscalaRubrica escala,
        double umbralBase)
        => !esFallback && UmbralAlcanzado(calificacionTotal, escala, umbralBase)
            ? NivelMadurez.Maduro
            : NivelMadurez.Incubacion;

    /// <summary>¿La calificación total alcanza la fracción efectiva de la escala de la rúbrica?</summary>
    public bool UmbralAlcanzado(decimal calificacionTotal, EscalaRubrica escala, double umbralEfectivo)
    {
        if (umbralEfectivo <= 0)
        {
            return false;
        }

        return calificacionTotal >= ValorUmbral(escala, umbralEfectivo);
    }

    /// <summary>Valor absoluto del umbral en la escala de la rubrica (fraccion acotada a [0,1]).</summary>
    public decimal ValorUmbral(EscalaRubrica escala, double umbralEfectivo)
    {
        var fraccion = (decimal)Math.Min(umbralEfectivo, 1.0);
        return escala.Min + (fraccion * (escala.Max - escala.Min));
    }

    /// <summary>
    /// Elegibilidad de una nueva mejora (05 §4.4): aún queda cupo de repreguntas en el hilo
    /// (<c>RepreguntasUsadas &lt; MaxRepreguntas</c>). No decide por sí sola ofrecerla (eso también exige
    /// una evaluación válida y que no haya cierre por calificación alta); solo expresa el límite duro.
    /// </summary>
    public bool PuedeOfrecerMejora(DominioConversacion conversacion, Pregunta pregunta)
        => conversacion.RepreguntasUsadas < pregunta.MaxRepreguntas;
}
