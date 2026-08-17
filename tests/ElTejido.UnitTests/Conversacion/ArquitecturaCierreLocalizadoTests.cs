using FluentAssertions;

namespace ElTejido.UnitTests.Conversacion;

/// <summary>
/// DT-P32-03 §6.3 y §7: guarda arquitectónica. El orquestador no puede volver a leer el cierre de la
/// campaña por su cuenta; toda ruta pasa por <c>IResolutorContenidoCampania</c>. Es una prueba de
/// código fuente a propósito: una lectura nueva compila y pasa las demás pruebas, pero reintroduce el
/// cierre español en hilos en inglés.
/// </summary>
public sealed class ArquitecturaCierreLocalizadoTests
{
    private const string LecturaDirecta = "ConfigConversacional.MensajeCierre";

    [Fact]
    public void OrquestadorConversacion_NoLeeElCierreDeLaCampaniaDirectamente()
    {
        var fuente = File.ReadAllText(RutaFuente("src/ElTejido.Application/Conversacion/OrquestadorConversacion.cs"));

        fuente.Should().NotContain(
            LecturaDirecta,
            "DT-P32-03 §3.1: el cierre se resuelve solo en ResolutorMensajeCierreCampania, que impide el respaldo cruzado entre idiomas");
    }

    [Fact]
    public void ResolutorContenidoCampania_EsElUnicoLectorDelCampoLegacy()
    {
        var fuente = File.ReadAllText(RutaFuente("src/ElTejido.Application/Campanas/ContenidoCampaniaEfectivo.cs"));

        fuente.Should().Contain(LecturaDirecta);
    }

    [Theory]
    [InlineData("src/ElTejido.Application/Conversacion/OrquestadorConversacion.cs")]
    [InlineData("src/ElTejido.Application/WhatsApp/ServicioEnvios.cs")]
    public void Consumidores_NoReconstruyenLocalizacionesPorSuCuenta(string rutaRelativa)
    {
        var fuente = File.ReadAllText(RutaFuente(rutaRelativa));

        fuente.Should().NotContain("TryObtenerLocalizacion");
        fuente.Should().NotContain(".Localizaciones");
    }

    [Fact]
    public void DirectivasLlm_SoloSeConstruyenEnLaPoliticaDeIdioma()
    {
        const string politica = "src/ElTejido.Application/Evaluacion/PoliticaIdiomaLlm.cs";
        var raiz = Path.Combine(DirectorioRepositorio(), "src/ElTejido.Application");
        var archivosConDirectiva = Directory.GetFiles(raiz, "*.cs", SearchOption.AllDirectories)
            .Where(archivo =>
            {
                var fuente = File.ReadAllText(archivo);
                return fuente.Contains("IDIOMA_DE_SALIDA", StringComparison.Ordinal)
                    || fuente.Contains("IDIOMA_ORIENTATIVO", StringComparison.Ordinal);
            })
            .Select(archivo => Path.GetRelativePath(DirectorioRepositorio(), archivo).Replace('\\', '/'))
            .ToArray();

        archivosConDirectiva.Should().Equal(politica);
    }

    [Fact]
    public void Readiness_NoLeeLocalizacionesNiMapeosMetaDirectamente()
    {
        var fuente = File.ReadAllText(
            RutaFuente("src/ElTejido.Application/Configuracion/ServicioReadinessCatalogosTextos.cs"));

        fuente.Should().Contain("IResolutorTextosGlobales")
            .And.Contain("IResolutorContenidoCampania")
            .And.Contain("IResolverPlantillaCanal")
            .And.Contain("IPoliticaIdiomaLlm")
            .And.NotContain("TryObtenerLocalizacion")
            .And.NotContain(".Mapeos");
    }

    private static string RutaFuente(string rutaRelativa)
    {
        var ruta = Path.Combine(DirectorioRepositorio(), rutaRelativa);
        File.Exists(ruta).Should().BeTrue($"debe existir {rutaRelativa}");
        return ruta;
    }

    private static string DirectorioRepositorio()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (directorio is not null && !File.Exists(Path.Combine(directorio.FullName, "ElTejido.sln")))
        {
            directorio = directorio.Parent;
        }

        directorio.Should().NotBeNull("la prueba se ejecuta dentro del repositorio");
        return directorio!.FullName;
    }
}
