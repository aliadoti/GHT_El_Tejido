namespace ElTejido.Application.Auth;

/// <summary>
/// Hashing del OTP con sal y pepper (10 §5, REQ §10.3.8). El codigo nunca se guarda ni se compara
/// en claro. El <paramref name="pepper"/> es un secreto global (<c>otp-salt</c> de Key Vault).
/// </summary>
public interface IHasherOtp
{
    /// <summary>Devuelve el hash del codigo combinado con el pepper.</summary>
    string Hashear(string codigo, string pepper);

    /// <summary>Verifica, en tiempo constante segun el algoritmo, el codigo contra el hash.</summary>
    bool Verificar(string codigo, string pepper, string hash);
}
