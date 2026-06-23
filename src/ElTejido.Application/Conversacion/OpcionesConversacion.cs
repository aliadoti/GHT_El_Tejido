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
        + "retroalimentacion y la tomare en cuenta. Si ya te sientes conforme, escribeme algo como "
        + "\"asi esta bien\" y seguimos.";

    public const string MensajeConfiguracionNoDisponibleDefault =
        "Hay un problema con la configuracion de esta campania. Contacta al administrador del sistema.";

    public const string MensajeCalificacionAltaDefault =
        "¡Excelente! Tu respuesta ya esta muy completa, asi que avanzamos.";

    public const string AcuseContinuarDefault =
        "¡Perfecto, sigamos!";

    public string SaludoPrimerContacto { get; set; } = SaludoPrimerContactoDefault;

    public string SaludoSiguientePregunta { get; set; } = SaludoSiguientePreguntaDefault;

    public string InvitacionMejora { get; set; } = InvitacionMejoraDefault;

    public string MensajeConfiguracionNoDisponible { get; set; } = MensajeConfiguracionNoDisponibleDefault;

    /// <summary>Felicitacion que antecede al cierre cuando una respuesta supera el umbral de calificacion alta.</summary>
    public string MensajeCalificacionAlta { get; set; } = MensajeCalificacionAltaDefault;

    /// <summary>Acuse calido que antecede al cierre cuando el participante pide continuar a la siguiente pregunta.</summary>
    public string AcuseContinuar { get; set; } = AcuseContinuarDefault;
}
