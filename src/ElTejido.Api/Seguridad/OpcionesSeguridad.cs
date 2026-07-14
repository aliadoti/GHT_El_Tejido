namespace ElTejido.Api.Seguridad;

/// <summary>
/// Limites de rate limiting configurables (10 §2, §3; seccion <c>Seguridad</c> de 02 §6).
/// Los guardrails de longitud de mensaje y maximos por campania se modelaran con sus modulos
/// duenos (Fase 5); aqui viven los limites de transporte HTTP.
/// </summary>
public sealed class OpcionesSeguridad
{
    public const string Seccion = "Seguridad";

    /// <summary>Solicitudes permitidas por IP/minuto en endpoints publicos (p. ej. <c>/api/auth/*</c>). Default 30 (10 §2).</summary>
    public int RateLimitPublicoPorMinuto { get; set; } = 30;

    /// <summary>Solicitudes permitidas por IP/minuto en el webhook de WhatsApp. Default 60.</summary>
    public int RateLimitWebhookPorMinuto { get; set; } = 60;

    /// <summary>
    /// P-10 — mensajes entrantes de WhatsApp permitidos por <b>número</b> por minuto (10 §2), aplicado
    /// en el procesador entrante antes de resolver el participante. <b>0 o negativo lo desactiva</b>
    /// (default). El valor efectivo lo lee la Infraestructura de <c>Seguridad:RateNumeroWhatsAppPorMinuto</c>;
    /// esta propiedad documenta el default y lo expone a los límites HTTP.
    /// </summary>
    public int RateNumeroWhatsAppPorMinuto { get; set; }
}
