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

    Task GuardarIdeaConsolidadaAsync(IdeaConsolidada idea, CancellationToken cancellationToken)
        => throw new NotSupportedException("El repositorio no implementa ideas consolidadas I-19.");

    Task<IdeaConsolidada?> ObtenerIdeaConsolidadaAsync(string campaniaId, string ideaId, CancellationToken cancellationToken)
        => throw new NotSupportedException("El repositorio no implementa ideas consolidadas I-19.");

    Task<IReadOnlyCollection<IdeaConsolidada>> ListarIdeasConsolidadasAsync(string campaniaId, CancellationToken cancellationToken)
        => throw new NotSupportedException("El repositorio no implementa ideas consolidadas I-19.");

    /// <summary>
    /// P-30: ideas historicas del participante dentro de una campania/pregunta, sin filtrar por estado
    /// ni por ciclo. La implementacion por defecto conserva compatibilidad con dobles de prueba I-19;
    /// los adaptadores persistentes pueden traducir el filtro a su consulta nativa.
    /// </summary>
    async Task<IReadOnlyCollection<IdeaConsolidada>> ListarIdeasHistoricasAsync(
        string campaniaId,
        string usuarioId,
        string preguntaId,
        CancellationToken cancellationToken)
        => (await ListarIdeasConsolidadasAsync(campaniaId, cancellationToken))
            .Where(idea => idea.UsuarioId == usuarioId && idea.PreguntaId == preguntaId)
            .ToArray();

    Task GuardarVersionIdeaAsync(VersionIdeaConsolidada version, CancellationToken cancellationToken)
        => throw new NotSupportedException("El repositorio no implementa versiones de ideas I-19.");

    Task<VersionIdeaConsolidada?> ObtenerVersionIdeaAsync(string campaniaId, string versionId, CancellationToken cancellationToken)
        => throw new NotSupportedException("El repositorio no implementa versiones de ideas I-19.");

    Task<IReadOnlyCollection<VersionIdeaConsolidada>> ListarVersionesIdeaAsync(string campaniaId, string ideaId, CancellationToken cancellationToken)
        => throw new NotSupportedException("El repositorio no implementa versiones de ideas I-19.");

    Task GuardarEvaluacionAsync(DominioEvaluacion evaluacion, CancellationToken cancellationToken);

    /// <summary>
    /// Devuelve la evaluacion mas reciente asociada a una respuesta. Esto blinda campañas reutilizadas
    /// o datos legacy con mas de una evaluacion para el mismo <c>respuestaId</c> (I-16, 09 §5/§7).
    /// </summary>
    Task<DominioEvaluacion?> ObtenerEvaluacionPorRespuestaAsync(
        string campaniaId,
        string respuestaId,
        CancellationToken cancellationToken);

    Task<DominioEvaluacion?> ObtenerEvaluacionPorIdAsync(
        string campaniaId,
        string evaluacionId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Lista las evaluaciones de una campaña en orden descendente por fecha (04 §5.8). No tiene
    /// implementación por defecto: un adaptador sin este diagnóstico no puede informar en silencio
    /// que no existen evaluaciones huérfanas (DT-QA-02).
    /// </summary>
    Task<IReadOnlyCollection<DominioEvaluacion>> ListarEvaluacionesAsync(
        string campaniaId,
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
    /// P-26 §9 — variante con <b>ventana móvil</b>: solo cuenta las evaluaciones con
    /// <c>fecha &gt;= desde</c>. La usan las campañas con <c>participacionContinua=true</c>, donde el
    /// cupo acumulado haría inviable volver a participar. La implementación por defecto degrada al
    /// conteo acumulado (más restrictivo, nunca menos), para que un adaptador que no la implemente
    /// conserve el comportamiento actual sin abrir el cupo por accidente.
    /// </summary>
    Task<int> ContarEvaluacionesUsuarioAsync(
        string campaniaId,
        string usuarioId,
        DateTimeOffset desde,
        CancellationToken cancellationToken)
        => ContarEvaluacionesUsuarioAsync(campaniaId, usuarioId, cancellationToken);

    /// <summary>
    /// I-19 §12.3 — llamadas de <b>consolidación</b> de un usuario en la campaña. Cada
    /// <see cref="VersionIdeaConsolidada"/> nace de exactamente una llamada al consolidador (también
    /// cuando esa llamada terminó en fallback), así que contar versiones cuenta llamadas sin documentos
    /// contadores nuevos, igual que <see cref="ContarEvaluacionesUsuarioAsync"/>. Sumadas a las
    /// evaluaciones dan el total que gobierna <c>MaxLlamadasLlmPorUsuario</c> (10 §2). Un repositorio
    /// sin ideas I-19 devuelve 0 y el cupo se comporta como antes.
    /// </summary>
    Task<int> ContarConsolidacionesUsuarioAsync(
        string campaniaId,
        string usuarioId,
        CancellationToken cancellationToken) => Task.FromResult(0);

    /// <summary>
    /// P-26 §9 — variante con <b>ventana móvil</b> de <see cref="ContarConsolidacionesUsuarioAsync(string,string,CancellationToken)"/>:
    /// solo cuenta las versiones generadas con <c>generadaEn &gt;= desde</c>. Misma degradación segura:
    /// por defecto delega al conteo acumulado.
    /// </summary>
    Task<int> ContarConsolidacionesUsuarioAsync(
        string campaniaId,
        string usuarioId,
        DateTimeOffset desde,
        CancellationToken cancellationToken)
        => ContarConsolidacionesUsuarioAsync(campaniaId, usuarioId, cancellationToken);

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
    IReadOnlyCollection<string> RutasBlob,
    int Ideas = 0,
    int VersionesIdea = 0);
