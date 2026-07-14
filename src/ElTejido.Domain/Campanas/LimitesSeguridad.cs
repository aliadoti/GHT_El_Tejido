using ElTejido.Domain.Common;

namespace ElTejido.Domain.Campanas;

public sealed class LimitesSeguridad
{
    private LimitesSeguridad(
        int maxCaracteresMensaje,
        int maxMensajesPorUsuario,
        int maxLlamadasLlmPorUsuario,
        int presupuestoTokensCampania)
    {
        MaxCaracteresMensaje = maxCaracteresMensaje;
        MaxMensajesPorUsuario = maxMensajesPorUsuario;
        MaxLlamadasLlmPorUsuario = maxLlamadasLlmPorUsuario;
        PresupuestoTokensCampania = presupuestoTokensCampania;
    }

    public int MaxCaracteresMensaje { get; }

    public int MaxMensajesPorUsuario { get; }

    public int MaxLlamadasLlmPorUsuario { get; }

    /// <summary>
    /// P-10 — presupuesto de tokens LLM de toda la campaña (prompt + completion acumulados). <b>0 o
    /// negativo lo desactiva</b> (default). Cuando los cupos están activos
    /// (<c>Conversacion:CuposHabilitados</c>) y el consumo acumulado alcanza este techo, la campaña se
    /// trata como cupo LLM agotado (cierre elegante). Aditivo (03 §3.3), default seguro.
    /// </summary>
    public int PresupuestoTokensCampania { get; }

    public static LimitesSeguridad Crear(
        int maxCaracteresMensaje,
        int maxMensajesPorUsuario,
        int maxLlamadasLlmPorUsuario,
        int presupuestoTokensCampania = 0)
    {
        EnsurePositive(maxCaracteresMensaje, nameof(maxCaracteresMensaje));
        EnsurePositive(maxMensajesPorUsuario, nameof(maxMensajesPorUsuario));
        EnsurePositive(maxLlamadasLlmPorUsuario, nameof(maxLlamadasLlmPorUsuario));

        return new LimitesSeguridad(
            maxCaracteresMensaje,
            maxMensajesPorUsuario,
            maxLlamadasLlmPorUsuario,
            Math.Max(0, presupuestoTokensCampania));
    }

    public static LimitesSeguridad ParaPregunta(int maxCaracteresMensaje, int maxLlamadasLlm)
    {
        EnsurePositive(maxCaracteresMensaje, nameof(maxCaracteresMensaje));
        EnsurePositive(maxLlamadasLlm, nameof(maxLlamadasLlm));

        return new LimitesSeguridad(maxCaracteresMensaje, 1, maxLlamadasLlm, 0);
    }

    private static void EnsurePositive(int value, string field)
    {
        if (value <= 0)
        {
            throw new DomainValidationException(
                "LIMITE_SEGURIDAD_INVALIDO",
                $"El campo {field} debe ser mayor que cero.");
        }
    }
}
