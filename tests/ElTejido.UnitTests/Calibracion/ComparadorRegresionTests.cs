using ElTejido.Calibracion;
using FluentAssertions;

namespace ElTejido.UnitTests.Calibracion;

/// <summary>
/// Paso 3 de D5: el comparador detecta regresiones sobre tolerancias con datos deterministas. Verde en CI.
/// </summary>
public sealed class ComparadorRegresionTests
{
    private static readonly MetadatosCorrido Meta =
        new("camp1", "r_general", 1, "pr_eval", 1, "llm_default", "gpt-4o-mini", 1, DateTimeOffset.UnixEpoch);

    private static MuestraCalibracion Valida(decimal total, string criterio, decimal puntaje, string decision)
        => new("a", "cat", false, null, total,
            new Dictionary<string, decimal> { [criterio] = puntaje }, decision, Array.Empty<string>(), 10, 5);

    private static MuestraCalibracion Fallback(string motivo)
        => new("a", "cat", true, motivo, 0m, new Dictionary<string, decimal>(), DecisionCalibracion.Cerrar, Array.Empty<string>(), 10, 0);

    private static ReporteCalibracion Reporte(params MuestraCalibracion[] muestras)
        => AgregadorCalibracion.Agregar(Meta, muestras);

    [Fact]
    public void Comparar_ReportesIdenticos_SinRegresion()
    {
        var baseline = Reporte(Valida(4m, "claridad", 4m, DecisionCalibracion.Cerrar));
        var actual = Reporte(Valida(4m, "claridad", 4m, DecisionCalibracion.Cerrar));

        var resultado = ComparadorRegresion.Comparar(baseline, actual);

        resultado.HayRegresion.Should().BeFalse();
        resultado.Regresiones.Should().BeEmpty();
    }

    [Fact]
    public void Comparar_DentroDeTolerancia_SinRegresion()
    {
        var baseline = Reporte(Valida(4m, "claridad", 4m, DecisionCalibracion.Cerrar));
        var actual = Reporte(Valida(3.6m, "claridad", 3.6m, DecisionCalibracion.Cerrar)); // Δ 0.4 < 0.5

        var resultado = ComparadorRegresion.Comparar(baseline, actual);

        resultado.HayRegresion.Should().BeFalse();
    }

    [Fact]
    public void Comparar_MediaBajaMasQueTolerancia_MarcaRegresion()
    {
        var baseline = Reporte(Valida(4m, "claridad", 4m, DecisionCalibracion.Cerrar));
        var actual = Reporte(Valida(3m, "claridad", 3m, DecisionCalibracion.Cerrar)); // Δ 1.0 > 0.5

        var resultado = ComparadorRegresion.Comparar(baseline, actual);

        resultado.HayRegresion.Should().BeTrue();
        resultado.Regresiones.Should().Contain(r => r.Tipo == TipoRegresion.MediaTotal);
        resultado.Regresiones.Should().Contain(r => r.Tipo == TipoRegresion.MediaEje && r.Eje == "claridad");
    }

    [Fact]
    public void Comparar_SubeInvalido_MarcaRegresion()
    {
        var baseline = Reporte(
            Valida(4m, "claridad", 4m, DecisionCalibracion.Cerrar),
            Valida(4m, "claridad", 4m, DecisionCalibracion.Cerrar));
        var actual = Reporte(
            Valida(4m, "claridad", 4m, DecisionCalibracion.Cerrar),
            Fallback("salida_invalida:no_json")); // 50% inválido vs 0%

        var resultado = ComparadorRegresion.Comparar(baseline, actual);

        resultado.Regresiones.Should().Contain(r => r.Tipo == TipoRegresion.PorcentajeInvalido);
    }

    [Fact]
    public void Comparar_CambiaProporcionDecision_MarcaRegresion()
    {
        var baseline = Reporte(
            Valida(4m, "claridad", 4m, DecisionCalibracion.Cerrar),
            Valida(4m, "claridad", 4m, DecisionCalibracion.Cerrar));
        var actual = Reporte(
            Valida(4m, "claridad", 4m, DecisionCalibracion.Cerrar),
            Valida(4m, "claridad", 4m, DecisionCalibracion.Repreguntar)); // 1.0 -> 0.5

        var resultado = ComparadorRegresion.Comparar(baseline, actual);

        resultado.Regresiones.Should().Contain(r => r.Tipo == TipoRegresion.ProporcionDecision);
    }

    [Fact]
    public void Comparar_EjeApareceYDesaparece_MarcaAmbos()
    {
        var baseline = Reporte(Valida(4m, "claridad", 4m, DecisionCalibracion.Cerrar));
        var actual = Reporte(Valida(4m, "otro_eje", 4m, DecisionCalibracion.Cerrar));

        var resultado = ComparadorRegresion.Comparar(baseline, actual);

        resultado.Regresiones.Should().Contain(r => r.Tipo == TipoRegresion.EjeAusente && r.Eje == "claridad");
        resultado.Regresiones.Should().Contain(r => r.Tipo == TipoRegresion.EjeNuevo && r.Eje == "otro_eje");
    }

    [Fact]
    public void Comparar_ToleranciaLaxa_NoMarca()
    {
        var baseline = Reporte(Valida(4m, "claridad", 4m, DecisionCalibracion.Cerrar));
        var actual = Reporte(Valida(1m, "claridad", 1m, DecisionCalibracion.Cerrar)); // Δ 3.0

        var resultado = ComparadorRegresion.Comparar(baseline, actual, new Tolerancias(MaxDeltaMediaTotal: 5.0, MaxDeltaMediaEje: 5.0));

        resultado.Regresiones.Should().NotContain(r => r.Tipo == TipoRegresion.MediaTotal);
        resultado.Regresiones.Should().NotContain(r => r.Tipo == TipoRegresion.MediaEje);
    }
}
