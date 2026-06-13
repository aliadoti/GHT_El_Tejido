namespace ElTejido.Application.Auth;

/// <summary>
/// Orquesta el login admin por OTP de WhatsApp (06 §4). Las respuestas son neutrales: no revelan
/// si un numero existe ni el motivo del rechazo (REQ §10.3.10).
/// </summary>
public interface IAuthAdminService
{
    /// <summary>
    /// Solicita el envio de un OTP. Desde la perspectiva del cliente siempre "tiene exito":
    /// no lanza por numero inexistente ni por exceso de solicitudes (06 §4.2).
    /// </summary>
    Task SolicitarCodigoAsync(string numeroCrudo, CancellationToken cancellationToken);

    /// <summary>
    /// Verifica el OTP e inicia sesion. Devuelve la sesion emitida si es correcto, o <c>null</c>
    /// si el codigo es invalido/vencido/usado o se agotaron los intentos (06 §4.3).
    /// </summary>
    Task<SesionEmitida?> VerificarCodigoAsync(string numeroCrudo, string codigo, CancellationToken cancellationToken);
}
