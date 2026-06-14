using ElTejido.Domain.Common;

namespace ElTejido.Domain.Configuracion;

/// <summary>
/// Rubrica versionada del contenedor config. Cubre REQ 17 y 29.8.
/// </summary>
public sealed class Rubrica
{
    private Rubrica(
        string id,
        string nombre,
        string descripcion,
        string contenidoMarkdown,
        EscalaRubrica escala,
        IReadOnlyCollection<CriterioRubrica> criterios,
        int version,
        EstadoRubrica estado,
        DateTimeOffset creadoEn,
        DateTimeOffset actualizadoEn)
    {
        Id = id;
        Nombre = nombre;
        Descripcion = descripcion;
        ContenidoMarkdown = contenidoMarkdown;
        Escala = escala;
        Criterios = criterios;
        Version = version;
        Estado = estado;
        CreadoEn = creadoEn;
        ActualizadoEn = actualizadoEn;
    }

    public string Id { get; }

    public string Nombre { get; }

    public string Descripcion { get; }

    public string ContenidoMarkdown { get; }

    public EscalaRubrica Escala { get; }

    public IReadOnlyCollection<CriterioRubrica> Criterios { get; }

    public int Version { get; }

    public EstadoRubrica Estado { get; }

    public DateTimeOffset CreadoEn { get; }

    public DateTimeOffset ActualizadoEn { get; }

    public static Rubrica Crear(
        string id,
        string nombre,
        string descripcion,
        string contenidoMarkdown,
        EscalaRubrica escala,
        IEnumerable<CriterioRubrica> criterios,
        int version,
        EstadoRubrica estado,
        DateTimeOffset creadoEn,
        DateTimeOffset actualizadoEn)
    {
        if (version <= 0)
        {
            throw new DomainValidationException(
                "VERSION_RUBRICA_INVALIDA",
                "La version de rubrica debe ser mayor que cero.");
        }

        var criteriosNormalizados = criterios.ToArray();
        if (criteriosNormalizados.Length == 0)
        {
            throw new DomainValidationException(
                "CRITERIOS_RUBRICA_REQUERIDOS",
                "La rubrica debe tener al menos un criterio.");
        }

        var fechaCreacionUtc = creadoEn.ToUniversalTime();
        var fechaActualizacionUtc = actualizadoEn.ToUniversalTime();
        if (fechaActualizacionUtc < fechaCreacionUtc)
        {
            throw new DomainValidationException(
                "FECHA_ACTUALIZACION_INVALIDA",
                "La fecha de actualizacion no puede ser anterior a la fecha de creacion.");
        }

        return new Rubrica(
            DomainGuards.Required(id, nameof(id)),
            DomainGuards.Required(nombre, nameof(nombre)),
            DomainGuards.Required(descripcion, nameof(descripcion)),
            DomainGuards.Required(contenidoMarkdown, nameof(contenidoMarkdown)),
            escala,
            criteriosNormalizados,
            version,
            estado,
            fechaCreacionUtc,
            fechaActualizacionUtc);
    }
}
