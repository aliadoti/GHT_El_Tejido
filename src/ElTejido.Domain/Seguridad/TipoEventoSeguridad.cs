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

    // P-03: accion administrativa auditable (p. ej. reinicio de datos de prueba de una campania o
    // participante). Aditivo al final para preservar los valores existentes (03 §3.15).
    AccionAdministrativa,
}
