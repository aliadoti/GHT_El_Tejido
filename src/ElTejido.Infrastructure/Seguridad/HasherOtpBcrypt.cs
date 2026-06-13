using ElTejido.Application.Auth;

namespace ElTejido.Infrastructure.Seguridad;

/// <summary>
/// Hashing del OTP con bcrypt (BCrypt.Net-Next) usando <c>otp-salt</c> como pepper (10 §5,
/// SUPUESTOS.md#fase3-otp-bcrypt-jwt). bcrypt genera su propia sal por hash; el pepper se concatena
/// al codigo antes de hashear. Nota: bcrypt trunca la entrada a 72 bytes, suficiente para un codigo
/// corto mas el pepper.
/// </summary>
public sealed class HasherOtpBcrypt : IHasherOtp
{
    public string Hashear(string codigo, string pepper)
    {
        ArgumentException.ThrowIfNullOrEmpty(codigo);
        return BCrypt.Net.BCrypt.HashPassword(Combinar(codigo, pepper));
    }

    public bool Verificar(string codigo, string pepper, string hash)
    {
        if (string.IsNullOrEmpty(codigo) || string.IsNullOrEmpty(hash))
        {
            return false;
        }

        try
        {
            return BCrypt.Net.BCrypt.Verify(Combinar(codigo, pepper), hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            return false;
        }
    }

    private static string Combinar(string codigo, string pepper) => codigo + pepper;
}
