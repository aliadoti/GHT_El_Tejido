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
        DateTimeOffset timestamp,
        string? campaniaId,
        int promptTokens,
        int completionTokens,
        bool esLlamadaLlm)
    {
        Id = id;
        TipoEvento = tipoEvento;
        UsuarioId = usuarioId;
        Numero = numero;
        Resultado = resultado;
        Detalle = detalle;
        CorrelationId = correlationId;
        Timestamp = timestamp;
        CampaniaId = campaniaId;
        PromptTokens = promptTokens;
        CompletionTokens = completionTokens;
        EsLlamadaLlm = esLlamadaLlm;
    }

    public string Id { get; }

    public TipoEventoSeguridad TipoEvento { get; }

    public string? UsuarioId { get; }

    public string? Numero { get; }

    public string Resultado { get; }

    public string? Detalle { get; }

    public string? CorrelationId { get; }

    public DateTimeOffset Timestamp { get; }

    /// <summary>Identificador interno de campaña para cuotas y agregados técnicos; no es PII.</summary>
    public string? CampaniaId { get; }

    /// <summary>Tokens del prompt, cuando el evento representa una llamada LLM.</summary>
    public int PromptTokens { get; }

    /// <summary>Tokens de completitud, cuando el evento representa una llamada LLM.</summary>
    public int CompletionTokens { get; }

    /// <summary>Indica una invocación efectiva al proveedor, incluso si terminó en fallback.</summary>
    public bool EsLlamadaLlm { get; }

    public static LogSeguridad Crear(
        string id,
        TipoEventoSeguridad tipoEvento,
        string? usuarioId,
        string? numero,
        string resultado,
        string? detalle,
        string? correlationId,
        DateTimeOffset timestamp,
        string? campaniaId = null,
        int promptTokens = 0,
        int completionTokens = 0,
        bool esLlamadaLlm = false)
    {
        return new LogSeguridad(
            DomainGuards.Required(id, nameof(id)),
            tipoEvento,
            string.IsNullOrWhiteSpace(usuarioId) ? null : usuarioId.Trim(),
            string.IsNullOrWhiteSpace(numero) ? null : numero.Trim(),
            DomainGuards.Required(resultado, nameof(resultado)),
            string.IsNullOrWhiteSpace(detalle) ? null : detalle.Trim(),
            string.IsNullOrWhiteSpace(correlationId) ? null : correlationId.Trim(),
            timestamp.ToUniversalTime(),
            string.IsNullOrWhiteSpace(campaniaId) ? null : campaniaId.Trim(),
            Math.Max(0, promptTokens),
            Math.Max(0, completionTokens),
            esLlamadaLlm);
    }
}
