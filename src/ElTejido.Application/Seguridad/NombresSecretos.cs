namespace ElTejido.Application.Seguridad;

/// <summary>
/// Nombres canonicos de los secretos en Key Vault (10 §4). Deben coincidir exactamente con la
/// guia de Azure; el codigo solo los consume, nunca crea recursos ni almacena sus valores.
/// </summary>
public static class NombresSecretos
{
    /// <summary>API key del proveedor LLM (<c>llm-key</c>).</summary>
    public const string LlmKey = "llm-key";

    /// <summary>Token de acceso de WhatsApp Cloud API (<c>wa-token</c>).</summary>
    public const string WaToken = "wa-token";

    /// <summary>App secret de WhatsApp para verificar la firma del webhook (<c>wa-appsec</c>).</summary>
    public const string WaAppSecret = "wa-appsec";

    /// <summary>Token de verificacion del webhook de Meta (<c>wa-verify-token</c>).</summary>
    public const string WaVerifyToken = "wa-verify-token";

    /// <summary>Secreto de firma de la sesion admin / JWT (<c>jwt-sign</c>).</summary>
    public const string JwtSign = "jwt-sign";

    /// <summary>Sal (pepper) de hashing del OTP (<c>otp-salt</c>).</summary>
    public const string OtpSalt = "otp-salt";
}
