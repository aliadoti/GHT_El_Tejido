using ElTejido.Domain.Campanas;

namespace ElTejido.Application.Conversacion;

/// <summary>
/// I-20 (§5): decisiones <b>deterministas</b> de la redacción conversacional, sin E/S y sin LLM
/// (mismo patrón que <see cref="PoliticaLimitesConversacion"/>, P-15). Resuelve si la voz dinámica
/// está habilitada y cuál es el prompt efectivo que le da tono.
/// </summary>
public sealed class PoliticaRedaccionConversacional
{
    /// <summary>Clave de `promptRefs` que da voz al redactor (03 §3.3, aditiva y opcional).</summary>
    public const string TipoPromptConversacion = "conversacion";

    /// <summary>Clave de respaldo: si no hay prompt de voz, el de retro guía el tono (§5).</summary>
    public const string TipoPromptRespaldo = "retro";

    private readonly bool _habilitadaGlobal;
    private readonly int _maxCaracteres;

    public PoliticaRedaccionConversacional(bool habilitadaGlobal, int maxCaracteres)
    {
        _habilitadaGlobal = habilitadaGlobal;
        _maxCaracteres = maxCaracteres > 0 ? maxCaracteres : 320;
    }

    /// <summary>
    /// I-20 §5: no hay opt-in por campaña. Solo manda el kill-switch global; apagado, cada acto usa su
    /// respaldo determinista sin tocar consolidación, evaluación ni estados.
    /// </summary>
    public bool Habilitada => _habilitadaGlobal;

    /// <summary>Largo máximo admitido por pieza redactada (§4.1); normalizado en el constructor.</summary>
    public int MaxCaracteres => _maxCaracteres;

    /// <summary>
    /// Prompt efectivo de voz con la precedencia de `03 §3.3`: `conversacion` de la **pregunta** →
    /// `conversacion` de la **campaña** → `retro` de la pregunta → `retro` de la campaña. Devuelve
    /// <c>null</c> cuando ninguna referencia existe: el redactor opera solo con sus instrucciones de
    /// seguridad y las campañas actuales siguen funcionando igual.
    /// </summary>
    public string? ResolverPromptRef(Campania campania, Pregunta pregunta)
        => Primero(pregunta.PromptRefs, TipoPromptConversacion)
            ?? Primero(campania.PromptRefs, TipoPromptConversacion)
            ?? Primero(pregunta.PromptRefs, TipoPromptRespaldo)
            ?? Primero(campania.PromptRefs, TipoPromptRespaldo);

    /// <summary>
    /// ¿El prompt efectivo es el de voz propio de I-20, o se cayó al respaldo de retro? Permite
    /// distinguir en telemetría una campaña ya configurada de una que hereda el tono (§5).
    /// </summary>
    public bool UsaPromptDeVoz(Campania campania, Pregunta pregunta)
        => Primero(pregunta.PromptRefs, TipoPromptConversacion) is not null
            || Primero(campania.PromptRefs, TipoPromptConversacion) is not null;

    /// <summary>
    /// Actos que muestran una pregunta al participante (§3/§4.1). Los demás solo llevan puente: así el
    /// servidor puede rechazar una salida que agregue una pregunta donde no corresponde.
    /// </summary>
    public static bool AdmitePregunta(ActoConversacional acto)
        => acto is ActoConversacional.Confirmar
            or ActoConversacional.Mejorar
            or ActoConversacional.Aclarar
            or ActoConversacional.Reabrir;

    private static string? Primero(IReadOnlyDictionary<string, string>? refs, string tipo)
        => refs is not null && refs.TryGetValue(tipo, out var referencia) && !string.IsNullOrWhiteSpace(referencia)
            ? referencia
            : null;
}
