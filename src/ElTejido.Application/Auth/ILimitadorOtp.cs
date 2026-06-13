using ElTejido.Domain.Identidad;

namespace ElTejido.Application.Auth;

/// <summary>
/// Limite de solicitudes de OTP por numero/ventana (REQ §10.3.7, 10 §2). Complementa el rate limit
/// HTTP por IP: protege contra abuso dirigido a un numero concreto.
/// </summary>
public interface ILimitadorOtp
{
    /// <summary>
    /// Registra una solicitud para el numero y devuelve <c>true</c> si aun esta dentro del limite,
    /// <c>false</c> si lo excede (en cuyo caso la respuesta sigue siendo neutral, 06 §4.2a).
    /// </summary>
    Task<bool> RegistrarYPermitirAsync(NumeroWhatsApp numero, CancellationToken cancellationToken);
}
