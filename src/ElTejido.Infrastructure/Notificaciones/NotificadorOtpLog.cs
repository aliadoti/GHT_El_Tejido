using ElTejido.Application.Auth;
using ElTejido.Domain.Identidad;
using Microsoft.Extensions.Logging;

namespace ElTejido.Infrastructure.Notificaciones;

/// <summary>
/// Implementacion provisional de <see cref="INotificadorOtp"/> hasta la Fase 5 (Gateway WhatsApp).
/// Registra que se genero un OTP <b>sin</b> el codigo ni el numero (10 §5), para no filtrar
/// secretos ni PII. En Fase 5 se reemplaza por el cliente real de WhatsApp.
/// </summary>
public sealed class NotificadorOtpLog : INotificadorOtp
{
    private readonly ILogger<NotificadorOtpLog> _logger;

    public NotificadorOtpLog(ILogger<NotificadorOtpLog> logger)
    {
        _logger = logger;
    }

    public Task EnviarCodigoAsync(NumeroWhatsApp numero, string codigo, CancellationToken cancellationToken)
    {
        _logger.LogInformation("OTP generado; el envio real por WhatsApp queda pendiente de la Fase 5.");
        return Task.CompletedTask;
    }
}
