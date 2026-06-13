namespace ElTejido.Domain.Seguridad;

/// <summary>
/// Eventos de seguridad registrados en LogSeguridad.
/// Cubre 03 seccion 3.15, 10 seccion 6.4 y REQ 30.
/// </summary>
public enum TipoEventoSeguridad
{
    SolicitudOtp,
    LoginExitoso,
    LoginFallido,
    RechazoParticipacion,
    RateLimit,
    AnomaliaLlm,
    PromptInjectionSospechoso,
    ErrorEnvio,
}
