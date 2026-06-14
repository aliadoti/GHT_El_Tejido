using ElTejido.Domain.Respuestas;
using DominioEvaluacion = ElTejido.Domain.Evaluacion.Evaluacion;

namespace ElTejido.Application.Respuestas;

/// <summary>
/// Puerto del contenedor Cosmos <c>responses</c> (partition key <c>campaniaId</c>) para Respuesta,
/// Evaluacion y ArtefactoMarkdown (03 §3.8-§3.10). Lo consumen el modulo de Markdown (09), el
/// orquestador (05 §4.3, que persiste Respuesta/Evaluacion) y las consultas administrativas (04 §5.8).
/// </summary>
public interface IRepositorioRespuestas
{
    Task GuardarRespuestaAsync(Respuesta respuesta, CancellationToken cancellationToken);

    Task<Respuesta?> ObtenerRespuestaAsync(string campaniaId, string respuestaId, CancellationToken cancellationToken);

    Task GuardarEvaluacionAsync(DominioEvaluacion evaluacion, CancellationToken cancellationToken);

    Task<DominioEvaluacion?> ObtenerEvaluacionPorRespuestaAsync(
        string campaniaId,
        string respuestaId,
        CancellationToken cancellationToken);

    Task GuardarArtefactoAsync(ArtefactoMarkdown artefacto, CancellationToken cancellationToken);

    Task<ArtefactoMarkdown?> ObtenerArtefactoAsync(
        string campaniaId,
        string artefactoId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ArtefactoMarkdown>> ListarArtefactosAsync(
        string campaniaId,
        CancellationToken cancellationToken);
}
