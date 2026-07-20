using ElTejido.Domain.Common;

namespace ElTejido.Domain.Campanas;

public sealed class ConfigConversacional
{
    private ConfigConversacional(
        int maxRepreguntas,
        string mensajeCierre,
        bool segmentacionIdeas,
        bool tejidoColectivo,
        bool parafraseo)
    {
        MaxRepreguntas = maxRepreguntas;
        MensajeCierre = mensajeCierre;
        SegmentacionIdeas = segmentacionIdeas;
        TejidoColectivo = tejidoColectivo;
        Parafraseo = parafraseo;
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

    /// <summary>
    /// I-05: solicita al evaluador un resumen fiel y breve del aporte para anteponerlo a la
    /// retroalimentacion. Nace apagado para que una campania o documento historico conserve el
    /// mensaje actual; el kill-switch global <c>Conversacion:Parafraseo=false</c> lo anula.
    /// </summary>
    public bool Parafraseo { get; }

    public static ConfigConversacional Crear(
        int maxRepreguntas,
        string mensajeCierre,
        bool segmentacionIdeas = false,
        bool tejidoColectivo = false,
        bool parafraseo = false)
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
            tejidoColectivo,
            parafraseo);
    }
}
