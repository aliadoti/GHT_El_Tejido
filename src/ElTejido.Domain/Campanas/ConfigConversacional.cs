using ElTejido.Domain.Common;

namespace ElTejido.Domain.Campanas;

public sealed class ConfigConversacional
{
    private ConfigConversacional(
        int maxRepreguntas,
        string mensajeCierre,
        bool segmentacionIdeas,
        bool tejidoColectivo,
        bool parafraseo,
        double? umbralCierreAnticipado,
        int? minutosInactividadSesion)
    {
        MaxRepreguntas = maxRepreguntas;
        MensajeCierre = mensajeCierre;
        SegmentacionIdeas = segmentacionIdeas;
        TejidoColectivo = tejidoColectivo;
        Parafraseo = parafraseo;
        UmbralCierreAnticipado = umbralCierreAnticipado;
        MinutosInactividadSesion = minutosInactividadSesion;
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

    /// <summary>
    /// P-13: override opcional del umbral de cierre por calificación alta. <c>null</c> hereda el
    /// default numérico global; un valor menor o igual a cero lo apaga solo para esta campaña. El
    /// kill-switch global <c>Conversacion:CierreAnticipadoHabilitado=false</c> siempre prevalece.
    /// </summary>
    public double? UmbralCierreAnticipado { get; }

    /// <summary>
    /// I-17 §7 — override por campaña de la ventana de <b>cierre por inactividad de sesion</b>, en
    /// minutos. <c>null</c> hereda el default global <c>Conversacion:MinutosInactividadSesion</c>;
    /// <c>&lt;= 0</c> desactiva el cierre por inactividad solo para esta campaña. No se parametriza por
    /// pregunta (decision del usuario 2026-07-22).
    /// </summary>
    public int? MinutosInactividadSesion { get; }

    public static ConfigConversacional Crear(
        int maxRepreguntas,
        string mensajeCierre,
        bool segmentacionIdeas = false,
        bool tejidoColectivo = false,
        bool parafraseo = false,
        double? umbralCierreAnticipado = null,
        int? minutosInactividadSesion = null)
    {
        if (maxRepreguntas < 0)
        {
            throw new DomainValidationException(
                "MAX_REPREGUNTAS_INVALIDO",
                "El maximo de repreguntas no puede ser negativo.");
        }

        if (umbralCierreAnticipado is > 1)
        {
            throw new DomainValidationException(
                "UMBRAL_CIERRE_ANTICIPADO_INVALIDO",
                "El umbral de cierre anticipado no puede ser mayor que 1.");
        }

        return new ConfigConversacional(
            maxRepreguntas,
            DomainGuards.Required(mensajeCierre, nameof(mensajeCierre)),
            segmentacionIdeas,
            tejidoColectivo,
            parafraseo,
            umbralCierreAnticipado,
            minutosInactividadSesion);
    }
}
