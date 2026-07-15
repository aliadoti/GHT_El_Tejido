using System.Runtime.CompilerServices;

namespace ElTejido.UnitTests.Calibracion;

/// <summary>
/// Localiza el golden set versionado (<c>tests/Calibracion/golden-set.json</c>) anclando en la ruta
/// de este archivo fuente vía <see cref="CallerFilePathAttribute"/>, robusto ante el working dir de
/// ejecución de pruebas (sin copiar el JSON al output).
/// </summary>
internal static class RutasGoldenSet
{
    public static string ArchivoSemillas()
    {
        // .../tests/ElTejido.UnitTests/Calibracion/RutasGoldenSet.cs -> raíz del repo -> tests/Calibracion.
        var dirEsteArchivo = Path.GetDirectoryName(RutaEsteArchivo())!;
        var raizRepo = Path.GetFullPath(Path.Combine(dirEsteArchivo, "..", "..", ".."));
        return Path.Combine(raizRepo, "tests", "Calibracion", "golden-set.json");
    }

    private static string RutaEsteArchivo([CallerFilePath] string ruta = "") => ruta;
}
