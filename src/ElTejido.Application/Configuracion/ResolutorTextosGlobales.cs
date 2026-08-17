using ElTejido.Domain.Localizacion;

namespace ElTejido.Application.Configuracion;

public enum ModoResolucionTextosGlobales
{
    Runtime,
    Diagnostico,
}

public abstract record ResultadoTextosGlobales(IdiomaConversacion Idioma)
{
    public sealed record Disponible(
        IdiomaConversacion Idioma,
        TextosConversacionResueltos? Textos)
        : ResultadoTextosGlobales(Idioma);

    public sealed record NoDisponible(IdiomaConversacion Idioma, string Codigo)
        : ResultadoTextosGlobales(Idioma);
}

/// <summary>
/// Fachada transversal del catálogo global. Diagnóstico no carga contenido, no altera cache/LKG y
/// no genera auditoría; runtime conserva exactamente el resolutor P-32 existente.
/// </summary>
public interface IResolutorTextosGlobales
{
    Task<ResultadoTextosGlobales> ResolverAsync(
        IdiomaConversacion idioma,
        ModoResolucionTextosGlobales modo,
        CancellationToken cancellationToken);
}

public sealed class ResolutorTextosGlobales : IResolutorTextosGlobales
{
    private readonly IResolutorTextosConversacion _runtime;
    private readonly IDisponibilidadCatalogoTextos _disponibilidad;

    public ResolutorTextosGlobales(
        IResolutorTextosConversacion runtime,
        IDisponibilidadCatalogoTextos disponibilidad)
    {
        _runtime = runtime;
        _disponibilidad = disponibilidad;
    }

    public async Task<ResultadoTextosGlobales> ResolverAsync(
        IdiomaConversacion idioma,
        ModoResolucionTextosGlobales modo,
        CancellationToken cancellationToken)
    {
        if (modo == ModoResolucionTextosGlobales.Runtime)
        {
            return new ResultadoTextosGlobales.Disponible(
                idioma,
                await _runtime.ResolverParaIdiomaAsync(idioma.Codigo, cancellationToken));
        }

        var faltantes = await _disponibilidad.ObtenerIdiomasSinCatalogoActivoAsync(
            [idioma.Codigo],
            cancellationToken);
        return faltantes.Count == 0
            ? new ResultadoTextosGlobales.Disponible(idioma, Textos: null)
            : new ResultadoTextosGlobales.NoDisponible(idioma, "catalogo_activo_faltante");
    }
}
