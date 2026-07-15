using ElTejido.Calibracion;
using ElTejido.Domain.Evaluacion;
using FluentAssertions;

namespace ElTejido.UnitTests.Calibracion;

/// <summary>
/// Paso 2 de D5: el agregador y el harness (con evaluador mockeado) producen un reporte determinista.
/// Sin LLM real: verde en CI.
/// </summary>
public sealed class AgregadorCalibracionTests
{
    private static MetadatosCorrido Meta(PrecioTokens? precio = null)
        => new("camp1", "r_general", 1, "pr_eval", 1, "llm_default", "gpt-4o-mini", 1, DateTimeOffset.UnixEpoch, precio);

    private static MuestraCalibracion MuestraValida(
        string entradaId,
        decimal total,
        (string, decimal)[] criterios,
        string decision,
        string[] ideas,
        int prompt,
        int completion)
        => new(
            entradaId,
            "cat",
            EsFallback: false,
            MotivoFallback: null,
            total,
            criterios.ToDictionary(c => c.Item1, c => c.Item2, StringComparer.Ordinal),
            decision,
            ideas,
            prompt,
            completion);

    [Fact]
    public void Agregar_CalculaDistribucionDecisionInvalidoYTokens()
    {
        var muestras = new List<MuestraCalibracion>
        {
            MuestraValida("a", 4m, new[] { ("claridad", 4m), ("concrecion", 2m) }, DecisionCalibracion.Cerrar, new[] { "idea1" }, 100, 50),
            MuestraValida("a", 2m, new[] { ("claridad", 2m), ("concrecion", 4m) }, DecisionCalibracion.Repreguntar, new[] { "idea1", "idea2" }, 80, 20),
            new("b", "cat", EsFallback: true, MotivoFallback: "salida_invalida:no_json", 0m,
                new Dictionary<string, decimal>(), DecisionCalibracion.Cerrar, Array.Empty<string>(), 10, 0),
        };

        var reporte = AgregadorCalibracion.Agregar(Meta(new PrecioTokens(1m, 2m)), muestras);

        reporte.TotalEntradas.Should().Be(2);
        reporte.TotalMuestras.Should().Be(3);

        // Distribución total sobre válidas [4, 2]: media 3, desv poblacional 1.
        reporte.DistribucionTotal.Muestras.Should().Be(2);
        reporte.DistribucionTotal.Min.Should().Be(2m);
        reporte.DistribucionTotal.Max.Should().Be(4m);
        reporte.DistribucionTotal.Media.Should().Be(3d);
        reporte.DistribucionTotal.Desviacion.Should().Be(1d);

        reporte.DistribucionPorEje.Select(e => e.Eje).Should().ContainInOrder("claridad", "concrecion");
        reporte.DistribucionPorEje.Single(e => e.Eje == "claridad").Media.Should().Be(3d);

        // Decisiones sobre válidas.
        reporte.Decisiones.Cerrar.Should().Be(1);
        reporte.Decisiones.Repreguntar.Should().Be(1);
        reporte.Decisiones.ProporcionCerrar.Should().Be(0.5d);

        // % inválido: 1 de 3.
        reporte.Invalidos.Invalidas.Should().Be(1);
        reporte.Invalidos.Porcentaje.Should().BeApproximately(33.3333d, 0.0001d);
        reporte.Invalidos.PorMotivo.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new MotivoInvalido("salida_invalida:no_json", 1));

        // Tokens: se suman TODAS (incluye fallback). Costo = 190/1000*1 + 70/1000*2 = 0.33.
        reporte.Tokens.PromptTokens.Should().Be(190);
        reporte.Tokens.CompletionTokens.Should().Be(70);
        reporte.Tokens.Total.Should().Be(260);
        reporte.Tokens.CostoEstimado.Should().Be(0.33m);
        reporte.Tokens.PorEntrada.Single(e => e.EntradaId == "a").Total.Should().Be(250);

        // Ideas por entrada (unión, ordenadas).
        reporte.IdeasPorEntrada.Single(e => e.EntradaId == "a").Ideas.Should().ContainInOrder("idea1", "idea2");
        reporte.IdeasPorEntrada.Single(e => e.EntradaId == "b").Ideas.Should().BeEmpty();
    }

    [Fact]
    public void Agregar_SinPrecio_NoCalculaCosto()
    {
        var muestras = new List<MuestraCalibracion>
        {
            MuestraValida("a", 3m, new[] { ("claridad", 3m) }, DecisionCalibracion.Cerrar, Array.Empty<string>(), 10, 5),
        };

        var reporte = AgregadorCalibracion.Agregar(Meta(), muestras);

        reporte.Tokens.CostoEstimado.Should().BeNull();
    }

    [Fact]
    public async Task Corredor_ConEvaluadorMockeado_CorreNVecesYAgrega()
    {
        var set = new GoldenSet("t", null, new[]
        {
            new EntradaGoldenSet("a", "cat", "texto a", null, null),
            new EntradaGoldenSet("b", "cat", "texto b", null, null),
        });

        var evaluador = new EvaluadorFake((entrada, indice) => entrada.Id == "a"
            ? FabricaEvaluacion.Exito(
                total: indice == 0 ? 4m : 2m,
                recomendacion: indice == 0 ? RecomendacionEvaluacion.Cerrar : RecomendacionEvaluacion.Repreguntar,
                criterios: new[] { ("claridad", indice == 0 ? 4m : 2m) },
                temas: new[] { "tema" },
                uso: UsoTokensLlm.Crear(100, 50))
            : FabricaEvaluacion.Fallback("error_proveedor", UsoTokensLlm.Crear(5, 5)));

        var reporte = await CorredorCalibracion.CorrerAsync(set, n: 2, evaluador, Meta(), CancellationToken.None);

        reporte.Metadatos.N.Should().Be(2);
        reporte.TotalEntradas.Should().Be(2);
        reporte.TotalMuestras.Should().Be(4);
        reporte.Decisiones.Cerrar.Should().Be(1);
        reporte.Decisiones.Repreguntar.Should().Be(1);
        reporte.Invalidos.Invalidas.Should().Be(2);
        reporte.Invalidos.Porcentaje.Should().Be(50d);
        reporte.Invalidos.PorMotivo.Should().ContainSingle().Which.Motivo.Should().Be("error_proveedor");
        reporte.Tokens.Total.Should().Be((100 + 50) * 2 + (5 + 5) * 2);
    }

    [Fact]
    public void Reporte_JsonRoundTrip_PreservaEstructura()
    {
        var reporte = AgregadorCalibracion.Agregar(Meta(new PrecioTokens(1m, 2m)), new List<MuestraCalibracion>
        {
            MuestraValida("a", 4m, new[] { ("claridad", 4m) }, DecisionCalibracion.Cerrar, new[] { "idea1" }, 100, 50),
        });

        var json = EscritorReporteJson.Serializar(reporte);
        var vuelta = EscritorReporteJson.Deserializar(json);

        vuelta.Should().BeEquivalentTo(reporte);
    }

    [Fact]
    public void Reporte_Markdown_ContieneSeccionesClave()
    {
        var reporte = AgregadorCalibracion.Agregar(Meta(), new List<MuestraCalibracion>
        {
            MuestraValida("a", 4m, new[] { ("claridad", 4m) }, DecisionCalibracion.Cerrar, new[] { "idea1" }, 100, 50),
        });

        var md = EscritorReporteMarkdown.Renderizar(reporte);

        md.Should().Contain("# Reporte de calibración — D5");
        md.Should().Contain("Distribución de scores");
        md.Should().Contain("cerrar vs repreguntar");
        md.Should().Contain("Salida inválida");
        md.Should().Contain("Tokens / costo");
        md.Should().Contain("claridad");
    }

    private sealed class EvaluadorFake : IEvaluadorEntrada
    {
        private readonly Func<EntradaGoldenSet, int, ElTejido.Application.Evaluacion.ResultadoEvaluacion> _fn;
        private readonly Dictionary<string, int> _contador = new(StringComparer.Ordinal);

        public EvaluadorFake(Func<EntradaGoldenSet, int, ElTejido.Application.Evaluacion.ResultadoEvaluacion> fn) => _fn = fn;

        public Task<ElTejido.Application.Evaluacion.ResultadoEvaluacion> EvaluarAsync(EntradaGoldenSet entrada, CancellationToken cancellationToken)
        {
            var indice = _contador.TryGetValue(entrada.Id, out var c) ? c : 0;
            _contador[entrada.Id] = indice + 1;
            return Task.FromResult(_fn(entrada, indice));
        }
    }
}
