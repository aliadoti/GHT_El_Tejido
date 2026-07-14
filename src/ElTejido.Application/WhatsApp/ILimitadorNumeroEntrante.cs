namespace ElTejido.Application.WhatsApp;

/// <summary>
/// P-10 — límite de mensajes entrantes por número de WhatsApp por minuto (10 §2). Complementa el
/// rate limit HTTP por IP y los cupos por usuario/campaña: protege la plataforma contra una ráfaga
/// dirigida a un número concreto <b>antes</b> de resolver el participante (patrón
/// <c>ILimitadorOtp</c>). Con el límite en 0/negativo queda deshabilitado (permite todo).
/// </summary>
public interface ILimitadorNumeroEntrante
{
    /// <summary>
    /// Registra un mensaje entrante para el número y devuelve <c>true</c> si aún está dentro del
    /// límite, <c>false</c> si lo excede (en cuyo caso el mensaje se descarta silenciosamente).
    /// </summary>
    Task<bool> RegistrarYPermitirAsync(string numero, CancellationToken cancellationToken);
}
