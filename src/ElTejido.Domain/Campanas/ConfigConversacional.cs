using ElTejido.Domain.Common;

namespace ElTejido.Domain.Campanas;

public sealed class ConfigConversacional
{
    private ConfigConversacional(int maxRepreguntas, string mensajeCierre, bool segmentacionIdeas)
    {
        MaxRepreguntas = maxRepreguntas;
        MensajeCierre = mensajeCierre;
        SegmentacionIdeas = segmentacionIdeas;
    }

    public int MaxRepreguntas { get; }

    public string MensajeCierre { get; }

    /// <summary>
    /// I-06: habilita la segmentacion de un mensaje en varias ideas para esta campania. El valor por
    /// defecto es <c>false</c> para que los documentos historicos mantengan el flujo 1-idea.
    /// </summary>
    public bool SegmentacionIdeas { get; }

    public static ConfigConversacional Crear(int maxRepreguntas, string mensajeCierre, bool segmentacionIdeas = false)
    {
        if (maxRepreguntas < 0)
        {
            throw new DomainValidationException(
                "MAX_REPREGUNTAS_INVALIDO",
                "El maximo de repreguntas no puede ser negativo.");
        }

        return new ConfigConversacional(
            maxRepreguntas,
            DomainGuards.Required(mensajeCierre, nameof(mensajeCierre)),
            segmentacionIdeas);
    }
}
