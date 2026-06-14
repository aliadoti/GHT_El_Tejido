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
        IReadOnlyCollection<string> tagsSnapshot)
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
        IEnumerable<string>? tagsSnapshot)
        => new(
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
            NormalizarTags(tagsSnapshot));

    public static IReadOnlyCollection<string> NormalizarTags(IEnumerable<string>? tags)
        => tags is null
            ? Array.Empty<string>()
            : tags.Select(t => t.Trim()).Where(t => t.Length > 0).Distinct(StringComparer.Ordinal).ToArray();
}
