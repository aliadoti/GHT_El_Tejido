namespace ElTejido.Application.Conversacion;

/// <summary>
/// Puerto de la <b>base de conocimiento común de la campaña</b> (I-09, 05 §4.8, 08 §3.2). Recupera
/// resúmenes <b>anonimizados</b> de aportes de otros participantes relevantes para una consulta, para
/// que el coach deje de ser autocontenido. La implementación es interna al orquestador/evaluador; los
/// aportes no se exponen por la API (`04` sin cambios).
/// </summary>
/// <remarks>
/// La firma incluye la identidad a excluir porque la Opción A léxica (decisión de diseño 2026-07-15,
/// I-09 §8) debe <b>excluir los aportes del propio autor y de la conversación en curso</b> para no
/// devolverle al participante su propio texto. La recuperación <b>nunca</b> debe bloquear el hilo: ante
/// error o sin aportes, el orquestador degrada a conversación autocontenida (05 §4.8).
/// <para>
/// Opción B (embeddings, <c>Conversacion:RecuperacionSemantica</c>, off) queda como implementación
/// pluggable diferida tras este mismo puerto; no se implementa en el Hito.
/// </para>
/// </remarks>
public interface IBaseConocimientoCampania
{
    /// <param name="campaniaId">Partición de la campaña sobre la que se recupera.</param>
    /// <param name="textoConsulta">Texto de la respuesta en curso; base del solapamiento léxico.</param>
    /// <param name="tags">Tags del participante actual; dan boost a aportes con tags compartidas.</param>
    /// <param name="usuarioIdAutorExcluir">Autor a excluir (no se le teje su propio aporte).</param>
    /// <param name="conversacionIdExcluir">Conversación en curso a excluir (null = no excluir ninguna).</param>
    /// <param name="topK">Máximo de aportes a devolver (recorte final).</param>
    Task<IReadOnlyList<AporteRelevante>> RecuperarAsync(
        string campaniaId,
        string textoConsulta,
        IReadOnlyCollection<string> tags,
        string usuarioIdAutorExcluir,
        string? conversacionIdExcluir,
        int topK,
        CancellationToken cancellationToken);
}
