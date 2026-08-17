using ElTejido.Domain.Common;
using ElTejido.Domain.Identidad;
using ElTejido.Domain.Localizacion;

namespace ElTejido.Domain.Participantes;

/// <summary>
/// Registro append-only de un mensaje saliente (contenedor participants, partition key campaniaId).
/// Cubre 03 seccion 3.5, REQ 29.6 y ARQ 13.
/// </summary>
public sealed class EnvioMensaje
{
    private EnvioMensaje(
        string id,
        string campaniaId,
        string usuarioId,
        string? mensajeInicialId,
        NumeroWhatsApp numero,
        EstadoEnvio estadoEnvio,
        TipoEnvioMensaje tipo,
        string? whatsappMessageId,
        DateTimeOffset fechaEnvio,
        string? error,
        IdiomaConversacion? idioma,
        string? plantillaRef)
    {
        Id = id;
        CampaniaId = campaniaId;
        UsuarioId = usuarioId;
        MensajeInicialId = mensajeInicialId;
        Numero = numero;
        EstadoEnvio = estadoEnvio;
        Tipo = tipo;
        WhatsappMessageId = whatsappMessageId;
        FechaEnvio = fechaEnvio;
        Error = error;
        IdiomaInterno = idioma;
        PlantillaRef = plantillaRef;
    }

    public string Id { get; }

    public string CampaniaId { get; }

    public string UsuarioId { get; }

    public string? MensajeInicialId { get; }

    public NumeroWhatsApp Numero { get; }

    public EstadoEnvio EstadoEnvio { get; }

    public TipoEnvioMensaje Tipo { get; }

    public string? WhatsappMessageId { get; }

    public DateTimeOffset FechaEnvio { get; }

    public string? Error { get; }

    /// <summary>Snapshot del idioma del usuario al preparar el envío; no contiene contenido del participante.</summary>
    public IdiomaConversacion? IdiomaInterno { get; }

    public string? Idioma => IdiomaInterno?.Codigo;

    /// <summary>Alias lógico de plantilla resuelto para el envío, sin identificador físico de Meta.</summary>
    public string? PlantillaRef { get; }

    public static EnvioMensaje Crear(
        string id,
        string campaniaId,
        string usuarioId,
        string? mensajeInicialId,
        NumeroWhatsApp numero,
        EstadoEnvio estadoEnvio,
        TipoEnvioMensaje tipo,
        string? whatsappMessageId,
        DateTimeOffset fechaEnvio,
        string? error,
        string? idioma = null,
        string? plantillaRef = null)
    {
        return new EnvioMensaje(
            DomainGuards.Required(id, nameof(id)),
            DomainGuards.Required(campaniaId, nameof(campaniaId)),
            DomainGuards.Required(usuarioId, nameof(usuarioId)),
            string.IsNullOrWhiteSpace(mensajeInicialId) ? null : mensajeInicialId.Trim(),
            numero,
            estadoEnvio,
            tipo,
            string.IsNullOrWhiteSpace(whatsappMessageId) ? null : whatsappMessageId.Trim(),
            fechaEnvio.ToUniversalTime(),
            string.IsNullOrWhiteSpace(error) ? null : error.Trim(),
            string.IsNullOrWhiteSpace(idioma) ? null : IdiomaConversacion.Crear(idioma),
            string.IsNullOrWhiteSpace(plantillaRef) ? null : plantillaRef.Trim());
    }
}
