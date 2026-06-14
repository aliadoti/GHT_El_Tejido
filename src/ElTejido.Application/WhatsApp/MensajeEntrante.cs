namespace ElTejido.Application.WhatsApp;

/// <summary>
/// Mensaje entrante de WhatsApp ya parseado y reducido a lo que el dominio necesita (05 §2.1).
/// El numero llega como lo envia Meta (digitos, E.164); la normalizacion canonica la hace la
/// resolucion de participante (06 §2).
/// </summary>
public sealed record MensajeEntrante(
    string NumeroE164,
    string Texto,
    string WhatsappMessageId,
    DateTimeOffset Timestamp);
