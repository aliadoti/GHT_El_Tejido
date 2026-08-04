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

    // I-17: telemetria/calibracion del sellado de madurez (03 §3.8). Registra la distribucion
    // maduro/incubacion por campania para dimensionar la base de incubacion y calibrar el umbral. Sin
    // PII de texto: solo el nivel sellado, el score, el valor de corte, la escala y el origen del
    // umbral. Aditivo al final para preservar los valores existentes (03 §3.15, 10 §6.2).
    ClasificacionMadurez,

    // I-18: transiciones de la cola sin texto ni PII.
    CoachingSecuencialIdeas,

    // I-19 (10 §6.2): transiciones de una idea consolidada
    // (propuesta|confirmada|corregida|evaluada|reabierta|cerrada|fallback) con indice, version, estado,
    // motivo y tokens de consolidacion. Nunca incluye el aporte ni la parafrasis. Aditivo al final para
    // preservar los valores existentes (03 §3.15).
    ConsolidacionProgresivaIdeas,

    // I-20 (10 §6.2): una entrada por llamada al redactor de turnos, con el acto, si se redacto o se
    // uso el respaldo, el motivo tecnico al degradar y los tokens de esa llamada. Nunca incluye el
    // texto redactado ni el rechazado. Aditivo al final para preservar los valores existentes.
    RedaccionConversacional,

    // P-26 (10 §6.2): auditoria del enrutamiento de participacion —
    // accion=ofrecido|seleccionado|invalido|expirado|procesado|cambioCampania con conteo de opciones e
    // ids internos. Nunca incluye el texto del participante ni nombres de campania. Aditivo al final
    // para preservar los valores existentes (03 §3.15).
    EnrutamientoParticipacion,

    // P-27 (10 §6.2): clasificación de intención de control. Solo resultado técnico, intención
    // validada, estado y tokens; nunca el texto del participante ni la salida cruda del modelo.
    ClasificacionIntencionControl,

    // P-28: entrada humana ante saludo/inicio breve sin flujo. Sin texto ni PII adicional.
    DespertarProactivo,
}
