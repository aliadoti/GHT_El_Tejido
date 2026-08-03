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
    {
        _finalizarIdea = new DetectorIntencionContinuar(
            DetectorIntencionContinuar.FrasesFinalizarIdeaPorDefecto, maxCaracteres);
        _finalizarParticipacion = new DetectorIntencionContinuar(
            DetectorIntencionContinuar.FrasesFinalizarParticipacionPorDefecto, maxCaracteres);
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
