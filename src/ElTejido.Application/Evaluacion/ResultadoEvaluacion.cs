using DominioEvaluacion = ElTejido.Domain.Evaluacion.Evaluacion;

namespace ElTejido.Application.Evaluacion;

/// <summary>
/// Resultado tipado de evaluar (08 §2): exito con la evaluacion completa, o fallback seguro con una
/// evaluacion parcial y el motivo (08 §6). En fallback el orquestador marca la respuesta como
/// <c>evaluacionPendiente</c> y cierra el hilo con retro neutra; nunca propaga el fallo al usuario.
/// </summary>
public abstract record ResultadoEvaluacion
{
    private ResultadoEvaluacion(DominioEvaluacion evaluacion)
    {
        Evaluacion = evaluacion;
    }

    /// <summary>Evaluacion construida (completa en exito, parcial/neutra en fallback).</summary>
    public DominioEvaluacion Evaluacion { get; }

    public sealed record Exito(DominioEvaluacion Evaluacion) : ResultadoEvaluacion(Evaluacion);

    public sealed record Fallback(DominioEvaluacion Evaluacion, string Motivo) : ResultadoEvaluacion(Evaluacion);
}
