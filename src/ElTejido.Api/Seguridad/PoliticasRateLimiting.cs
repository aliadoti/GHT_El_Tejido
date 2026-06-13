namespace ElTejido.Api.Seguridad;

/// <summary>
/// Nombres de las politicas de rate limiting (10 §2, §3). Se aplican por endpoint con
/// <c>RequireRateLimiting</c>, nunca global, para no afectar <c>/health</c>.
/// </summary>
public static class PoliticasRateLimiting
{
    /// <summary>Endpoints publicos (login OTP). Pensada para <c>/api/auth/*</c> (Fase 3).</summary>
    public const string Publico = "publico";

    /// <summary>Webhook entrante de WhatsApp (Fase 5).</summary>
    public const string Webhook = "webhook";

    /// <summary>Politica estricta (1/min) usada solo por endpoints de diagnostico en Development.</summary>
    public const string Demo = "demo";
}
