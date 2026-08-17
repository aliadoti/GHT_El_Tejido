using ElTejido.Domain.Campanas;
using ElTejido.Domain.Localizacion;

namespace ElTejido.Application.WhatsApp;

public static class ProblemasPlantillaCanal
{
    public const string PlantillaRefFaltante = "plantilla_ref_faltante";
    public const string NombreFaltante = "nombre_faltante";
    public const string IdiomaMetaFaltante = "idioma_meta_faltante";
    public const string ComponenteVacio = "componente_vacio";
    public const string ComponenteDuplicado = "componente_duplicado";
}

public abstract record ResultadoPlantillaCanal(IdiomaConversacion Idioma)
{
    public sealed record Disponible(
        IdiomaConversacion Idioma,
        PlantillaWhatsApp Plantilla,
        IReadOnlyList<string> Componentes)
        : ResultadoPlantillaCanal(Idioma);

    public sealed record NoDisponible(
        IdiomaConversacion Idioma,
        IReadOnlyList<string> Problemas,
        bool NombreConfigurado,
        bool IdiomaMetaConfigurado,
        IReadOnlyList<string> Componentes)
        : ResultadoPlantillaCanal(Idioma);
}

/// <summary>Único puente entre el idioma interno y los códigos físicos aprobados por Meta.</summary>
public interface IResolverPlantillaCanal
{
    ResultadoPlantillaCanal Resolver(string? plantillaRef, IdiomaConversacion idioma);

    ResultadoPlantillaCanal ResolverLegacy(PlantillaWhatsApp? respaldo = null);
}

public sealed class ResolverPlantillaCanal : IResolverPlantillaCanal
{
    private readonly OpcionesPlantillaEnvioInicial _opciones;

    public ResolverPlantillaCanal(OpcionesPlantillaEnvioInicial opciones)
        => _opciones = opciones;

    public ResultadoPlantillaCanal Resolver(string? plantillaRef, IdiomaConversacion idioma)
    {
        if (string.IsNullOrWhiteSpace(plantillaRef))
        {
            return NoDisponible(idioma, [ProblemasPlantillaCanal.PlantillaRefFaltante], null);
        }

        var alias = plantillaRef.Trim();
        var configurada = _opciones.Mapeos.TryGetValue(alias, out var porIdioma)
            && porIdioma.TryGetValue(idioma.Codigo, out var encontrada)
                ? encontrada
                : null;
        return ResolverConfigurada(idioma, configurada);
    }

    public ResultadoPlantillaCanal ResolverLegacy(PlantillaWhatsApp? respaldo = null)
        => ResolverConfigurada(
            IdiomaConversacion.Espanol,
            new PlantillaEnvioInicialConfigurada
            {
                Nombre = _opciones.Nombre,
                Idioma = string.IsNullOrWhiteSpace(_opciones.Idioma) ? respaldo?.Idioma ?? string.Empty : _opciones.Idioma,
                Componentes = _opciones.Componentes.Length == 0
                    ? respaldo?.Componentes.ToArray() ?? []
                    : _opciones.Componentes,
            });

    private static ResultadoPlantillaCanal ResolverConfigurada(
        IdiomaConversacion idioma,
        PlantillaEnvioInicialConfigurada? configurada)
    {
        var nombreConfigurado = !string.IsNullOrWhiteSpace(configurada?.Nombre);
        var idiomaMetaConfigurado = !string.IsNullOrWhiteSpace(configurada?.Idioma);
        var componentesOriginales = configurada?.Componentes ?? [];
        var componentes = componentesOriginales
            .Where(componente => !string.IsNullOrWhiteSpace(componente))
            .Select(componente => componente.Trim())
            .ToArray();
        var problemas = new List<string>();
        if (!nombreConfigurado)
        {
            problemas.Add(ProblemasPlantillaCanal.NombreFaltante);
        }

        if (!idiomaMetaConfigurado)
        {
            problemas.Add(ProblemasPlantillaCanal.IdiomaMetaFaltante);
        }

        if (componentesOriginales.Any(string.IsNullOrWhiteSpace))
        {
            problemas.Add(ProblemasPlantillaCanal.ComponenteVacio);
        }

        if (componentes.Distinct(StringComparer.Ordinal).Count() != componentes.Length)
        {
            problemas.Add(ProblemasPlantillaCanal.ComponenteDuplicado);
        }

        if (problemas.Count > 0)
        {
            return NoDisponible(idioma, problemas, configurada);
        }

        return new ResultadoPlantillaCanal.Disponible(
            idioma,
            PlantillaWhatsApp.Crear(configurada!.Nombre, configurada.Idioma, componentes),
            componentes);
    }

    private static ResultadoPlantillaCanal.NoDisponible NoDisponible(
        IdiomaConversacion idioma,
        IReadOnlyList<string> problemas,
        PlantillaEnvioInicialConfigurada? configurada)
        => new(
            idioma,
            problemas,
            !string.IsNullOrWhiteSpace(configurada?.Nombre),
            !string.IsNullOrWhiteSpace(configurada?.Idioma),
            (configurada?.Componentes ?? [])
                .Where(componente => !string.IsNullOrWhiteSpace(componente))
                .Select(componente => componente.Trim())
                .ToArray());
}
