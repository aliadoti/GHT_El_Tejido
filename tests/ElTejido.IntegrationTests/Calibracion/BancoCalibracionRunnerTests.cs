using System.Runtime.CompilerServices;
using ElTejido.Application.Evaluacion;
using ElTejido.Application.Seguridad;
using ElTejido.Calibracion;
using ElTejido.Infrastructure.Llm;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace ElTejido.IntegrationTests.Calibracion;

/// <summary>
/// Runner opt-in del banco de calibración (D5 §3.4). Corre el golden set contra el <b>LLM real</b> de
/// staging: <b>cuesta dinero</b>, por eso está marcado <c>[Trait("Category","Calibracion")]</c> y
/// EXCLUIDO del <c>dotnet test</c>/CI por defecto (filtro <c>Category!=Calibracion</c>). Además es
/// no-op si faltan las variables de entorno (equivalente local de Key Vault + tripleta de staging).
/// El agregador/comparador ya se prueban mockeados y verdes en CI (proyecto ElTejido.Calibracion).
///
/// Disparo (runbook):
///   export CALIBRACION_CONFIG=/ruta/staging-triplet.json
///   export CALIBRACION_API_KEY=****            # equivalente local de Key Vault; nunca en el repo
///   dotnet test tests/ElTejido.IntegrationTests -c Release --filter "Category=Calibracion"
/// Congelar baseline: copiar el reporte JSON de salida a tests/Calibracion/baseline.json.
/// </summary>
public sealed class BancoCalibracionRunnerTests
{
    [Trait("Category", "Calibracion")]
    [Fact]
    public async Task Correr_ContraLlmRealDeStaging_ProduceReporteYComparaBaseline()
    {
        if (!RunnerCalibracion.EstaConfigurado())
        {
            // Opt-in: sin CALIBRACION_CONFIG + CALIBRACION_API_KEY el runner no llama al LLM real.
            return;
        }

        var config = ConfiguracionRunner.CargarDesdeArchivo(Environment.GetEnvironmentVariable(RunnerCalibracion.VarConfig)!);
        var goldenSet = CargadorGoldenSet.CargarDesdeArchivo(RutaGoldenSet());

        var logSeguridad = Substitute.For<IRepositorioLogSeguridad>();
        using var http = new HttpClient();
        var client = new LlmClientHttp(http, new SecretProviderEntorno(), TimeProvider.System, NullLogger<LlmClientHttp>.Instance);
        var evaluadorLlm = new EvaluadorLlm(client, logSeguridad, new CorrelacionFija(), TimeProvider.System);
        var evaluadorEntrada = new EvaluadorEntradaLlmReal(evaluadorLlm, config);

        var metadatos = evaluadorEntrada.Metadatos(DateTimeOffset.UtcNow);
        var reporte = await CorredorCalibracion.CorrerAsync(goldenSet, config.N, evaluadorEntrada, metadatos, CancellationToken.None);

        var dirSalida = Environment.GetEnvironmentVariable(RunnerCalibracion.VarSalida) ?? DirSalidaPorDefecto();
        Directory.CreateDirectory(dirSalida);
        var sello = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
        await File.WriteAllTextAsync(Path.Combine(dirSalida, $"reporte-{sello}.json"), EscritorReporteJson.Serializar(reporte));
        await File.WriteAllTextAsync(Path.Combine(dirSalida, $"reporte-{sello}.md"), EscritorReporteMarkdown.Renderizar(reporte));

        // Si hay baseline congelado, compara y falla ante regresión sobre tolerancias (D5 §3.3).
        var rutaBaseline = RutaBaseline();
        if (File.Exists(rutaBaseline))
        {
            var baseline = EscritorReporteJson.DeserializarArchivo(rutaBaseline);
            var comparacion = ComparadorRegresion.Comparar(baseline, reporte);
            if (comparacion.HayRegresion)
            {
                var detalle = string.Join("\n", comparacion.Regresiones.Select(r => "  - " + r.Descripcion));
                throw new Xunit.Sdk.XunitException("Regresión de calibración contra el baseline:\n" + detalle);
            }
        }
    }

    private static string RutaGoldenSet() => Path.Combine(DirCalibracion(), "golden-set.json");

    private static string RutaBaseline() => Path.Combine(DirCalibracion(), "baseline.json");

    private static string DirSalidaPorDefecto() => Path.Combine(DirCalibracion(), "salida");

    private static string DirCalibracion()
    {
        // .../tests/ElTejido.IntegrationTests/Calibracion/BancoCalibracionRunnerTests.cs -> raíz repo -> tests/Calibracion.
        var dir = Path.GetDirectoryName(RutaEsteArchivo())!;
        var raiz = Path.GetFullPath(Path.Combine(dir, "..", "..", ".."));
        return Path.Combine(raiz, "tests", "Calibracion");
    }

    private static string RutaEsteArchivo([CallerFilePath] string ruta = "") => ruta;
}
