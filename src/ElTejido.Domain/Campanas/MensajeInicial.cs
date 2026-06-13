using ElTejido.Domain.Common;

namespace ElTejido.Domain.Campanas;

public sealed class MensajeInicial
{
    private MensajeInicial(
        string id,
        string nombreInterno,
        string texto,
        int orden,
        IReadOnlyCollection<string> variablesDinamicas,
        EstadoRegistro estado,
        PlantillaWhatsApp? plantillaWhatsApp)
    {
        Id = id;
        NombreInterno = nombreInterno;
        Texto = texto;
        Orden = orden;
        VariablesDinamicas = variablesDinamicas;
        Estado = estado;
        PlantillaWhatsApp = plantillaWhatsApp;
    }

    public string Id { get; }

    public string NombreInterno { get; }

    public string Texto { get; }

    public int Orden { get; }

    public IReadOnlyCollection<string> VariablesDinamicas { get; }

    public EstadoRegistro Estado { get; }

    public PlantillaWhatsApp? PlantillaWhatsApp { get; }

    public static MensajeInicial Crear(
        string id,
        string nombreInterno,
        string texto,
        int orden,
        IEnumerable<string>? variablesDinamicas,
        EstadoRegistro estado,
        PlantillaWhatsApp? plantillaWhatsApp)
    {
        if (orden <= 0)
        {
            throw new DomainValidationException(
                "ORDEN_INVALIDO",
                "El orden debe ser mayor que cero.");
        }

        return new MensajeInicial(
            DomainGuards.Required(id, nameof(id)),
            DomainGuards.Required(nombreInterno, nameof(nombreInterno)),
            DomainGuards.Required(texto, nameof(texto)),
            orden,
            NormalizeStrings(variablesDinamicas),
            estado,
            plantillaWhatsApp);
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
