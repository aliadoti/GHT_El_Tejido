using ElTejido.Domain.Campanas;
using ElTejido.Domain.Common;

namespace ElTejido.Domain.Respuestas;

/// <summary>
/// Artefacto Markdown durable, atribuido y regenerable (contenedor <c>responses</c>, 03 §3.10,
/// REQ §22). Es cache materializada: la fuente de verdad son los datos operativos (REQ §23.3). El
/// contenido se embebe aqui (consulta rapida) y se guarda en Blob (<see cref="BlobPath"/>).
/// </summary>
public sealed class ArtefactoMarkdown
{
    private ArtefactoMarkdown(
        string id,
        string campaniaId,
        TipoArtefactoMarkdown tipoArtefacto,
        string usuarioId,
        string preguntaId,
        string respuestaRef,
        string evaluacionRef,
        string contenidoMarkdown,
        string blobPath,
        EstadoArtefacto estado,
        int version,
        DateTimeOffset creadoEn,
        DateTimeOffset actualizadoEn)
    {
        Id = id;
        CampaniaId = campaniaId;
        TipoArtefacto = tipoArtefacto;
        UsuarioId = usuarioId;
        PreguntaId = preguntaId;
        RespuestaRef = respuestaRef;
        EvaluacionRef = evaluacionRef;
        ContenidoMarkdown = contenidoMarkdown;
        BlobPath = blobPath;
        Estado = estado;
        Version = version;
        CreadoEn = creadoEn;
        ActualizadoEn = actualizadoEn;
    }

    public string Id { get; }

    public string CampaniaId { get; }

    public TipoArtefactoMarkdown TipoArtefacto { get; }

    public string UsuarioId { get; }

    public string PreguntaId { get; }

    public string RespuestaRef { get; }

    public string EvaluacionRef { get; }

    public string ContenidoMarkdown { get; }

    public string BlobPath { get; }

    public EstadoArtefacto Estado { get; }

    public int Version { get; }

    public DateTimeOffset CreadoEn { get; }

    public DateTimeOffset ActualizadoEn { get; }

    public static ArtefactoMarkdown Crear(
        string id,
        string campaniaId,
        TipoArtefactoMarkdown tipoArtefacto,
        string usuarioId,
        string preguntaId,
        string respuestaRef,
        string evaluacionRef,
        string contenidoMarkdown,
        string blobPath,
        EstadoArtefacto estado,
        int version,
        DateTimeOffset creadoEn,
        DateTimeOffset actualizadoEn)
    {
        if (version <= 0)
        {
            throw new DomainValidationException(
                "VERSION_ARTEFACTO_INVALIDA",
                "La version del artefacto debe ser mayor que cero.");
        }

        return new ArtefactoMarkdown(
            DomainGuards.Required(id, nameof(id)),
            DomainGuards.Required(campaniaId, nameof(campaniaId)),
            tipoArtefacto,
            DomainGuards.Required(usuarioId, nameof(usuarioId)),
            DomainGuards.Required(preguntaId, nameof(preguntaId)),
            DomainGuards.Required(respuestaRef, nameof(respuestaRef)),
            DomainGuards.Required(evaluacionRef, nameof(evaluacionRef)),
            DomainGuards.Required(contenidoMarkdown, nameof(contenidoMarkdown)),
            DomainGuards.Required(blobPath, nameof(blobPath)),
            estado,
            version,
            creadoEn.ToUniversalTime(),
            actualizadoEn.ToUniversalTime());
    }
}
