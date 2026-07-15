namespace ElTejido.Calibracion;

/// <summary>
/// Harness de calibración (D5 §3.2): corre el golden set <b>N veces</b> a través de un
/// <see cref="IEvaluadorEntrada"/> y agrega el resultado en un <see cref="ReporteCalibracion"/>. El
/// bucle es determinista en orden (entrada por entrada, repetición por repetición); la variabilidad
/// la aporta el modelo, por eso se corre N veces y se reporta la distribución, no un único valor.
/// </summary>
public static class CorredorCalibracion
{
    public static async Task<ReporteCalibracion> CorrerAsync(
        GoldenSet set,
        int n,
        IEvaluadorEntrada evaluador,
        MetadatosCorrido metadatos,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(evaluador);
        ArgumentNullException.ThrowIfNull(metadatos);

        var repeticiones = Math.Max(1, n);
        var muestras = new List<MuestraCalibracion>(set.Entradas.Count * repeticiones);

        foreach (var entrada in set.Entradas)
        {
            for (var i = 0; i < repeticiones; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var resultado = await evaluador.EvaluarAsync(entrada, cancellationToken);
                muestras.Add(MuestraCalibracion.Desde(entrada, resultado));
            }
        }

        return AgregadorCalibracion.Agregar(metadatos with { N = repeticiones }, muestras);
    }
}
