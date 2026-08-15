using FluentAssertions;

namespace ElTejido.UnitTests.Conversacion;

/// <summary>
/// DT-P32-03 §6.3 y §7: guarda arquitectónica. El orquestador no puede volver a leer el cierre de la
/// campaña por su cuenta; toda ruta pasa por <c>IResolutorMensajeCierreCampania</c>. Es una prueba de
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
    public void ResolutorMensajeCierreCampania_SigueSiendoElUnicoLectorDelCampoLegacy()
    {
        var fuente = File.ReadAllText(RutaFuente("src/ElTejido.Application/Conversacion/ResolutorMensajeCierreCampania.cs"));

        fuente.Should().Contain(LecturaDirecta);
    }

    private static string RutaFuente(string rutaRelativa)
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (directorio is not null && !File.Exists(Path.Combine(directorio.FullName, "ElTejido.sln")))
        {
            directorio = directorio.Parent;
        }

        directorio.Should().NotBeNull("la prueba se ejecuta dentro del repositorio");
        var ruta = Path.Combine(directorio!.FullName, rutaRelativa);
        File.Exists(ruta).Should().BeTrue($"debe existir {rutaRelativa}");
        return ruta;
    }
}
