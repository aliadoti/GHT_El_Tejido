using ElTejido.Domain.Common;

namespace ElTejido.Domain.Campanas;

public sealed class LimitesSeguridad
{
    private LimitesSeguridad(int maxCaracteresMensaje, int maxMensajesPorUsuario, int maxLlamadasLlmPorUsuario)
    {
        MaxCaracteresMensaje = maxCaracteresMensaje;
        MaxMensajesPorUsuario = maxMensajesPorUsuario;
        MaxLlamadasLlmPorUsuario = maxLlamadasLlmPorUsuario;
    }

    public int MaxCaracteresMensaje { get; }

    public int MaxMensajesPorUsuario { get; }

    public int MaxLlamadasLlmPorUsuario { get; }

    public static LimitesSeguridad Crear(
        int maxCaracteresMensaje,
        int maxMensajesPorUsuario,
        int maxLlamadasLlmPorUsuario)
    {
        EnsurePositive(maxCaracteresMensaje, nameof(maxCaracteresMensaje));
        EnsurePositive(maxMensajesPorUsuario, nameof(maxMensajesPorUsuario));
        EnsurePositive(maxLlamadasLlmPorUsuario, nameof(maxLlamadasLlmPorUsuario));

        return new LimitesSeguridad(
            maxCaracteresMensaje,
            maxMensajesPorUsuario,
            maxLlamadasLlmPorUsuario);
    }

    public static LimitesSeguridad ParaPregunta(int maxCaracteresMensaje, int maxLlamadasLlm)
    {
        EnsurePositive(maxCaracteresMensaje, nameof(maxCaracteresMensaje));
        EnsurePositive(maxLlamadasLlm, nameof(maxLlamadasLlm));

        return new LimitesSeguridad(maxCaracteresMensaje, 1, maxLlamadasLlm);
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
