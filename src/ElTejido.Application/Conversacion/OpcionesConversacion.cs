namespace ElTejido.Application.Conversacion;

/// <summary>
/// Opciones del ciclo de vida conversacional (seccion de configuracion <c>Conversacion</c>). La
/// expiracion blinda el sistema cerrando hilos que llevan demasiado tiempo sin respuesta del
/// participante (p. ej. abandono tras recibir la pregunta o la retroalimentacion).
/// </summary>
public sealed class OpcionesConversacion
{
    public const string Seccion = "Conversacion";

    /// <summary>
    /// Horas sin actividad tras las cuales un hilo ABIERTO se cierra automaticamente.
    /// <b>0 o negativo desactiva</b> la expiracion (default desactivado).
    /// </summary>
    public int HorasExpiracionSinRespuesta { get; set; }

    /// <summary>Periodicidad del barrido de expiracion, en minutos (minimo 1; default 15).</summary>
    public int IntervaloRevisionMinutos { get; set; } = 15;

    /// <summary>
    /// Umbral de <b>cierre anticipado por calificacion alta</b> (05 §4.4), expresado como fraccion de
    /// la escala de la rubrica en [0,1]. Si la calificacion total de una evaluacion valida alcanza
    /// <c>Min + Umbral * (Max - Min)</c>, NO se ofrece una revision aunque queden repreguntas: se
    /// felicita y se cierra/avanza. <b>0 o negativo desactiva</b> (default desactivado): se mantiene la
    /// revision deterministica de siempre.
    /// </summary>
    public double UmbralCierreAnticipado { get; set; }

    /// <summary>
    /// Largo maximo (en caracteres ya normalizados) para que una coincidencia por contencion de una
    /// frase de continuar cuente como intencion del participante. Acota falsos positivos sobre
    /// respuestas mejoradas largas. Una igualdad exacta con una frase siempre cuenta. Default 40.
    /// </summary>
    public int MaxCaracteresIntencionContinuar { get; set; } = 40;

    /// <summary>
    /// Frases con las que el participante expresa que ya esta conforme y quiere continuar a la siguiente
    /// pregunta (05 §4.4). Si se deja vacio, el orquestador usa <see cref="DetectorIntencionContinuar.FrasesPorDefecto"/>.
    /// </summary>
    public IList<string> FrasesContinuar { get; set; } = new List<string>();

    /// <summary>
    /// Interruptor global de los <b>cupos por usuario/campania</b> (10 §2): cuando esta en <c>true</c>,
    /// el orquestador aplica <c>Campania.ConfigSeguridad.MaxMensajesPorUsuario</c> (al exceder, el
    /// entrante se descarta con rechazo neutral silencioso y se registra <c>RateLimit</c>) y
    /// <c>MaxLlamadasLlmPorUsuario</c> (al exceder, no se llama al LLM: la respuesta queda
    /// <c>recibida</c> y el hilo cierra elegante con el mensaje de cierre). <b>Default <c>false</c></b>
    /// (D1: nada nuevo activo por defecto); antes de habilitarlo hay que dimensionar los limites de la
    /// campania (&#8776; preguntas x (1 + MaxRepreguntas) + margen).
    /// </summary>
    public bool CuposHabilitados { get; set; }

    /// <summary>
    /// Techo duro de <b>turnos entrantes por hilo</b> (incluido el primer contacto): garantiza la
    /// terminacion de cualquier conversacion aunque el LLM u otras reglas pidan seguir. Al alcanzarlo,
    /// el siguiente entrante se registra como <c>recibida</c> sin evaluar y el hilo cierra elegante
    /// (mismo camino que las revisiones agotadas), registrando <c>RateLimit</c>.
    /// <b>0 o negativo desactiva</b> (default desactivado).
    /// </summary>
    public int MaxTurnosPorHilo { get; set; }

    /// <summary>
    /// Kill-switch global de I-06. Por defecto respeta la configuracion por campania; en <c>false</c>
    /// fuerza el flujo 1-idea sin modificar datos ni requerir redeploy.
    /// </summary>
    public bool SegmentacionIdeas { get; set; } = true;

    /// <summary>Maximo de ideas procesadas por mensaje cuando I-06 esta activo. Default 5.</summary>
    public int MaxIdeasPorMensaje { get; set; } = 5;

    /// <summary>Largo minimo de una idea valida tras trim para evitar fragmentacion. Default 30.</summary>
    public int LongitudMinimaIdea { get; set; } = 30;

    /// <summary>
    /// Kill-switch global del <b>tejido colectivo</b> (I-09, 05 §4.8). Por defecto respeta la
    /// configuracion por campania (<c>Campania.ConfigConversacional.TejidoColectivo</c>); en
    /// <c>false</c> apaga el tejido para todas las campanias sin redeploy (rollback operativo).
    /// </summary>
    public bool TejidoColectivo { get; set; } = true;

    /// <summary>Maximo de aportes de la comunidad a recuperar por conversacion (I-09). Default 3.</summary>
    public int TopKAportes { get; set; } = 3;

    /// <summary>
    /// Presupuesto de tokens del bloque <c>APORTES_DE_LA_COMUNIDAD</c> (I-09, 08 §3.2): el bloque se
    /// trunca antes de armar el prompt para acotar costo/latencia. <b>0 o negativo omite el bloque</b>
    /// (tejido efectivamente apagado). Default 300.
    /// </summary>
    public int PresupuestoTokensTejido { get; set; } = 300;

    /// <summary>
    /// Fraccion minima de keywords de la consulta que un aporte debe cubrir para ser candidato del
    /// tejido (I-09, Opcion A lexica), en [0,1]. Acota el ruido. Default 0.1.
    /// </summary>
    public double UmbralSolapamientoTejido { get; set; } = 0.1;

    /// <summary>
    /// Flag global de la <b>Opcion B (embeddings)</b> del tejido (I-09). <b>Default <c>false</c></b>
    /// (diferida tras el Hito); el core Sprint 1b implementa solo la Opcion A lexica. Reservado para
    /// enchufar <c>RecuperadorSemanticoBaseConocimiento</c> sin tocar el orquestador.
    /// </summary>
    public bool RecuperacionSemantica { get; set; }

    /// <summary>
    /// Kill-switch global de I-05. Por defecto respeta <c>Campania.ConfigConversacional.Parafraseo</c>;
    /// en <c>false</c> no solicita ni muestra el parafraseo para ninguna campaña, sin redeploy.
    /// </summary>
    public bool Parafraseo { get; set; } = true;

    /// <summary>
    /// Máximo de caracteres del parafraseo que se muestra al participante (I-05). Si excede el
    /// límite, el evaluador conserva solo frases completas; 400 es el default operativo.
    /// </summary>
    public int MaxCaracteresParafraseo { get; set; } = 400;

    /// <summary>Textos operativos del orquestador que se pueden sobreescribir por configuracion.</summary>
    public OpcionesMensajesConversacion Mensajes { get; set; } = new();
}

public sealed class OpcionesMensajesConversacion
{
    public const string SaludoPrimerContactoDefault =
        "¡Hola! Gracias por escribirnos. Para participar, responde a esta pregunta:";

    public const string SaludoSiguientePreguntaDefault =
        "Continuemos con la siguiente pregunta:";

    public const string InvitacionMejoraDefault =
        "Si quieres, puedes enviarme una version mejorada de tu respuesta con base en esta "
        + "retroalimentacion y la tomare en cuenta.";

    public const string MensajeConfiguracionNoDisponibleDefault =
        "Hay un problema con la configuracion de esta campania. Contacta al administrador del sistema.";

    public const string MensajeCalificacionAltaDefault =
        "¡Excelente! Tu respuesta ya esta muy completa, asi que avanzamos.";

    public const string AcuseContinuarDefault =
        "¡Perfecto, sigamos!";

    /// <summary>
    /// Coletillas (variantes) que invitan al participante a quedarse o seguir, ensenando ademas la frase
    /// de salida del "no quiero mejorar". Se rotan por turno/hilo para que el flujo no repita siempre la
    /// misma frase y se sienta conversacional. Si la lista queda vacia, no se anexa coletilla.
    /// </summary>
    public static readonly IReadOnlyList<string> InvitacionContinuarVariantesDefault = new[]
    {
        "Si ya te sientes conforme, escribeme algo como \"asi esta bien\" y seguimos.",
        "Y si prefieres dejarla asi, solo dime \"listo\" y continuamos.",
        "Cuando quieras cerrar este punto, respondeme \"sigamos\" y pasamos a lo siguiente.",
    };

    public string SaludoPrimerContacto { get; set; } = SaludoPrimerContactoDefault;

    public string SaludoSiguientePregunta { get; set; } = SaludoSiguientePreguntaDefault;

    public string InvitacionMejora { get; set; } = InvitacionMejoraDefault;

    /// <summary>
    /// Variantes de la invitacion a mejorar usadas como respaldo cuando el LLM no devuelve una
    /// <c>RepreguntaSugerida</c> natural. Se rotan por turno/hilo para variar el texto. Vacia = usa
    /// <see cref="InvitacionMejora"/>.
    /// </summary>
    public IReadOnlyList<string> InvitacionMejoraVariantes { get; set; } = new List<string>();

    /// <summary>
    /// Coletillas que invitan a seguir/cerrar y ensenan la frase de salida (manejo natural del "no
    /// quiero mejorar"). Se anexan a la invitacion y se rotan por turno/hilo. Vacia = usa
    /// <see cref="InvitacionContinuarVariantesDefault"/>.
    /// </summary>
    public IReadOnlyList<string> InvitacionContinuarVariantes { get; set; } = new List<string>();

    public string MensajeConfiguracionNoDisponible { get; set; } = MensajeConfiguracionNoDisponibleDefault;

    /// <summary>Felicitacion que antecede al cierre cuando una respuesta supera el umbral de calificacion alta.</summary>
    public string MensajeCalificacionAlta { get; set; } = MensajeCalificacionAltaDefault;

    /// <summary>Acuse calido que antecede al cierre cuando el participante pide continuar a la siguiente pregunta.</summary>
    public string AcuseContinuar { get; set; } = AcuseContinuarDefault;

    /// <summary>
    /// Variantes del acuse de continuar; se rotan por hilo para no repetir siempre la misma frase.
    /// Vacia = usa <see cref="AcuseContinuar"/>.
    /// </summary>
    public IReadOnlyList<string> AcuseContinuarVariantes { get; set; } = new List<string>();
}
