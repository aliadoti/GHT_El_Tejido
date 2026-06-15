namespace ElTejido.Application.Diagnostico;

/// <summary>
/// Puerto de una comprobacion de preparacion sobre una dependencia externa. Cada implementacion
/// (Infrastructure) verifica una dependencia concreta y devuelve uno o mas resultados (p. ej. la
/// comprobacion de secretos devuelve un resultado por secreto). No debe lanzar: traduce los fallos
/// a <see cref="EstadoPreparacion.Error"/> sin filtrar detalles sensibles (10 §6.3).
/// </summary>
public interface IComprobacionPreparacion
{
    Task<IReadOnlyList<ResultadoComprobacion>> ComprobarAsync(CancellationToken cancellationToken);
}
