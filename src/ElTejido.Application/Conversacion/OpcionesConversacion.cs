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
}
