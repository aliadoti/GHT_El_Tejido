using System.Security.Cryptography;
using System.Text;
using ElTejido.Application.Auth;

namespace ElTejido.Infrastructure.Seguridad;

/// <summary>
/// Genera codigos OTP numericos con <see cref="RandomNumberGenerator"/> (CSPRNG, 06 §4.2b).
/// </summary>
public sealed class GeneradorCodigoOtpCsprng : IGeneradorCodigoOtp
{
    public string Generar(int longitud)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(longitud, 1);

        var builder = new StringBuilder(longitud);
        for (var i = 0; i < longitud; i++)
        {
            builder.Append((char)('0' + RandomNumberGenerator.GetInt32(0, 10)));
        }

        return builder.ToString();
    }
}
