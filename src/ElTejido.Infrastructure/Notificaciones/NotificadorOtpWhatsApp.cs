using ElTejido.Application.Auth;
using ElTejido.Application.WhatsApp;
using ElTejido.Domain.Campanas;
using ElTejido.Domain.Identidad;
using ElTejido.Domain.Participantes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ElTejido.Infrastructure.Notificaciones;

/// <summary>
/// Envia el OTP de login admin por WhatsApp (06 §4.2e) a traves del <see cref="IWhatsAppGateway"/>.
/// El login es un mensaje iniciado por el negocio (sin ventana de servicio de 24h), por lo que Meta
/// exige una <b>plantilla HSM aprobada</b> (05 §2.2); por eso se usa <c>EnviarPlantillaAsync</c> y no
/// texto libre. El codigo viaja como variable de la plantilla y <b>nunca</b> se registra (10 §5).
/// Un fallo de envio se registra (sin el codigo) y se traga: asi se preserva la respuesta neutral del
/// login (REQ §10.3.10) — el cliente no debe poder distinguir un admin valido de uno invalido por un
/// error de envio. Operaciones lo diagnostica por logs y <c>/health/ready</c>.
/// </summary>
public sealed class NotificadorOtpWhatsApp : INotificadorOtp
{
    private readonly IWhatsAppGateway _gateway;
    private readonly OpcionesOtpWhatsApp _opciones;
    private readonly ILogger<NotificadorOtpWhatsApp> _logger;

    public NotificadorOtpWhatsApp(
        IWhatsAppGateway gateway,
        IOptions<OpcionesOtpWhatsApp> opciones,
        ILogger<NotificadorOtpWhatsApp> logger)
    {
        _gateway = gateway;
        _opciones = opciones.Value;
        _logger = logger;
    }

    public async Task EnviarCodigoAsync(NumeroWhatsApp numero, string codigo, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_opciones.PlantillaNombre))
        {
            // Defensa adicional: el registro solo elige este notificador con plantilla configurada.
            _logger.LogError("OTP no enviado: falta el nombre de la plantilla de autenticacion (Auth:OtpWhatsApp:PlantillaNombre).");
            return;
        }

        // Plantilla de categoria Authentication (con boton copy-code/one-tap): el codigo se envia en
        // el body y en el boton (lo arma el gateway). Solo se necesita el nombre/idioma de la plantilla.
        var plantilla = PlantillaWhatsApp.Crear(
            _opciones.PlantillaNombre,
            _opciones.PlantillaIdioma,
            componentes: null);

        EnvioResultado resultado;
        try
        {
            resultado = await _gateway.EnviarPlantillaAutenticacionAsync(
                numero.Valor,
                plantilla,
                codigo,
                TipoEnvioMensaje.Autenticacion,
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Nunca se incluye el codigo en el log. Se traga para no romper la respuesta neutral.
            _logger.LogError(ex, "Fallo inesperado enviando el OTP por WhatsApp (plantilla '{Plantilla}').", _opciones.PlantillaNombre);
            return;
        }

        if (!resultado.Exito)
        {
            _logger.LogError(
                "No se pudo enviar el OTP por WhatsApp (plantilla '{Plantilla}'): {Error}",
                _opciones.PlantillaNombre,
                resultado.Error);
        }
    }
}
