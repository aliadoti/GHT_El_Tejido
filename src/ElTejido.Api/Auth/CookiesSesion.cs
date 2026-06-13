namespace ElTejido.Api.Auth;

/// <summary>
/// Constantes de la cookie de sesion admin (04 §1, 06 §4.4): <c>httpOnly</c>, <c>Secure</c>
/// (fuera de Development), <c>SameSite=Strict</c>.
/// </summary>
internal static class CookiesSesion
{
    public const string Nombre = "eltejido_sesion";
}
