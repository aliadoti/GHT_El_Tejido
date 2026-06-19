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
        "Si quieres, puedes enviar una version mejorada de tu respuesta con base en esta "
        + "retroalimentacion y la tomare en cuenta (es tu ultimo ajuste). Si ya estas conforme, "
        + "no necesitas responder.";

    public const string MensajeConfiguracionNoDisponibleDefault =
        "Hay un problema con la configuracion de esta campania. Contacta al administrador del sistema.";

    public string SaludoPrimerContacto { get; set; } = SaludoPrimerContactoDefault;

    public string SaludoSiguientePregunta { get; set; } = SaludoSiguientePreguntaDefault;

    public string InvitacionMejora { get; set; } = InvitacionMejoraDefault;

    public string MensajeConfiguracionNoDisponible { get; set; } = MensajeConfiguracionNoDisponibleDefault;
}
