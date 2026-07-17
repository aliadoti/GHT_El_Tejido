using ElTejido.Domain.Common;

namespace ElTejido.Domain.Campanas;

public sealed class ConfigConversacional
{
    private ConfigConversacional(int maxRepreguntas, string mensajeCierre, bool segmentacionIdeas, bool tejidoColectivo)
    {
        MaxRepreguntas = maxRepreguntas;
        MensajeCierre = mensajeCierre;
        SegmentacionIdeas = segmentacionIdeas;
        TejidoColectivo = tejidoColectivo;
    }

    public int MaxRepreguntas { get; }

    public string MensajeCierre { get; }

    /// <summary>
    /// I-06: habilita la segmentacion de un mensaje en varias ideas para esta campania. El valor por
    /// defecto es <c>false</c> para que los documentos historicos mantengan el flujo 1-idea.
    /// </summary>
    public bool SegmentacionIdeas { get; }

    /// <summary>
    /// I-09: habilita el <b>tejido colectivo</b> para esta campania — el coach recupera e inyecta como
    /// dato no confiable delimitado (08 §3.2) resumenes anonimizados de aportes de otros participantes
    /// de la misma campania antes de evaluar. El valor por defecto es <c>false</c> para que los
    /// documentos historicos mantengan la conversacion autocontenida (comportamiento actual). El
    /// kill-switch global <c>Conversacion:TejidoColectivo=false</c> lo anula para todas las campanias.
    /// I-10 (Sprint 2) anade sobre este mismo campo la semantica base previa vs. blanco y su UI.
    /// </summary>
    public bool TejidoColectivo { get; }

    public static ConfigConversacional Crear(
        int maxRepreguntas,
        string mensajeCierre,
        bool segmentacionIdeas = false,
        bool tejidoColectivo = false)
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
            segmentacionIdeas,
            tejidoColectivo);
    }
}
