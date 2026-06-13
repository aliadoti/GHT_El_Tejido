using ElTejido.Domain.Common;

namespace ElTejido.Domain.Campanas;

public sealed class ConfigConversacional
{
    private ConfigConversacional(int maxRepreguntas, string mensajeCierre)
    {
        MaxRepreguntas = maxRepreguntas;
        MensajeCierre = mensajeCierre;
    }

    public int MaxRepreguntas { get; }

    public string MensajeCierre { get; }

    public static ConfigConversacional Crear(int maxRepreguntas, string mensajeCierre)
    {
        if (maxRepreguntas < 0)
        {
            throw new DomainValidationException(
                "MAX_REPREGUNTAS_INVALIDO",
                "El maximo de repreguntas no puede ser negativo.");
        }

        return new ConfigConversacional(
            maxRepreguntas,
            DomainGuards.Required(mensajeCierre, nameof(mensajeCierre)));
    }
}
