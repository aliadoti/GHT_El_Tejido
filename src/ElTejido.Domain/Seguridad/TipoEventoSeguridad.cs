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

    // I-01: marca de telemetria/calibracion cuando el cierre anticipado por umbral de rubrica
    // (Conversacion:UmbralCierreAnticipado) dispara. No es una amenaza: sirve para calibrar el
    // umbral en staging (10 §6.2). Aditivo al final para preservar los valores existentes.
    CierreUmbralAnticipado,

    // I-06: metrica operativa de una llamada de segmentacion. El detalle no contiene texto del
    // participante; solo conteos, fallback, truncamiento y tokens reportados por el proveedor.
    SegmentacionIdeas,

    // I-09: metrica operativa del tejido colectivo por conversacion. El detalle NO contiene los
    // resumenes ni texto: solo conteos (aportes recuperados/tejidos), degradacion, error y latencia
    // de recuperacion. Aditivo al final para preservar los valores existentes (03 §3.15, 10 §6.2).
    TejidoColectivo,
}
