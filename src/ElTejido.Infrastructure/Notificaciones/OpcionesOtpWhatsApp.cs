namespace ElTejido.Infrastructure.Notificaciones;

/// <summary>
/// Configuracion del envio del OTP de login admin por WhatsApp (seccion <c>Auth:OtpWhatsApp</c>;
/// 06 §4.2e, 05 §2.2). Por defecto esta <b>deshabilitado</b>: sin una plantilla HSM aprobada el
/// login usa el notificador de log (<see cref="NotificadorOtpLog"/>), de modo que dev y la
/// simulacion siguen funcionando sin WhatsApp real. Solo nombres/valores no secretos.
/// </summary>
public sealed class OpcionesOtpWhatsApp
{
    public const string Seccion = "Auth:OtpWhatsApp";

    /// <summary>
    /// Si es <c>true</c> y hay una plantilla configurada, el OTP se envia por WhatsApp con una
    /// plantilla HSM aprobada; si no, se usa el notificador de log.
    /// </summary>
    public bool Habilitado { get; set; }

    /// <summary>Nombre de la plantilla HSM aprobada (categoria autenticacion) para el codigo.</summary>
    public string PlantillaNombre { get; set; } = string.Empty;

    /// <summary>
    /// Idioma de la plantilla; debe coincidir EXACTAMENTE con el de la plantilla aprobada en Meta
    /// (p. ej. <c>es</c>, <c>es_CO</c>, <c>en_US</c>). Un idioma que no coincide da error de Meta.
    /// </summary>
    public string PlantillaIdioma { get; set; } = "es";
}
