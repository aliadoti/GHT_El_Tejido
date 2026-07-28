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
        string? respuestaRef,
        string? evaluacionRef,
        string? ideaRef,
        string? versionIdeaRef,
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
        IdeaRef = ideaRef;
        VersionIdeaRef = versionIdeaRef;
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

    /// <summary>Aporte de origen. Siempre presente en artefactos <c>respuesta</c>; nulo en los de idea.</summary>
    public string? RespuestaRef { get; }

    /// <summary>Evaluación de origen. Nula cuando la idea aún no tiene evaluación vigente (I-19 §10).</summary>
    public string? EvaluacionRef { get; }

    /// <summary>I-19 (03 §3.10): idea lógica del artefacto canónico; obligatorio si el tipo es <c>idea</c>.</summary>
    public string? IdeaRef { get; }

    /// <summary>I-19: versión consolidada exacta que se renderizó, si la hay.</summary>
    public string? VersionIdeaRef { get; }

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
        string? respuestaRef,
        string? evaluacionRef,
        string contenidoMarkdown,
        string blobPath,
        EstadoArtefacto estado,
        int version,
        DateTimeOffset creadoEn,
        DateTimeOffset actualizadoEn,
        string? ideaRef = null,
        string? versionIdeaRef = null)
    {
        if (version <= 0)
        {
            throw new DomainValidationException(
                "VERSION_ARTEFACTO_INVALIDA",
                "La version del artefacto debe ser mayor que cero.");
        }

        // I-19 (03 §3.10): el artefacto canonico de una idea se ancla a `ideaRef` y puede no tener
        // evaluacion vigente; los demas tipos conservan su invariante historica sobre `respuestaRef`.
        var esIdea = tipoArtefacto == TipoArtefactoMarkdown.Idea;
        if (esIdea != !string.IsNullOrWhiteSpace(ideaRef))
        {
            throw new DomainValidationException(
                "IDEA_REF_ARTEFACTO_INVALIDA",
                "Solo un artefacto de tipo idea referencia una idea, y siempre debe hacerlo.");
        }

        if (!esIdea && string.IsNullOrWhiteSpace(respuestaRef))
        {
            throw new DomainValidationException(
                "RESPUESTA_REF_ARTEFACTO_INVALIDA",
                "Un artefacto que no es de idea debe referenciar su respuesta.");
        }

        return new ArtefactoMarkdown(
            DomainGuards.Required(id, nameof(id)),
            DomainGuards.Required(campaniaId, nameof(campaniaId)),
            tipoArtefacto,
            DomainGuards.Required(usuarioId, nameof(usuarioId)),
            DomainGuards.Required(preguntaId, nameof(preguntaId)),
            Normalizar(respuestaRef),
            Normalizar(evaluacionRef),
            Normalizar(ideaRef),
            Normalizar(versionIdeaRef),
            DomainGuards.Required(contenidoMarkdown, nameof(contenidoMarkdown)),
            DomainGuards.Required(blobPath, nameof(blobPath)),
            estado,
            version,
            creadoEn.ToUniversalTime(),
            actualizadoEn.ToUniversalTime());
    }

    private static string? Normalizar(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
