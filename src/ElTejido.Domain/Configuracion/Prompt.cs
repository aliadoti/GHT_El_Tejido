using ElTejido.Domain.Common;

namespace ElTejido.Domain.Configuracion;

/// <summary>
/// Prompt versionado y aprobable del contenedor config. Cubre REQ 18 y 29.9.
/// </summary>
public sealed class Prompt
{
    private Prompt(
        string id,
        string nombre,
        string tipoPrompt,
        string contenido,
        int version,
        EstadoPrompt estado,
        string? aprobadoPor,
        DateTimeOffset? fechaAprobacion,
        DateTimeOffset creadoEn,
        DateTimeOffset actualizadoEn)
    {
        Id = id;
        Nombre = nombre;
        TipoPrompt = tipoPrompt;
        Contenido = contenido;
        Version = version;
        Estado = estado;
        AprobadoPor = aprobadoPor;
        FechaAprobacion = fechaAprobacion;
        CreadoEn = creadoEn;
        ActualizadoEn = actualizadoEn;
    }

    public string Id { get; }

    public string Nombre { get; }

    public string TipoPrompt { get; }

    public string Contenido { get; }

    public int Version { get; }

    public EstadoPrompt Estado { get; }

    public string? AprobadoPor { get; }

    public DateTimeOffset? FechaAprobacion { get; }

    public DateTimeOffset CreadoEn { get; }

    public DateTimeOffset ActualizadoEn { get; }

    public static Prompt Crear(
        string id,
        string nombre,
        string tipoPrompt,
        string contenido,
        int version,
        EstadoPrompt estado,
        string? aprobadoPor,
        DateTimeOffset? fechaAprobacion,
        DateTimeOffset creadoEn,
        DateTimeOffset actualizadoEn)
    {
        if (version <= 0)
        {
            throw new DomainValidationException(
                "VERSION_PROMPT_INVALIDA",
                "La version del prompt debe ser mayor que cero.");
        }

        if ((aprobadoPor is null) != (fechaAprobacion is null))
        {
            throw new DomainValidationException(
                "APROBACION_PROMPT_INVALIDA",
                "La aprobacion del prompt debe incluir usuario y fecha.");
        }

        var fechaCreacionUtc = creadoEn.ToUniversalTime();
        var fechaActualizacionUtc = actualizadoEn.ToUniversalTime();
        if (fechaActualizacionUtc < fechaCreacionUtc)
        {
            throw new DomainValidationException(
                "FECHA_ACTUALIZACION_INVALIDA",
                "La fecha de actualizacion no puede ser anterior a la fecha de creacion.");
        }

        return new Prompt(
            DomainGuards.Required(id, nameof(id)),
            DomainGuards.Required(nombre, nameof(nombre)),
            DomainGuards.Required(tipoPrompt, nameof(tipoPrompt)),
            DomainGuards.Required(contenido, nameof(contenido)),
            version,
            estado,
            string.IsNullOrWhiteSpace(aprobadoPor) ? null : aprobadoPor.Trim(),
            fechaAprobacion?.ToUniversalTime(),
            fechaCreacionUtc,
            fechaActualizacionUtc);
    }
}
