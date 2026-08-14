using ElTejido.Application.Common;

namespace ElTejido.Application.Configuracion;

/// <summary>
/// DT-P32-02 §5: permite a la gestion de campanias exigir catalogo global por idioma sin conocer el
/// contenido editorial ni la persistencia del catalogo.
/// </summary>
public interface IDisponibilidadCatalogoTextos
{
    /// <summary>
    /// De los idiomas pedidos, los que <b>no</b> tienen una version global activa y valida, en el
    /// mismo orden en que se recibieron. Nunca devuelve mensajes ni frases.
    /// </summary>
    Task<IReadOnlyCollection<string>> ObtenerIdiomasSinCatalogoActivoAsync(
        IReadOnlyCollection<string> idiomas,
        CancellationToken cancellationToken);
}

/// <inheritdoc />
public sealed class DisponibilidadCatalogoTextos : IDisponibilidadCatalogoTextos
{
    private readonly IRepositorioCatalogosTextos _repositorio;
    private readonly PoliticaLimitesCatalogoTextos _limites;

    public DisponibilidadCatalogoTextos(
        IRepositorioCatalogosTextos repositorio,
        OpcionesCatalogoTextos? opciones = null)
    {
        _repositorio = repositorio;
        _limites = (opciones ?? new OpcionesCatalogoTextos()).Limites;
    }

    public async Task<IReadOnlyCollection<string>> ObtenerIdiomasSinCatalogoActivoAsync(
        IReadOnlyCollection<string> idiomas,
        CancellationToken cancellationToken)
    {
        var faltantes = new List<string>();
        foreach (var idioma in idiomas ?? Array.Empty<string>())
        {
            var normalizado = idioma?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(normalizado) || faltantes.Contains(normalizado, StringComparer.Ordinal))
            {
                continue;
            }

            var activo = await _repositorio.ObtenerActivoAsync(normalizado, cancellationToken);
            if (activo is null || !EsValido(activo))
            {
                faltantes.Add(normalizado);
            }
        }

        return faltantes;
    }

    /// <summary>Un catalogo activo cuyo contenido ya no valida cuenta como ausente, no como listo.</summary>
    private bool EsValido(VersionCatalogoTextos version)
    {
        try
        {
            var huella = ValidadorCatalogoTextosConversacion.ValidarYCalcularHuella(
                version.Catalogo.Mensajes,
                version.Catalogo.Frases,
                _limites);
            return string.Equals(huella, version.Catalogo.Huella, StringComparison.Ordinal);
        }
        catch (ErrorValidacion)
        {
            return false;
        }
    }
}
