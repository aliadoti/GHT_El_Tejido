using ElTejido.Domain.Common;

namespace ElTejido.Domain.Campanas;

public sealed class PlantillaWhatsApp
{
    private PlantillaWhatsApp(string nombre, string idioma, IReadOnlyCollection<string> componentes)
    {
        Nombre = nombre;
        Idioma = idioma;
        Componentes = componentes;
    }

    public string Nombre { get; }

    public string Idioma { get; }

    public IReadOnlyCollection<string> Componentes { get; }

    public static PlantillaWhatsApp Crear(string nombre, string idioma, IEnumerable<string>? componentes)
    {
        return new PlantillaWhatsApp(
            DomainGuards.Required(nombre, nameof(nombre)),
            DomainGuards.Required(idioma, nameof(idioma)),
            NormalizeStrings(componentes));
    }

    private static IReadOnlyCollection<string> NormalizeStrings(IEnumerable<string>? values)
    {
        if (values is null)
        {
            return Array.Empty<string>();
        }

        return values
            .Select(value => value.Trim())
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
}
