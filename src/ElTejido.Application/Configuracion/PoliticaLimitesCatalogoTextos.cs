namespace ElTejido.Application.Configuracion;

/// <summary>
/// DT-P32-02 §2.4: limites editoriales del catalogo de textos (P-32, REQ §19).
/// El limite operativo es configurable sin recompilar, pero siempre dentro de un techo compilado.
/// Un exceso devuelve error tipificado; el contenido nunca se recorta ni se mezcla con defaults.
/// </summary>
public sealed record PoliticaLimitesCatalogoTextos
{
    public const int MinFrasesPorGrupo = 1;
    public const int MaxFrasesPorGrupoDefault = 100;
    public const int TechoFrasesPorGrupo = 500;

    public const int MinBytesImportacionJson = 1024;
    public const int MaxBytesImportacionJsonDefault = 262144;
    public const int TechoBytesImportacionJson = 1048576;

    public const int MaxCaracteresMensaje = 1000;
    public const int MaxCaracteresFrase = 200;

    private PoliticaLimitesCatalogoTextos(int maxFrasesPorGrupo, int maxBytesImportacionJson)
    {
        MaxFrasesPorGrupo = maxFrasesPorGrupo;
        MaxBytesImportacionJson = maxBytesImportacionJson;
    }

    /// <summary>Politica compilada; la usan el respaldo de emergencia y las pruebas puras.</summary>
    public static PoliticaLimitesCatalogoTextos PorDefecto { get; } =
        new(MaxFrasesPorGrupoDefault, MaxBytesImportacionJsonDefault);

    public int MaxFrasesPorGrupo { get; }

    public int MaxBytesImportacionJson { get; }

    /// <summary>
    /// Ajusta los valores configurados a los techos compilados. Un valor fuera de rango nunca
    /// derriba el arranque: se lleva al limite mas cercano y el operador lo ve en readiness.
    /// </summary>
    public static PoliticaLimitesCatalogoTextos Crear(int maxFrasesPorGrupo, int maxBytesImportacionJson)
        => new(
            Math.Clamp(maxFrasesPorGrupo, MinFrasesPorGrupo, TechoFrasesPorGrupo),
            Math.Clamp(maxBytesImportacionJson, MinBytesImportacionJson, TechoBytesImportacionJson));
}
