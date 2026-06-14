namespace ElTejido.Application.WhatsApp;

/// <summary>
/// Resultado de un envio saliente por WhatsApp (05 §2.1). No transporta secretos ni PII; el
/// <paramref name="Error"/> es solo un detalle tecnico breve para trazabilidad/telemetria (10 §6).
/// </summary>
public sealed record EnvioResultado(bool Exito, string? WhatsappMessageId, string? Error)
{
    public static EnvioResultado Ok(string? whatsappMessageId)
        => new(true, whatsappMessageId, null);

    public static EnvioResultado Fallo(string error)
        => new(false, null, error);
}
