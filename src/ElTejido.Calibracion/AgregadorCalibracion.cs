namespace ElTejido.Calibracion;

/// <summary>
/// Agrega las muestras de un corrido en un <see cref="ReporteCalibracion"/> determinista (D5 §3.2).
/// Lógica pura sin I/O ni LLM: se prueba mockeada y verde en CI. Las distribuciones y decisiones se
/// calculan sobre muestras <b>válidas</b> (no fallback); los fallbacks se contabilizan aparte como
/// % inválido. Los tokens se suman sobre <b>todas</b> las muestras (el proveedor cobra el fallback, P-10).
/// </summary>
public static class AgregadorCalibracion
{
    private const int DecimalesEstadistica = 4;
    private const int DecimalesCosto = 6;

    public static ReporteCalibracion Agregar(MetadatosCorrido metadatos, IReadOnlyList<MuestraCalibracion> muestras)
    {
        ArgumentNullException.ThrowIfNull(metadatos);
        ArgumentNullException.ThrowIfNull(muestras);

        var validas = muestras.Where(m => !m.EsFallback).ToArray();

        var distribucionTotal = Distribucion("total", validas.Select(m => m.CalificacionTotal));

        var distribucionPorEje = validas
            .SelectMany(m => m.CalificacionPorCriterio)
            .GroupBy(par => par.Key, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => Distribucion(g.Key, g.Select(par => par.Value)))
            .ToArray();

        var decisiones = new ConteoDecisiones(
            validas.Count(m => m.Decision == DecisionCalibracion.Cerrar),
            validas.Count(m => m.Decision == DecisionCalibracion.Repreguntar));

        var invalidos = ResumenInvalidos(muestras);
        var tokens = ResumenTokens(metadatos, muestras);

        var ideasPorEntrada = muestras
            .GroupBy(m => m.EntradaId, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new IdeasEntrada(
                g.Key,
                g.First().Categoria,
                g.SelectMany(m => m.Ideas)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(idea => idea, StringComparer.OrdinalIgnoreCase)
                    .ToArray()))
            .ToArray();

        var totalEntradas = muestras.Select(m => m.EntradaId).Distinct(StringComparer.Ordinal).Count();

        return new ReporteCalibracion(
            metadatos,
            totalEntradas,
            muestras.Count,
            distribucionTotal,
            distribucionPorEje,
            decisiones,
            invalidos,
            tokens,
            ideasPorEntrada);
    }

    private static DistribucionEje Distribucion(string eje, IEnumerable<decimal> valores)
    {
        var lista = valores.ToArray();
        if (lista.Length == 0)
        {
            return new DistribucionEje(eje, 0, 0m, 0m, 0d, 0d);
        }

        var media = lista.Average();
        var mediaD = (double)media;
        var varianza = lista.Sum(v => Math.Pow((double)v - mediaD, 2)) / lista.Length;

        return new DistribucionEje(
            eje,
            lista.Length,
            lista.Min(),
            lista.Max(),
            Math.Round(mediaD, DecimalesEstadistica, MidpointRounding.ToEven),
            Math.Round(Math.Sqrt(varianza), DecimalesEstadistica, MidpointRounding.ToEven));
    }

    private static ResumenInvalidos ResumenInvalidos(IReadOnlyList<MuestraCalibracion> muestras)
    {
        var invalidas = muestras.Where(m => m.EsFallback).ToArray();
        var porMotivo = invalidas
            .GroupBy(m => m.MotivoFallback ?? "desconocido", StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new MotivoInvalido(g.Key, g.Count()))
            .ToArray();

        var porcentaje = muestras.Count == 0
            ? 0d
            : Math.Round(100d * invalidas.Length / muestras.Count, DecimalesEstadistica, MidpointRounding.ToEven);

        return new ResumenInvalidos(muestras.Count, invalidas.Length, porcentaje, porMotivo);
    }

    private static ResumenTokens ResumenTokens(MetadatosCorrido metadatos, IReadOnlyList<MuestraCalibracion> muestras)
    {
        var porEntrada = muestras
            .GroupBy(m => m.EntradaId, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new UsoTokensEntrada(g.Key, g.Sum(m => m.PromptTokens), g.Sum(m => m.CompletionTokens)))
            .ToArray();

        var prompt = muestras.Sum(m => m.PromptTokens);
        var completion = muestras.Sum(m => m.CompletionTokens);

        decimal? costo = metadatos.Precio is null
            ? null
            : Math.Round(
                (prompt / 1000m * metadatos.Precio.PorMilPrompt) + (completion / 1000m * metadatos.Precio.PorMilCompletion),
                DecimalesCosto,
                MidpointRounding.ToEven);

        return new ResumenTokens(prompt, completion, prompt + completion, costo, porEntrada);
    }
}
