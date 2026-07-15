namespace ElTejido.Calibracion;

/// <summary>Tipo de regresión detectada al comparar un corrido contra el baseline (D5 §3.3).</summary>
public enum TipoRegresion
{
    MediaTotal,
    MediaEje,
    EjeAusente,
    EjeNuevo,
    PorcentajeInvalido,
    ProporcionDecision,
}

/// <summary>
/// Tolerancias configurables del comparador (D5 §3.3). Un delta que las exceda marca regresión. Los
/// defaults son conservadores para el Hito; se ajustan por corrido según el árbitro de la calibración.
/// </summary>
public sealed record Tolerancias(
    double MaxDeltaMediaTotal = 0.5,
    double MaxDeltaMediaEje = 0.5,
    double MaxDeltaPorcentajeInvalido = 5.0,
    double MaxDeltaProporcionCerrar = 0.15);

/// <summary>Una regresión concreta: qué cambió, cuánto y contra qué tolerancia.</summary>
public sealed record Regresion(
    TipoRegresion Tipo,
    string? Eje,
    double ValorBaseline,
    double ValorActual,
    double Delta,
    double Tolerancia,
    string Descripcion);

/// <summary>Resultado de comparar contra el baseline: lista ordenada de regresiones (vacía = sin regresión).</summary>
public sealed record ResultadoComparacion(IReadOnlyList<Regresion> Regresiones)
{
    public bool HayRegresion => Regresiones.Count > 0;
}

/// <summary>
/// Compara un reporte de corrido contra el baseline congelado y marca los deltas que exceden las
/// tolerancias (D5 §3.3): así toda versión de prompt/umbral/rúbrica se valida contra la anterior con
/// el banco como árbitro (T-47). Lógica pura y determinista: se prueba con datos mockeados en CI.
/// </summary>
public static class ComparadorRegresion
{
    public static ResultadoComparacion Comparar(ReporteCalibracion baseline, ReporteCalibracion actual, Tolerancias? tolerancias = null)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(actual);
        var tol = tolerancias ?? new Tolerancias();

        var regresiones = new List<Regresion>();

        // Media total.
        AgregarSiExcede(
            regresiones,
            TipoRegresion.MediaTotal,
            eje: null,
            baseline.DistribucionTotal.Media,
            actual.DistribucionTotal.Media,
            tol.MaxDeltaMediaTotal,
            "Media del score total");

        // Ejes: media por criterio, más ejes que aparecen/desaparecen.
        var ejesBaseline = baseline.DistribucionPorEje.ToDictionary(e => e.Eje, StringComparer.Ordinal);
        var ejesActual = actual.DistribucionPorEje.ToDictionary(e => e.Eje, StringComparer.Ordinal);

        foreach (var eje in ejesBaseline.Keys.Union(ejesActual.Keys, StringComparer.Ordinal).OrderBy(e => e, StringComparer.Ordinal))
        {
            var enBaseline = ejesBaseline.TryGetValue(eje, out var db);
            var enActual = ejesActual.TryGetValue(eje, out var da);

            if (enBaseline && !enActual)
            {
                regresiones.Add(new Regresion(TipoRegresion.EjeAusente, eje, db!.Media, 0d, 0d, 0d,
                    $"El eje '{eje}' estaba en el baseline y no aparece en el corrido actual."));
            }
            else if (!enBaseline && enActual)
            {
                regresiones.Add(new Regresion(TipoRegresion.EjeNuevo, eje, 0d, da!.Media, 0d, 0d,
                    $"El eje '{eje}' aparece en el corrido actual y no estaba en el baseline."));
            }
            else
            {
                AgregarSiExcede(regresiones, TipoRegresion.MediaEje, eje, db!.Media, da!.Media, tol.MaxDeltaMediaEje,
                    $"Media del eje '{eje}'");
            }
        }

        // % inválido (puntos porcentuales).
        AgregarSiExcede(
            regresiones,
            TipoRegresion.PorcentajeInvalido,
            eje: null,
            baseline.Invalidos.Porcentaje,
            actual.Invalidos.Porcentaje,
            tol.MaxDeltaPorcentajeInvalido,
            "% de salida inválida");

        // Proporción de decisión cerrar.
        AgregarSiExcede(
            regresiones,
            TipoRegresion.ProporcionDecision,
            eje: null,
            baseline.Decisiones.ProporcionCerrar,
            actual.Decisiones.ProporcionCerrar,
            tol.MaxDeltaProporcionCerrar,
            "Proporción de decisión 'cerrar'");

        return new ResultadoComparacion(regresiones);
    }

    private static void AgregarSiExcede(
        List<Regresion> regresiones,
        TipoRegresion tipo,
        string? eje,
        double baseline,
        double actual,
        double tolerancia,
        string etiqueta)
    {
        var delta = actual - baseline;
        if (Math.Abs(delta) > tolerancia)
        {
            regresiones.Add(new Regresion(
                tipo,
                eje,
                baseline,
                actual,
                delta,
                tolerancia,
                $"{etiqueta}: {baseline:0.####} → {actual:0.####} (Δ {delta:+0.####;-0.####;0}, tol {tolerancia:0.####})"));
        }
    }
}
