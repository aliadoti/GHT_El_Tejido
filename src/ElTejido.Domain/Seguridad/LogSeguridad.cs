using ElTejido.Domain.Common;

namespace ElTejido.Domain.Seguridad;

/// <summary>
/// Evento de seguridad append-only (contenedor security). Sin codigos, secretos ni PII innecesaria.
/// Cubre 03 seccion 3.15, 10 seccion 6.4 y REQ 30.
/// </summary>
public sealed class LogSeguridad
{
    private LogSeguridad(
        string id,
        TipoEventoSeguridad tipoEvento,
        string? usuarioId,
        string? numero,
        string resultado,
        string? detalle,
        string? correlationId,
        DateTimeOffset timestamp)
    {
        Id = id;
        TipoEvento = tipoEvento;
        UsuarioId = usuarioId;
        Numero = numero;
        Resultado = resultado;
        Detalle = detalle;
        CorrelationId = correlationId;
        Timestamp = timestamp;
    }

    public string Id { get; }

    public TipoEventoSeguridad TipoEvento { get; }

    public string? UsuarioId { get; }

    public string? Numero { get; }

    public string Resultado { get; }

    public string? Detalle { get; }

    public string? CorrelationId { get; }

    public DateTimeOffset Timestamp { get; }

    public static LogSeguridad Crear(
        string id,
        TipoEventoSeguridad tipoEvento,
        string? usuarioId,
        string? numero,
        string resultado,
        string? detalle,
        string? correlationId,
        DateTimeOffset timestamp)
    {
        return new LogSeguridad(
            DomainGuards.Required(id, nameof(id)),
            tipoEvento,
            string.IsNullOrWhiteSpace(usuarioId) ? null : usuarioId.Trim(),
            string.IsNullOrWhiteSpace(numero) ? null : numero.Trim(),
            DomainGuards.Required(resultado, nameof(resultado)),
            string.IsNullOrWhiteSpace(detalle) ? null : detalle.Trim(),
            string.IsNullOrWhiteSpace(correlationId) ? null : correlationId.Trim(),
            timestamp.ToUniversalTime());
    }
}
