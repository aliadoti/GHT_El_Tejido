using ElTejido.Domain.Conversaciones;

namespace ElTejido.Application.Conversacion;

/// <summary>
/// P-27: decide exclusivamente en el servidor qué candidatos de salida pueden convertirse en una
/// transición. No realiza E/S ni interpreta aportes sustantivos como cierres.
/// </summary>
public sealed class PoliticaIntencionControl
{
    private readonly DetectorIntencionContinuar _finalizarIdea;
    private readonly DetectorIntencionContinuar _finalizarParticipacion;

    public PoliticaIntencionControl(int maxCaracteres)
        : this(
            DetectorIntencionContinuar.FrasesFinalizarIdeaPorDefecto,
            DetectorIntencionContinuar.FrasesFinalizarParticipacionPorDefecto,
            maxCaracteres)
    {
    }

    public PoliticaIntencionControl(OpcionesConversacion opciones)
    {
        ArgumentNullException.ThrowIfNull(opciones);

        var resolucion = ResolutorFrasesFinalizacion.Resolver(opciones);

        _finalizarIdea = new DetectorIntencionContinuar(
            resolucion.FinalizarIdea.Frases, opciones.MaxCaracteresClasificacionIntencionControl);
        _finalizarParticipacion = new DetectorIntencionContinuar(
            resolucion.FinalizarParticipacion.Frases, opciones.MaxCaracteresClasificacionIntencionControl);
    }

    private PoliticaIntencionControl(
        IEnumerable<string> frasesFinalizarIdea,
        IEnumerable<string> frasesFinalizarParticipacion,
        int maxCaracteres)
    {
        _finalizarIdea = new DetectorIntencionContinuar(frasesFinalizarIdea, maxCaracteres);
        _finalizarParticipacion = new DetectorIntencionContinuar(frasesFinalizarParticipacion, maxCaracteres);
    }

    public DecisionIntencionControl Resolver(
        EstadoMaquinaConversacion estado,
        bool hayUnidadActiva,
        string texto,
        IntencionControl? candidata = null)
    {
        if (!EsElegible(estado, hayUnidadActiva))
        {
            return DecisionIntencionControl.Aportar;
        }

        if (_finalizarParticipacion.Coincide(texto))
        {
            return DecisionIntencionControl.FinalizarParticipacion;
        }

        if (_finalizarIdea.Coincide(texto))
        {
            return DecisionIntencionControl.FinalizarIdea;
        }

        return candidata switch
        {
            IntencionControl.FinalizarIdea => DecisionIntencionControl.FinalizarIdea,
            IntencionControl.FinalizarParticipacion => DecisionIntencionControl.FinalizarParticipacion,
            IntencionControl.Ambigua => DecisionIntencionControl.Ambigua,
            _ => DecisionIntencionControl.Aportar,
        };
    }

    public static bool EsElegible(EstadoMaquinaConversacion estado, bool hayUnidadActiva)
        => hayUnidadActiva && estado is EstadoMaquinaConversacion.EsperandoRepregunta
            or EstadoMaquinaConversacion.EsperandoConfirmacionSalida;
}

public enum DecisionIntencionControl
{
    Aportar,
    FinalizarIdea,
    FinalizarParticipacion,
    Ambigua,
}
