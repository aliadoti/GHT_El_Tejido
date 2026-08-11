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
    public bool Habilitado { get; set; }

    public int CacheSegundos { get; set; } = 60;
}
