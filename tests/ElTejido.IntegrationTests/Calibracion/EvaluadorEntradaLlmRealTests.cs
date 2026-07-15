using System.Runtime.CompilerServices;
using ElTejido.Application.Common;
using ElTejido.Application.Evaluacion;
using ElTejido.Application.Seguridad;
using ElTejido.Calibracion;
using ElTejido.Domain.Evaluacion;
using FluentAssertions;
using NSubstitute;

namespace ElTejido.IntegrationTests.Calibracion;

/// <summary>
/// Verifica la ruta de construcción del runner (config de staging → tripleta de dominio →
/// <c>ContextoEvaluacion</c> → <c>EvaluadorLlm</c> real) con un <c>ILlmClient</c> <b>mockeado</b>: sin
/// costo, corre en CI. Es la red de seguridad de que la plantilla `staging-triplet.example.json` arma
/// entidades de dominio válidas (pesos de rúbrica, límites, escala) y el harness agrega bien.
/// </summary>
public sealed class EvaluadorEntradaLlmRealTests
{
    private const string SalidaValida =
        "{\"calificacion_por_criterio\":[{\"criterio\":\"claridad\",\"puntaje\":4,\"justificacion\":\"ok\"}],"
        + "\"calificacion_total\":4,\"explicacion\":\"buena\",\"retroalimentacion_usuario\":\"Gracias\","
        + "\"recomendacion\":\"cerrar\",\"repregunta_sugerida\":\"\",\"temas\":[\"aprendizaje\"],"
        + "\"entidades\":[\"equipo\"],\"anomalia_seguridad\":false}";

    [Fact]
    public async Task Runner_ConConfigEjemplo_ConstruyeTripletaYAgrega()
    {
        var config = ConfiguracionRunner.CargarDesdeArchivo(RutaEjemplo());
        var goldenSet = CargadorGoldenSet.CargarDesdeArchivo(RutaGoldenSet());

        var client = Substitute.For<ILlmClient>();
        client.CompletarJsonAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LlmRespuesta(SalidaValida, UsoTokensLlm.Crear(120, 40)));

        var evaluadorLlm = new EvaluadorLlm(
            client,
            Substitute.For<IRepositorioLogSeguridad>(),
            Substitute.For<IProveedorCorrelacion>(),
            TimeProvider.System);
        var runner = new EvaluadorEntradaLlmReal(evaluadorLlm, config);

        var metadatos = runner.Metadatos(DateTimeOffset.UnixEpoch);
        var reporte = await CorredorCalibracion.CorrerAsync(goldenSet, n: 1, runner, metadatos, CancellationToken.None);

        // La tripleta se construyó desde el config (rúbrica/prompt/modelo versionados).
        reporte.Metadatos.RubricaRef.Should().Be(config.Rubrica.Id);
        reporte.Metadatos.VersionRubrica.Should().Be(config.Rubrica.Version);
        reporte.Metadatos.Modelo.Should().Be(config.Modelo);
        reporte.Metadatos.Precio.Should().NotBeNull();

        // Corrió cada entrada del golden set una vez y agregó salidas válidas.
        reporte.TotalEntradas.Should().Be(goldenSet.Entradas.Count);
        reporte.TotalMuestras.Should().Be(goldenSet.Entradas.Count);
        reporte.Invalidos.Invalidas.Should().Be(0);
        reporte.Decisiones.Cerrar.Should().Be(goldenSet.Entradas.Count);
        reporte.DistribucionTotal.Media.Should().Be(4d);
        reporte.Tokens.Total.Should().Be((120 + 40) * goldenSet.Entradas.Count);
    }

    [Fact]
    public void ConfigEjemplo_NoContieneApiKeyInline()
    {
        // Regla de oro §6: cero secretos en el repo. La plantilla no debe traer 'apiKey'.
        var texto = File.ReadAllText(RutaEjemplo());

        texto.Should().NotContain("\"apiKey\"");
    }

    private static string RutaEjemplo() => Path.Combine(DirCalibracion(), "staging-triplet.example.json");

    private static string RutaGoldenSet() => Path.Combine(DirCalibracion(), "golden-set.json");

    private static string DirCalibracion()
    {
        var dir = Path.GetDirectoryName(RutaEsteArchivo())!;
        var raiz = Path.GetFullPath(Path.Combine(dir, "..", "..", ".."));
        return Path.Combine(raiz, "tests", "Calibracion");
    }

    private static string RutaEsteArchivo([CallerFilePath] string ruta = "") => ruta;
}
