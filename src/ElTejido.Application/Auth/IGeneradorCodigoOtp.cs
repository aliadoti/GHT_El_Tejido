namespace ElTejido.Application.Auth;

/// <summary>
/// Genera codigos OTP numericos con un CSPRNG (06 §4.2b). Aislado en un puerto para inyectar
/// codigos deterministas en pruebas.
/// </summary>
public interface IGeneradorCodigoOtp
{
    /// <summary>Genera un codigo numerico de <paramref name="longitud"/> digitos.</summary>
    string Generar(int longitud);
}
