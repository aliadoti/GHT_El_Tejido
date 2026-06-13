namespace ElTejido.Application.Auth;

/// <summary>
/// Parametros configurables de autenticacion (seccion <c>Auth</c> de 02 §6; 06 §4.2).
/// Todos tienen defaults seguros para el MVP.
/// </summary>
public sealed class OpcionesAuth
{
    public const string Seccion = "Auth";

    /// <summary>Vigencia del OTP en minutos (06 §4.2 default 5).</summary>
    public int OtpTtlMinutos { get; set; } = 5;

    /// <summary>Cantidad de digitos del OTP (06 §4.2 default 6).</summary>
    public int OtpLongitud { get; set; } = 6;

    /// <summary>Intentos de verificacion permitidos por codigo (06 §4.2 default 5).</summary>
    public int OtpIntentos { get; set; } = 5;

    /// <summary>Maximo de solicitudes de OTP por numero dentro de la ventana (REQ §10.3.7 default 5).</summary>
    public int OtpSolicitudesPorVentana { get; set; } = 5;

    /// <summary>Ventana del limite de solicitudes de OTP, en minutos (default 60).</summary>
    public int OtpVentanaSolicitudesMinutos { get; set; } = 60;

    /// <summary>Vigencia de la sesion admin en minutos (06 §4.3 default 60).</summary>
    public int SesionTtlMinutos { get; set; } = 60;
}
