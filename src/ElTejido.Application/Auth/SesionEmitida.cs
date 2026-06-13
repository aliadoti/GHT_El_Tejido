namespace ElTejido.Application.Auth;

/// <summary>
/// Sesion emitida tras un login OTP correcto (06 §4.3b). El <see cref="Token"/> viaja en la cookie
/// de sesion (<c>httpOnly/Secure/SameSite=Strict</c>); el <see cref="CsrfToken"/> y el
/// <see cref="Usuario"/> se devuelven en el cuerpo (04 §4.2).
/// </summary>
public sealed record SesionEmitida(
    string Token,
    string CsrfToken,
    DateTimeOffset ExpiraEn,
    UsuarioSesion Usuario);
