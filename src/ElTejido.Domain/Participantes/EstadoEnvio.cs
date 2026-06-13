namespace ElTejido.Domain.Participantes;

/// <summary>
/// Estado de envio de un mensaje saliente o de un participante de campania.
/// Cubre 03 secciones 3.4 y 3.5.
/// </summary>
public enum EstadoEnvio
{
    Pendiente,
    Enviado,
    Error,
}
