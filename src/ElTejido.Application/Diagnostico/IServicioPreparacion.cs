namespace ElTejido.Application.Diagnostico;

/// <summary>
/// Agrega todas las comprobaciones de preparacion (readiness) y produce el reporte consolidado
/// que consume el endpoint <c>/health/ready</c> (guia de Azure §11, 13 §7).
/// </summary>
public interface IServicioPreparacion
{
    Task<ReportePreparacion> GenerarReporteAsync(CancellationToken cancellationToken);
}
