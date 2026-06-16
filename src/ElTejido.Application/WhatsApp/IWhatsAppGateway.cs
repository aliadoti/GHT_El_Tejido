using ElTejido.Domain.Campanas;
using ElTejido.Domain.Participantes;

namespace ElTejido.Application.WhatsApp;

/// <summary>
/// Puerto del WhatsApp Gateway (05 §2.1): encapsula toda interaccion con WhatsApp Cloud API
/// (envio plantilla vs texto libre, parseo del webhook y verificacion de firma). La decision
/// plantilla-vs-texto la toma el llamador segun la ventana de servicio (05 §2.2); este puerto
/// solo ejecuta el envio solicitado. Implementa REQ §9, §15, §21, §26 y ARQ §4.
/// </summary>
public interface IWhatsAppGateway
{
    /// <summary>
    /// Envia una plantilla HSM aprobada (necesaria para iniciar conversacion o fuera de la
    /// ventana de 24h, 05 §2.2). <paramref name="variables"/> mapea cada componente declarado en
    /// la plantilla a su valor.
    /// </summary>
    Task<EnvioResultado> EnviarPlantillaAsync(
        string numeroE164,
        PlantillaWhatsApp plantilla,
        IReadOnlyDictionary<string, string> variables,
        TipoEnvioMensaje tipo,
        CancellationToken cancellationToken);

    /// <summary>
    /// Envia una plantilla de <b>autenticacion</b> de Meta (categoria Authentication) con boton de
    /// codigo (copy-code / one-tap autofill). Meta exige que el codigo viaje tanto en el componente
    /// <c>body</c> como en el componente <c>button</c> (sub_type <c>url</c>, index <c>0</c>); este
    /// metodo arma ambos a partir del mismo <paramref name="codigo"/>. Necesaria para el OTP de login
    /// (06 §4.2e): es un mensaje iniciado por el negocio, sin ventana de 24h (05 §2.2).
    /// </summary>
    Task<EnvioResultado> EnviarPlantillaAutenticacionAsync(
        string numeroE164,
        PlantillaWhatsApp plantilla,
        string codigo,
        TipoEnvioMensaje tipo,
        CancellationToken cancellationToken);

    /// <summary>Envia texto libre (solo permitido dentro de la ventana de servicio de 24h, 05 §2.2).</summary>
    Task<EnvioResultado> EnviarTextoAsync(
        string numeroE164,
        string texto,
        TipoEnvioMensaje tipo,
        CancellationToken cancellationToken);

    /// <summary>Parsea el payload de Meta a un <see cref="MensajeEntrante"/>; <c>null</c> si no es un mensaje procesable.</summary>
    MensajeEntrante? ParsearWebhook(WhatsAppWebhookPayload payload);

    /// <summary>
    /// Verifica la firma <c>X-Hub-Signature-256</c> (HMAC-SHA256 con el app secret, 04 §6.2, 10 §3).
    /// Comparacion en tiempo constante. <c>false</c> obliga a descartar el webhook con 401.
    /// </summary>
    bool VerificarFirma(ReadOnlySpan<byte> cuerpoCrudo, string? firmaHeader, string appSecret);
}
