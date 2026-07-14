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

    Task<DominioEvaluacion?> ObtenerEvaluacionPorIdAsync(
        string campaniaId,
        string evaluacionId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Respuesta>> ListarRespuestasAsync(
        string campaniaId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Cantidad de evaluaciones registradas para un usuario dentro de una campania. Cada llamada al
    /// LLM persiste exactamente una <c>Evaluacion</c> (valida o fallback), asi que este conteo es el
    /// contador del cupo <c>MaxLlamadasLlmPorUsuario</c> (10 §2) sin documentos adicionales.
    /// </summary>
    Task<int> ContarEvaluacionesUsuarioAsync(
        string campaniaId,
        string usuarioId,
        CancellationToken cancellationToken);

    /// <summary>
    /// P-10 — suma los tokens LLM (prompt + completion) de todas las evaluaciones de una campaña.
    /// Es el contador del presupuesto <c>PresupuestoTokensCampania</c> derivado de documentos
    /// existentes (sin documentos contadores nuevos, mismo criterio que
    /// <see cref="ContarEvaluacionesUsuarioAsync"/>). Las evaluaciones sin uso reportado suman 0.
    /// </summary>
    Task<long> SumarTokensCampaniaAsync(
        string campaniaId,
        CancellationToken cancellationToken);

    Task GuardarArtefactoAsync(ArtefactoMarkdown artefacto, CancellationToken cancellationToken);

    Task<ArtefactoMarkdown?> ObtenerArtefactoAsync(
        string campaniaId,
        string artefactoId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ArtefactoMarkdown>> ListarArtefactosAsync(
        string campaniaId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Borra fisicamente respuestas, evaluaciones y artefactos Markdown dentro de una campania
    /// (P-03, reinicio de datos de prueba). Con <paramref name="usuarioId"/> = null borra todo lo de
    /// la campania; con un usuario, solo lo de ese usuario. Acotado a la particion <c>campaniaId</c>;
    /// idempotente. Devuelve los conteos y las rutas de blob de los artefactos borrados (para que el
    /// servicio intente borrar tambien el blob).
    /// </summary>
    Task<ConteoBorradoRespuestas> EliminarPorUsuarioAsync(
        string campaniaId,
        string? usuarioId,
        CancellationToken cancellationToken);
}

/// <summary>Conteos del borrado de respuestas/evaluaciones/artefactos de un alcance (P-03).</summary>
public sealed record ConteoBorradoRespuestas(
    int Respuestas,
    int Evaluaciones,
    int Artefactos,
    IReadOnlyCollection<string> RutasBlob);
