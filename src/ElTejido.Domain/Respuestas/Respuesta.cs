using ElTejido.Domain.Common;

namespace ElTejido.Domain.Respuestas;

/// <summary>
/// Respuesta del participante a una pregunta (contenedor <c>responses</c>, 03 §3.8, REQ §29.12).
/// Guarda <c>tagsSnapshot</c> con los tags vigentes al responder (REQ §30.1).
/// </summary>
public sealed class Respuesta
{
    private Respuesta(
        string id,
        string campaniaId,
        string usuarioId,
        string preguntaId,
        string conversacionId,
        string texto,
        string canal,
        bool esRepregunta,
        EstadoRespuesta estado,
        DateTimeOffset fecha,
        IReadOnlyCollection<string> tagsSnapshot,
        int? ideaIndice,
        string? respuestaPadreId,
        NivelMadurez nivelMadurez,
        string? ideaRaizId,
        string? respuestaAnteriorId,
        int? revisionIndice)
    {
        Id = id;
        CampaniaId = campaniaId;
        UsuarioId = usuarioId;
        PreguntaId = preguntaId;
        ConversacionId = conversacionId;
        Texto = texto;
        Canal = canal;
        EsRepregunta = esRepregunta;
        Estado = estado;
        Fecha = fecha;
        TagsSnapshot = tagsSnapshot;
        IdeaIndice = ideaIndice;
        RespuestaPadreId = respuestaPadreId;
        NivelMadurez = nivelMadurez;
        IdeaRaizId = ideaRaizId;
        RespuestaAnteriorId = respuestaAnteriorId;
        RevisionIndice = revisionIndice;
    }

    public string Id { get; }

    public string CampaniaId { get; }

    public string UsuarioId { get; }

    public string PreguntaId { get; }

    public string ConversacionId { get; }

    public string Texto { get; }

    public string Canal { get; }

    public bool EsRepregunta { get; }

    public EstadoRespuesta Estado { get; }

    public DateTimeOffset Fecha { get; }

    public IReadOnlyCollection<string> TagsSnapshot { get; }

    /// <summary>Indice 1-based de una idea segmentada; null conserva la respuesta historica 1-idea.</summary>
    public int? IdeaIndice { get; }

    /// <summary>Identificador del mensaje origen para agrupar respuestas segmentadas; null = historica.</summary>
    public string? RespuestaPadreId { get; }

    public string? IdeaRaizId { get; }

    public string? RespuestaAnteriorId { get; }

    public int? RevisionIndice { get; }

    /// <summary>
    /// I-17 — nivel de madurez sellado al evaluar (03 §3.8). Default seguro <see cref="NivelMadurez.Incubacion"/>
    /// para documentos historicos sin el campo. Se reclasifica a <c>Incubacion</c> si el participante
    /// rechaza explicitamente el guardado (§5 punto 4) via <see cref="ReclasificarComoIncubacion"/>.
    /// </summary>
    public NivelMadurez NivelMadurez { get; private set; }

    /// <summary>
    /// I-17 — degrada la respuesta a <see cref="NivelMadurez.Incubacion"/> tras un rechazo explicito del
    /// participante ("guardar salvo que diga no"). Idempotente; nunca promueve a maduro.
    /// </summary>
    public void ReclasificarComoIncubacion() => NivelMadurez = NivelMadurez.Incubacion;

    public static Respuesta Crear(
        string id,
        string campaniaId,
        string usuarioId,
        string preguntaId,
        string conversacionId,
        string texto,
        string canal,
        bool esRepregunta,
        EstadoRespuesta estado,
        DateTimeOffset fecha,
        IEnumerable<string>? tagsSnapshot,
        int? ideaIndice = null,
        string? respuestaPadreId = null,
        NivelMadurez nivelMadurez = NivelMadurez.Incubacion,
        string? ideaRaizId = null,
        string? respuestaAnteriorId = null,
        int? revisionIndice = null)
    {
        if (ideaIndice is <= 0)
        {
            throw new DomainValidationException(
                "IDEA_INDICE_INVALIDO",
                "El indice de idea debe ser mayor que cero.");
        }

        if (ideaIndice.HasValue != !string.IsNullOrWhiteSpace(respuestaPadreId))
        {
            throw new DomainValidationException(
                "TRAZABILIDAD_IDEA_INCOMPLETA",
                "ideaIndice y respuestaPadreId deben informarse juntos.");
        }

        var tieneLinaje = !string.IsNullOrWhiteSpace(ideaRaizId)
            || !string.IsNullOrWhiteSpace(respuestaAnteriorId)
            || revisionIndice.HasValue;
        if (tieneLinaje)
        {
            if (string.IsNullOrWhiteSpace(ideaRaizId) || !revisionIndice.HasValue || revisionIndice < 0)
            {
                throw new DomainValidationException(
                    "LINAJE_REVISION_INCOMPLETO",
                    "El linaje requiere ideaRaizId y revisionIndice no negativo.");
            }

            if ((revisionIndice == 0 && !string.IsNullOrWhiteSpace(respuestaAnteriorId))
                || (revisionIndice > 0 && string.IsNullOrWhiteSpace(respuestaAnteriorId)))
            {
                throw new DomainValidationException(
                    "LINAJE_REVISION_INVALIDO",
                    "La raiz no tiene anterior y toda revision debe apuntar a la version previa.");
            }
        }

        return new(
            DomainGuards.Required(id, nameof(id)),
            DomainGuards.Required(campaniaId, nameof(campaniaId)),
            DomainGuards.Required(usuarioId, nameof(usuarioId)),
            DomainGuards.Required(preguntaId, nameof(preguntaId)),
            DomainGuards.Required(conversacionId, nameof(conversacionId)),
            DomainGuards.Required(texto, nameof(texto)),
            DomainGuards.Required(canal, nameof(canal)),
            esRepregunta,
            estado,
            fecha.ToUniversalTime(),
            NormalizarTags(tagsSnapshot),
            ideaIndice,
            string.IsNullOrWhiteSpace(respuestaPadreId) ? null : respuestaPadreId.Trim(),
            nivelMadurez,
            string.IsNullOrWhiteSpace(ideaRaizId) ? null : ideaRaizId.Trim(),
            string.IsNullOrWhiteSpace(respuestaAnteriorId) ? null : respuestaAnteriorId.Trim(),
            revisionIndice);
    }

    public static IReadOnlyCollection<string> NormalizarTags(IEnumerable<string>? tags)
        => tags is null
            ? Array.Empty<string>()
            : tags.Select(t => t.Trim()).Where(t => t.Length > 0).Distinct(StringComparer.Ordinal).ToArray();
}
