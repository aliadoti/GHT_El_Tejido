using ElTejido.Domain.Common;

namespace ElTejido.Domain.Usuarios;

public sealed class Tag
{
    private Tag(
        string id,
        string nombre,
        string tipoTag,
        string? descripcion,
        EstadoRegistro estado,
        DateTimeOffset creadoEn)
    {
        Id = id;
        Nombre = nombre;
        TipoTag = tipoTag;
        Descripcion = descripcion;
        Estado = estado;
        CreadoEn = creadoEn;
    }

    public string Id { get; }

    public string Nombre { get; }

    public string TipoTag { get; }

    public string? Descripcion { get; }

    public EstadoRegistro Estado { get; }

    public DateTimeOffset CreadoEn { get; }

    public static Tag Crear(
        string id,
        string nombre,
        string tipoTag,
        string? descripcion,
        EstadoRegistro estado,
        DateTimeOffset creadoEn)
    {
        return new Tag(
            DomainGuards.Required(id, nameof(id)),
            DomainGuards.Required(nombre, nameof(nombre)),
            DomainGuards.Required(tipoTag, nameof(tipoTag)),
            string.IsNullOrWhiteSpace(descripcion) ? null : descripcion.Trim(),
            estado,
            creadoEn.ToUniversalTime());
    }
}

