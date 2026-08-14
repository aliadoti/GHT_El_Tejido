namespace ElTejido.Application.Configuracion;

public enum OrigenTextosConversacion
{
    Legacy,
    Catalogo,
    Cache,
    UltimaVersionValida,
    Emergencia,
}

public sealed record ResultadoTextosConversacion(
    VersionCatalogoTextos? Version,
    OrigenTextosConversacion Origen);

/// <summary>Lectura segura del catalogo efectivo para runtime y previsualizacion administrativa.</summary>
public interface IProveedorTextosConversacion
{
    /// <summary>Respeta el gate; con OFF devuelve origen legacy y no consulta persistencia.</summary>
    Task<ResultadoTextosConversacion> ObtenerParaRuntimeAsync(
        string idioma,
        CancellationToken cancellationToken);

    /// <summary>Ignora el gate para que un administrador pruebe el contenido antes de habilitarlo.</summary>
    Task<ResultadoTextosConversacion> PrevisualizarAsync(
        string idioma,
        CancellationToken cancellationToken);
}

public interface IInvalidacionCacheCatalogosTextos
{
    void Invalidar(string idioma);
}

public sealed class OpcionesCatalogoTextos
{
    private int _maxFrasesPorGrupo = PoliticaLimitesCatalogoTextos.MaxFrasesPorGrupoDefault;
    private int _maxBytesImportacionJson = PoliticaLimitesCatalogoTextos.MaxBytesImportacionJsonDefault;

    public bool Habilitado { get; set; }

    public int CacheSegundos { get; set; } = 60;

    /// <summary>
    /// DT-P32-02 §2.4: limite operativo de frases por grupo. Se ajusta al rango compilado
    /// (<c>1..500</c>) para que un valor mal configurado no derribe el arranque ni abra el techo.
    /// </summary>
    public int MaxFrasesPorGrupo
    {
        get => _maxFrasesPorGrupo;
        set => _maxFrasesPorGrupo = Math.Clamp(
            value,
            PoliticaLimitesCatalogoTextos.MinFrasesPorGrupo,
            PoliticaLimitesCatalogoTextos.TechoFrasesPorGrupo);
    }

    /// <summary>DT-P32-02 §2.4: tamano maximo del JSON de edicion masiva; techo compilado de 1 MiB.</summary>
    public int MaxBytesImportacionJson
    {
        get => _maxBytesImportacionJson;
        set => _maxBytesImportacionJson = Math.Clamp(
            value,
            PoliticaLimitesCatalogoTextos.MinBytesImportacionJson,
            PoliticaLimitesCatalogoTextos.TechoBytesImportacionJson);
    }

    public PoliticaLimitesCatalogoTextos Limites
        => PoliticaLimitesCatalogoTextos.Crear(MaxFrasesPorGrupo, MaxBytesImportacionJson);
}
