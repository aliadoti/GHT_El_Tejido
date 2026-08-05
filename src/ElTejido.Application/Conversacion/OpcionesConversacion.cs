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
    /// I-17 §7 — <b>default global</b> de la ventana de cierre por <b>inactividad de sesion</b>, en
    /// minutos (granularidad sub-hora del flujo del 20-jul). Lo sobreescribe el override por campaña
    /// <c>ConfigConversacional.MinutosInactividadSesion</c>. <b>0 o negativo desactiva</b> (default off
    /// por D1; valor recomendado 5 que el operador fija en el acta de flags del dia-D).
    /// </summary>
    public int MinutosInactividadSesion { get; set; }

    /// <summary>
    /// Umbral <b>único compartido</b> (P-13 + I-17), fracción de la escala de la rúbrica en [0,1].
    /// Gobierna a la vez el <b>cierre anticipado por calificación alta</b> (05 §4.4) y la
    /// <b>clasificación de madurez</b> de guardado (I-17: <c>maduro</c>/<c>incubacion</c>) + el disparo de
    /// paráfrasis (I-05). Es el <b>default global</b>; lo sobreescriben el override por campaña y el
    /// override por pregunta (precedencia pregunta → campaña → global). <b>I-17: default <c>0.6</c></b>.
    /// <b>0 o negativo desactiva</b> el efecto (nada supera el umbral).
    /// </summary>
    public double UmbralCierreAnticipado { get; set; } = 0.6;

    /// <summary>
    /// Kill-switch global del <b>cierre anticipado</b> (P-13). En <c>false</c> apaga todo cierre
    /// anticipado sin redeploy, <b>sin afectar</b> la clasificación de madurez (I-17). <b>I-17: default
    /// <c>false</c></b> para no encender el cierre al subir el default del umbral a 0.6 (D1 + modelo de
    /// cierre no-determinista del 20-jul); se activa tras calibrar (runbook I-01).
    /// </summary>
    public bool CierreAnticipadoHabilitado { get; set; }

    /// <summary>
    /// Largo maximo (en caracteres ya normalizados) para que una coincidencia por contencion de una
    /// frase de continuar cuente como intencion del participante. Acota falsos positivos sobre
    /// respuestas mejoradas largas. Una igualdad exacta con una frase siempre cuenta. Default 40.
    /// </summary>
    public int MaxCaracteresIntencionContinuar { get; set; } = 40;

    /// <summary>
    /// P-27: kill-switch global del clasificador flexible de intenciones de control. Nace apagado;
    /// los alias deterministas de salida no dependen de esta opción.
    /// </summary>
    public bool ClasificacionIntencionControl { get; set; }

    /// <summary>
    /// P-28: kill-switch global para contestar un saludo/inicio breve cuando no existe flujo ni
    /// afinidad vigente. Nace apagado; un aporte sustantivo sigue entrando directamente por P-26.
    /// </summary>
    public bool DespertarProactivoHabilitado { get; set; }

    /// <summary>P-28: longitud máxima para reconocer un saludo/inicio de forma determinista.</summary>
    public int MaxCaracteresDespertarProactivo { get; set; } = 80;

    /// <summary>
    /// P-29 (§7): kill-switch global del <b>mensaje de pausa</b> que humaniza el cierre por
    /// inactividad. Nace apagado; con él en <c>false</c> el barrido de I-17 §7 sigue cerrando igual,
    /// solo que sin aviso. No gobierna el temporizador, el umbral ni el <c>motivoCierre</c>.
    /// </summary>
    public bool CierrePorTiempoHabilitado { get; set; }

    /// <summary>
    /// P-30: kill-switch global para listar y retomar ideas historicas del propio participante en el
    /// alcance campania/pregunta ya resuelto. Nace apagado; con <c>false</c> se conserva exactamente la
    /// reapertura reciente de I-19/P-26.
    /// </summary>
    public bool RetomarIdeasHabilitado { get; set; }

    /// <summary>
    /// P-28: vocabulario breve de saludo/inicio. Vacío usa
    /// <see cref="DetectorEntradaProactiva.FrasesPorDefecto"/>.
    /// </summary>
    public IList<string> FrasesDespertarProactivo { get; set; } = new List<string>();

    /// <summary>
    /// P-27: longitud máxima del texto normalizado elegible para clasificación flexible. Un valor
    /// menor o igual a cero deshabilita la ruta; el orquestador decidirá la elegibilidad final.
    /// </summary>
    public int MaxCaracteresClasificacionIntencionControl { get; set; } = 160;

    /// <summary>
    /// Frases con las que el participante expresa que ya esta conforme y quiere continuar a la siguiente
    /// pregunta (05 §4.4). Si se deja vacio, el orquestador usa <see cref="DetectorIntencionContinuar.FrasesPorDefecto"/>.
    /// </summary>
    public IList<string> FrasesContinuar { get; set; } = new List<string>();

    /// <summary>
    /// P-24 — frases cortas con las que el participante pide trabajar una propuesta ya mostrada (por
    /// ejemplo, "vamos a mejorarla"). Solo en <c>pendienteConfirmacion</c> confirman implícitamente la
    /// versión vigente para evaluarla; no se guardan como aporte. Vacío = lista compilada.
    /// </summary>
    public IList<string> FrasesSolicitarMejora { get; set; } = new List<string>();

    /// <summary>
    /// P-25 — rollback temporal al flujo I-19 que pide confirmar cada versión con
    /// "¿Es correcto?". En <c>false</c>, una respuesta sustantiva se consolida, confirma
    /// internamente y evalúa en el mismo turno; solo una ambigüedad real pide aclaración.
    /// El POCO conserva <c>true</c> por compatibilidad de consumidores que lo construyen sin
    /// configuración, mientras la aplicación distribuida fija explícitamente <c>false</c>.
    /// </summary>
    public bool ConfirmacionExplicitaIdeasHabilitada { get; set; } = true;

    /// <summary>
    /// I-17 §5.4 — frases con las que el participante <b>rechaza explícitamente</b> que su idea madura se
    /// guarde. Al coincidir en <c>esperandoRepregunta</c>, se degrada la respuesta madura a incubación y
    /// se cierra con un acuse ("guardar salvo que diga no"). Vacio = usa
    /// <see cref="DetectorIntencionContinuar.FrasesRechazoGuardadoPorDefecto"/>.
    /// </summary>
    public IList<string> FrasesRechazoGuardado { get; set; } = new List<string>();

    /// <summary>
    /// I-19 §4.7 — frases con las que el participante pide volver a la <b>idea cerrada más reciente</b>
    /// ("la anterior"). Vacio = usa
    /// <see cref="DetectorIntencionContinuar.FrasesRevisitarAnteriorPorDefecto"/>.
    /// </summary>
    public IList<string> FrasesRevisitarAnterior { get; set; } = new List<string>();

    /// <summary>
    /// I-19 §4.7 — frases con las que pide revisitar <b>alguna</b> idea previa sin señalar cuál; con
    /// varias candidatas el servidor ofrece una lista numerada. Vacio = usa
    /// <see cref="DetectorIntencionContinuar.FrasesRevisitarIdeaPorDefecto"/>.
    /// </summary>
    public IList<string> FrasesRevisitarIdea { get; set; } = new List<string>();

    /// <summary>
    /// P-26 §5.1 paso 3 — frases con las que el participante pide explicitamente cambiar de campania
    /// ("otra campaña"). Suspenden la afinidad vigente sin cerrar la idea y recalculan opciones.
    /// Vacio = usa <see cref="DetectorIntencionContinuar.FrasesCambiarCampaniaPorDefecto"/>.
    /// </summary>
    public IList<string> FrasesCambiarCampania { get; set; } = new List<string>();

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

    /// <summary>I-18: kill-switch global; el campo de campaña sigue naciendo apagado.</summary>
    public bool CoachingSecuencialIdeas { get; set; } = true;

    /// <summary>I-18: default global de la ventana por idea. 0 o negativo la desactiva.</summary>
    public int MinutosCoachingPorIdea { get; set; }

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
    /// I-19: kill-switch global de emergencia. Nace activo para todas las campañas; al apagarlo los
    /// aportes nuevos no vuelven a ser evaluados como respuestas aisladas.
    /// </summary>
    public bool ConsolidacionProgresivaHabilitada { get; set; } = true;

    /// <summary>
    /// I-20 (§5): kill-switch global de la redacción conversacional fluida. Nace activo y **no** tiene
    /// opt-in por campaña. Al apagarlo se usan los respaldos deterministas de cada acto, sin alterar
    /// consolidación, evaluación, cola, estados ni Markdown.
    /// </summary>
    public bool RedaccionConversacionalFluidaHabilitada { get; set; } = true;

    /// <summary>
    /// I-20 (§4.1): largo máximo de cada pieza redactada (puente y pregunta). Una salida más larga se
    /// considera inválida y degrada al respaldo del acto. 0 o negativo se normaliza al default.
    /// </summary>
    public int MaxCaracteresRedaccionTurno { get; set; } = 320;

    /// <summary>I-19: límite de la versión acumulada; evita truncar silenciosamente hechos confirmados.</summary>
    public int MaxCaracteresIdeaConsolidada { get; set; } = 4000;

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

    public const string SaludoReactivacionDefault =
        "¡Hola! Estoy aquí para acompañarte a crear una nueva idea. Cuéntame qué quieres proponer.";

    public const string PausaPorInactividadDefault =
        "Demos una pausa por ahora. Cuando quieras seguimos: escríbeme y retomamos donde quedamos.";

    public const string InvitacionMejoraDefault =
        "Si quieres, puedes enviarme una version mejorada de tu respuesta con base en esta "
        + "retroalimentacion y la tomare en cuenta.";

    public const string MensajeConfiguracionNoDisponibleDefault =
        "Hay un problema con la configuracion de esta campania. Contacta al administrador del sistema.";

    public const string MensajeCalificacionAltaDefault =
        "¡Excelente! Tu respuesta ya esta muy completa, asi que avanzamos.";

    public const string AcuseContinuarDefault =
        "¡Perfecto, sigamos!";

    public const string AcuseRechazoGuardadoDefault =
        "Entendido, no la guardo como definitiva. ¡Gracias por decírmelo!";

    public const string AcuseReaperturaIdeaDefault =
        "Claro, volvamos a esa idea. Así quedó registrada:";

    public const string InvitacionReaperturaIdeaDefault =
        "¿Qué quieres cambiar o agregar?";

    public const string PreguntaSeleccionIdeaDefault =
        "¿Cuál de estas ideas quieres retomar? Respóndeme con el número.";

    public const string InstruccionSeleccionIdeaDefault =
        "Responde con el número o con el resumen exacto de la idea.";

    public const string SinIdeasHistoricasDefault =
        "No encontré ideas anteriores en este tema. Si quieres, escribe una idea nueva.";

    public const string EncabezadoSeleccionCampaniaDefault =
        "¿A cuál campaña corresponde tu aporte?";

    public const string InstruccionSeleccionCampaniaDefault =
        "Responde con el número o con el nombre de la campaña.";

    public const string AyudaSeleccionCampaniaInvalidaDefault =
        "No reconocí esa opción. Tu aporte sigue guardado.";

    public const string EncabezadoSeleccionPreguntaDefault =
        "¿Sobre cuál pregunta quieres aportar?";

    public const string InstruccionSeleccionPreguntaDefault =
        "Responde con el número o con el texto de la pregunta.";

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

    /// <summary>P-28: respaldo si no se puede redactar el saludo de reactivación.</summary>
    public string SaludoReactivacion { get; set; } = SaludoReactivacionDefault;

    /// <summary>
    /// P-29 §5.2: respaldo determinista del mensaje de pausa que acompaña al cierre por inactividad.
    /// Nunca menciona rúbrica ni puntajes y deja la puerta abierta a retomar.
    /// </summary>
    public string PausaPorInactividad { get; set; } = PausaPorInactividadDefault;

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
    /// I-17 — acuse calido que antecede al cierre cuando el participante rechaza que su idea madura se
    /// guarde (se degrada a incubación antes de cerrar).
    /// </summary>
    public string AcuseRechazoGuardado { get; set; } = AcuseRechazoGuardadoDefault;

    /// <summary>
    /// I-19 §4.7 — acuse con el que se retoma una idea cerrada; antecede al texto de la versión
    /// confirmada, que sigue siendo la oficial mientras se prepara la nueva.
    /// </summary>
    public string AcuseReaperturaIdea { get; set; } = AcuseReaperturaIdeaDefault;

    /// <summary>I-19 §4.7 — invitación breve que cierra el mensaje de reapertura.</summary>
    public string InvitacionReaperturaIdea { get; set; } = InvitacionReaperturaIdeaDefault;

    /// <summary>
    /// I-19 §4.7 — encabezado de la lista breve numerada cuando hay varias ideas candidatas a reabrir.
    /// La lista nunca muestra calificaciones.
    /// </summary>
    public string PreguntaSeleccionIdea { get; set; } = PreguntaSeleccionIdeaDefault;

    /// <summary>P-30: instrucción determinista para número o resumen exacto no ambiguo.</summary>
    public string InstruccionSeleccionIdea { get; set; } = InstruccionSeleccionIdeaDefault;

    /// <summary>P-30: respuesta neutral cuando no hay candidatas históricas en el alcance.</summary>
    public string SinIdeasHistoricas { get; set; } = SinIdeasHistoricasDefault;

    /// <summary>P-26 §5.3 — encabezado del menu numerado de campanias elegibles.</summary>
    public string EncabezadoSeleccionCampania { get; set; } = EncabezadoSeleccionCampaniaDefault;

    /// <summary>P-26 §5.3 — instruccion final del menu (numero o nombre de la campania).</summary>
    public string InstruccionSeleccionCampania { get; set; } = InstruccionSeleccionCampaniaDefault;

    /// <summary>P-26 §5.5 — ayuda que antecede al menu cuando la seleccion recibida no es valida.</summary>
    public string AyudaSeleccionCampaniaInvalida { get; set; } = AyudaSeleccionCampaniaInvalidaDefault;

    /// <summary>P-26 §5.4 — encabezado del menu numerado de preguntas elegibles.</summary>
    public string EncabezadoSeleccionPregunta { get; set; } = EncabezadoSeleccionPreguntaDefault;

    /// <summary>P-26 §5.4 — instruccion final del menu de preguntas (numero o texto completo).</summary>
    public string InstruccionSeleccionPregunta { get; set; } = InstruccionSeleccionPreguntaDefault;

    /// <summary>
    /// Variantes del acuse de continuar; se rotan por hilo para no repetir siempre la misma frase.
    /// Vacia = usa <see cref="AcuseContinuar"/>.
    /// </summary>
    public IReadOnlyList<string> AcuseContinuarVariantes { get; set; } = new List<string>();
}
