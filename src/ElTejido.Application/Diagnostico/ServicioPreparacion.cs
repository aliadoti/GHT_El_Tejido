namespace ElTejido.Application.Diagnostico;

/// <summary>
/// Implementacion por defecto: ejecuta todas las <see cref="IComprobacionPreparacion"/> registradas
/// y consolida sus resultados. El agregado prioriza <see cref="EstadoPreparacion.Error"/> sobre
/// <see cref="EstadoPreparacion.Faltante"/>; las comprobaciones <see cref="EstadoPreparacion.NoAplica"/>
/// no afectan el resultado. Si una comprobacion lanza pese al contrato, se reporta como Error sin
/// filtrar el mensaje (solo el tipo de excepcion), para no interrumpir el resto.
/// </summary>
public sealed class ServicioPreparacion : IServicioPreparacion
{
    private readonly IEnumerable<IComprobacionPreparacion> _comprobaciones;

    public ServicioPreparacion(IEnumerable<IComprobacionPreparacion> comprobaciones)
    {
        _comprobaciones = comprobaciones;
    }

    public async Task<ReportePreparacion> GenerarReporteAsync(CancellationToken cancellationToken)
    {
        var resultados = new List<ResultadoComprobacion>();

        foreach (var comprobacion in _comprobaciones)
        {
            try
            {
                resultados.AddRange(await comprobacion.ComprobarAsync(cancellationToken));
            }
            catch (Exception ex)
            {
                resultados.Add(new ResultadoComprobacion(
                    comprobacion.GetType().Name,
                    EstadoPreparacion.Error,
                    $"Fallo no controlado: {ex.GetType().Name}."));
            }
        }

        var estado = Consolidar(resultados);
        return new ReportePreparacion(estado, resultados);
    }

    private static EstadoPreparacion Consolidar(IReadOnlyCollection<ResultadoComprobacion> resultados)
    {
        if (resultados.Any(r => r.Estado == EstadoPreparacion.Error))
        {
            return EstadoPreparacion.Error;
        }

        if (resultados.Any(r => r.Estado == EstadoPreparacion.Faltante))
        {
            return EstadoPreparacion.Faltante;
        }

        return EstadoPreparacion.Ok;
    }
}
