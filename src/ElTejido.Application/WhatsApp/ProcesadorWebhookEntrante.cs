using ElTejido.Application.Conversacion;
using ElTejido.Application.Identidad;

namespace ElTejido.Application.WhatsApp;

/// <summary>Desenlace del procesamiento de un payload entrante, para trazabilidad (05 §2.4).</summary>
public enum ResultadoProcesoEntrante
{
    /// <summary>El payload no contenia un mensaje procesable (p. ej. notificacion de estado).</summary>
    NoMensaje,

    /// <summary>Mensaje repetido por reintento de Meta; descartado por idempotencia (03 §4).</summary>
    Duplicado,

    /// <summary>Numero no autorizado; rechazo neutral ya registrado por la resolucion (06 §3.3).</summary>
    NoAutorizado,

    /// <summary>Mensaje aceptado y entregado al orquestador conversacional.</summary>
    Procesado,
}

/// <summary>
/// Procesa un payload entrante del webhook tras el ack 200 (05 §2.4 pasos a-g): parsea, aplica
/// idempotencia, resuelve el participante, aplica el guardrail de longitud (10 §2) y entrega el
/// control al orquestador. Logica pura de aplicacion: el <c>IHostedService</c> que la invoca
/// (Infrastructure) solo aporta la cola, el scope de DI y el logging.
/// </summary>
public sealed class ProcesadorWebhookEntrante
{
    private readonly IWhatsAppGateway _gateway;
    private readonly IRegistroWebhookDedupe _dedupe;
    private readonly IResolutorParticipante _resolutor;
    private readonly IOrquestadorConversacion _orquestador;
    private readonly TimeProvider _tiempo;

    public ProcesadorWebhookEntrante(
        IWhatsAppGateway gateway,
        IRegistroWebhookDedupe dedupe,
        IResolutorParticipante resolutor,
        IOrquestadorConversacion orquestador,
        TimeProvider tiempo)
    {
        _gateway = gateway;
        _dedupe = dedupe;
        _resolutor = resolutor;
        _orquestador = orquestador;
        _tiempo = tiempo;
    }

    public async Task<ResultadoProcesoEntrante> ProcesarAsync(
        WhatsAppWebhookPayload payload,
        CancellationToken cancellationToken)
    {
        var mensaje = _gateway.ParsearWebhook(payload);
        if (mensaje is null)
        {
            return ResultadoProcesoEntrante.NoMensaje;
        }

        // Idempotencia por whatsappMessageId: si ya estaba registrado, es un reintento de Meta (03 §4).
        var primeraVez = await _dedupe.IntentarRegistrarMensajeAsync(
            mensaje.WhatsappMessageId,
            _tiempo.GetUtcNow(),
            cancellationToken);
        if (!primeraVez)
        {
            return ResultadoProcesoEntrante.Duplicado;
        }

        var resolucion = await _resolutor.ResolverAsync(mensaje.NumeroE164, cancellationToken);
        if (resolucion is not ResultadoResolucion.Autorizado autorizado)
        {
            // El rechazo neutral ya quedo registrado en LogSeguridad por la resolucion (06 §3.3).
            return ResultadoProcesoEntrante.NoAutorizado;
        }

        var participante = autorizado.Participante;

        // Guardrail de entrada: acota la longitud al maximo configurado de la campania (10 §2).
        var maximo = participante.Campania.ConfigSeguridad.MaxCaracteresMensaje;
        var mensajeAcotado = mensaje.Texto.Length > maximo
            ? mensaje with { Texto = mensaje.Texto[..maximo] }
            : mensaje;

        await _orquestador.ProcesarMensajeEntranteAsync(participante, mensajeAcotado, cancellationToken);
        return ResultadoProcesoEntrante.Procesado;
    }
}
