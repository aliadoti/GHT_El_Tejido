using ElTejido.Domain.Identidad;

namespace ElTejido.Application.Auth;

/// <summary>
/// Envia el codigo OTP por WhatsApp (06 §4.2e). Es el punto de union con el Gateway (05); su
/// implementacion real llega en la Fase 5. La implementacion nunca debe registrar el codigo.
/// </summary>
public interface INotificadorOtp
{
    Task EnviarCodigoAsync(NumeroWhatsApp numero, string codigo, CancellationToken cancellationToken);
}
