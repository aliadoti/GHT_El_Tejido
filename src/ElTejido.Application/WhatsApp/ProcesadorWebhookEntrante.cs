using ElTejido.Application.Common;
using ElTejido.Application.Conversacion;
using ElTejido.Application.Identidad;
using ElTejido.Application.Seguridad;
using ElTejido.Domain.Seguridad;

namespace ElTejido.Application.WhatsApp;

/// <summary>Desenlace del procesamiento de un payload entrante, para trazabilidad (05 §2.4).</summary>
public enum ResultadoProcesoEntrante
{
    /// <summary>El payload no contenia un mensaje procesable (p. ej. notificacion de estado).</summary>
    NoMensaje,

    /// <summary>Mensaje repetido por reintento de Meta; descartado por idempotencia (03 §4).</summary>
    Duplicado,

    /// <summary>
    /// El numero excedio el rate por minuto (P-10, 10 §2); descartado silenciosamente antes de
    /// resolver el participante. Ya registrado en LogSeguridad como <c>rate_numero</c>.
    /// </summary>
    RateLimitado,

    /// <summary>Numero no autorizado; rechazo neutral ya registrado por la resolucion (06 §3.3).</summary>
    NoAutorizado,

    /// <summary>Mensaje aceptado y entregado al orquestador conversacional.</summary>
    Procesado,
}

/// <summary>
/// Desenlace del procesamiento mas el motivo de rechazo cuando aplica (<see cref="ResultadoProcesoEntrante.NoAutorizado"/>).
/// El motivo es interno (06 §3.3): se registra/loguea para diagnostico, nunca se revela al usuario.
/// </summary>
public readonly record struct ResultadoEntrante(ResultadoProcesoEntrante Estado, MotivoRechazo? Motivo = null);

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
    private readonly ILimitadorNumeroEntrante _limitadorNumero;
    private readonly IResolutorParticipante _resolutor;
    private readonly IOrquestadorConversacion _orquestador;
    private readonly IRepositorioLogSeguridad _logSeguridad;
    private readonly IProveedorCorrelacion _correlacion;
    private readonly TimeProvider _tiempo;

    public ProcesadorWebhookEntrante(
        IWhatsAppGateway gateway,
        IRegistroWebhookDedupe dedupe,
        ILimitadorNumeroEntrante limitadorNumero,
        IResolutorParticipante resolutor,
        IOrquestadorConversacion orquestador,
        IRepositorioLogSeguridad logSeguridad,
        IProveedorCorrelacion correlacion,
        TimeProvider tiempo)
    {
        _gateway = gateway;
        _dedupe = dedupe;
        _limitadorNumero = limitadorNumero;
        _resolutor = resolutor;
        _orquestador = orquestador;
        _logSeguridad = logSeguridad;
        _correlacion = correlacion;
        _tiempo = tiempo;
    }

    public async Task<ResultadoEntrante> ProcesarAsync(
        WhatsAppWebhookPayload payload,
        CancellationToken cancellationToken)
    {
        var mensaje = _gateway.ParsearWebhook(payload);
        if (mensaje is null)
        {
            return new ResultadoEntrante(ResultadoProcesoEntrante.NoMensaje);
        }

        // Idempotencia por whatsappMessageId: si ya estaba registrado, es un reintento de Meta (03 §4).
        var primeraVez = await _dedupe.IntentarRegistrarMensajeAsync(
            mensaje.WhatsappMessageId,
            _tiempo.GetUtcNow(),
            cancellationToken);
        if (!primeraVez)
        {
            return new ResultadoEntrante(ResultadoProcesoEntrante.Duplicado);
        }

        // Rate por numero (P-10, 10 §2): protege la plataforma de una rafaga dirigida a un numero
        // ANTES de resolver el participante. Al exceder, descarte silencioso + auditoria (sin PII
        // extra: solo el numero, como los demas eventos de LogSeguridad).
        if (!await _limitadorNumero.RegistrarYPermitirAsync(mensaje.NumeroE164, cancellationToken))
        {
            await RegistrarRateNumeroAsync(mensaje.NumeroE164, cancellationToken);
            return new ResultadoEntrante(ResultadoProcesoEntrante.RateLimitado);
        }

        var resolucion = await _resolutor.ResolverAsync(mensaje.NumeroE164, cancellationToken);
        if (resolucion is not ResultadoResolucion.Autorizado autorizado)
        {
            // El rechazo neutral ya quedo registrado en LogSeguridad por la resolucion (06 §3.3);
            // el motivo se devuelve para diagnostico interno (logs), nunca se revela al usuario.
            var motivo = (resolucion as ResultadoResolucion.NoAutorizado)?.Motivo;
            return new ResultadoEntrante(ResultadoProcesoEntrante.NoAutorizado, motivo);
        }

        var participante = autorizado.Participante;

        // Guardrail de entrada: acota la longitud al maximo configurado de la campania (10 §2).
        var maximo = participante.Campania.ConfigSeguridad.MaxCaracteresMensaje;
        var mensajeAcotado = mensaje.Texto.Length > maximo
            ? mensaje with { Texto = mensaje.Texto[..maximo] }
            : mensaje;

        await _orquestador.ProcesarMensajeEntranteAsync(participante, mensajeAcotado, cancellationToken);
        return new ResultadoEntrante(ResultadoProcesoEntrante.Procesado);
    }

    private Task RegistrarRateNumeroAsync(string numero, CancellationToken cancellationToken)
        => _logSeguridad.RegistrarAsync(
            LogSeguridad.Crear(
                "log_" + Guid.NewGuid().ToString("N"),
                TipoEventoSeguridad.RateLimit,
                usuarioId: null,
                numero,
                "rechazado",
                "rate_numero",
                _correlacion.CorrelationIdActual,
                _tiempo.GetUtcNow()),
            cancellationToken);
}
