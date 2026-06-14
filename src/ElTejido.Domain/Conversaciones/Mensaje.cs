using ElTejido.Domain.Common;

namespace ElTejido.Domain.Conversaciones;

/// <summary>
/// Mensaje individual del hilo (contenedor <c>conversations</c>, 03 §3.7, REQ §28.3). El
/// <c>whatsappMessageId</c> se puebla en entrantes (idempotencia) y en salientes cuando Meta lo
/// devuelve.
/// </summary>
public sealed class Mensaje
{
    private Mensaje(
        string id,
        string campaniaId,
        string conversacionId,
        DireccionMensaje direccion,
        string texto,
        string? whatsappMessageId,
        DateTimeOffset timestamp)
    {
        Id = id;
        CampaniaId = campaniaId;
        ConversacionId = conversacionId;
        Direccion = direccion;
        Texto = texto;
        WhatsappMessageId = whatsappMessageId;
        Timestamp = timestamp;
    }

    public string Id { get; }

    public string CampaniaId { get; }

    public string ConversacionId { get; }

    public DireccionMensaje Direccion { get; }

    public string Texto { get; }

    public string? WhatsappMessageId { get; }

    public DateTimeOffset Timestamp { get; }

    public static Mensaje Crear(
        string id,
        string campaniaId,
        string conversacionId,
        DireccionMensaje direccion,
        string texto,
        string? whatsappMessageId,
        DateTimeOffset timestamp)
        => new(
            DomainGuards.Required(id, nameof(id)),
            DomainGuards.Required(campaniaId, nameof(campaniaId)),
            DomainGuards.Required(conversacionId, nameof(conversacionId)),
            direccion,
            texto ?? string.Empty,
            string.IsNullOrWhiteSpace(whatsappMessageId) ? null : whatsappMessageId.Trim(),
            timestamp.ToUniversalTime());
}
