using ElTejido.Domain.Common;
using ElTejido.Domain.Identidad;

namespace ElTejido.Domain.Usuarios;

public sealed class Usuario
{
    private Usuario(
        string id,
        string nombre,
        NumeroWhatsApp whatsappNormalizado,
        RolUsuario rol,
        EstadoRegistro estado,
        string area,
        string empresa,
        IReadOnlyCollection<string> tags,
        IReadOnlyDictionary<string, object?> propiedadesDinamicas,
        DateTimeOffset creadoEn,
        DateTimeOffset actualizadoEn)
    {
        Id = id;
        Nombre = nombre;
        WhatsappNormalizado = whatsappNormalizado;
        Rol = rol;
        Estado = estado;
        Area = area;
        Empresa = empresa;
        Tags = tags;
        PropiedadesDinamicas = propiedadesDinamicas;
        CreadoEn = creadoEn;
        ActualizadoEn = actualizadoEn;
    }

    public string Id { get; }

    public string Nombre { get; }

    public NumeroWhatsApp WhatsappNormalizado { get; }

    public RolUsuario Rol { get; }

    public EstadoRegistro Estado { get; }

    public string Area { get; }

    public string Empresa { get; }

    public IReadOnlyCollection<string> Tags { get; }

    public IReadOnlyDictionary<string, object?> PropiedadesDinamicas { get; }

    public DateTimeOffset CreadoEn { get; }

    public DateTimeOffset ActualizadoEn { get; }

    public bool EsAdministrativo => Rol is RolUsuario.Admin or RolUsuario.Visor;

    public static Usuario Crear(
        string id,
        string nombre,
        NumeroWhatsApp whatsappNormalizado,
        RolUsuario rol,
        EstadoRegistro estado,
        string area,
        string empresa,
        IEnumerable<string>? tags,
        IReadOnlyDictionary<string, object?>? propiedadesDinamicas,
        DateTimeOffset creadoEn,
        DateTimeOffset actualizadoEn)
    {
        var fechaCreacionUtc = creadoEn.ToUniversalTime();
        var fechaActualizacionUtc = actualizadoEn.ToUniversalTime();

        if (fechaActualizacionUtc < fechaCreacionUtc)
        {
            throw new DomainValidationException(
                "FECHA_ACTUALIZACION_INVALIDA",
                "La fecha de actualizacion no puede ser anterior a la fecha de creacion.");
        }

        return new Usuario(
            DomainGuards.Required(id, nameof(id)),
            DomainGuards.Required(nombre, nameof(nombre)),
            whatsappNormalizado,
            rol,
            estado,
            DomainGuards.Required(area, nameof(area)),
            DomainGuards.Required(empresa, nameof(empresa)),
            NormalizeTags(tags),
            NormalizeProperties(propiedadesDinamicas),
            fechaCreacionUtc,
            fechaActualizacionUtc);
    }

    private static IReadOnlyCollection<string> NormalizeTags(IEnumerable<string>? tags)
    {
        if (tags is null)
        {
            return Array.Empty<string>();
        }

        return tags
            .Select(tag => tag.Trim())
            .Where(tag => tag.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyDictionary<string, object?> NormalizeProperties(
        IReadOnlyDictionary<string, object?>? properties)
    {
        if (properties is null || properties.Count == 0)
        {
            return new Dictionary<string, object?>();
        }

        return properties
            .Where(property => !string.IsNullOrWhiteSpace(property.Key))
            .ToDictionary(
                property => property.Key.Trim(),
                property => property.Value,
                StringComparer.Ordinal);
    }
}

