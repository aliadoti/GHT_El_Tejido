namespace ElTejido.Application.WhatsApp;

/// <summary>
/// Casos de uso de envio masivo de mensajes iniciales (04 §5.4, 05 §2.5). La campania DEBE estar
/// <c>activa</c> (409 si no). Encola un trabajo por participante y responde con el job para que el
/// portal consulte el avance.
/// </summary>
public interface IServicioEnvios
{
    /// <summary>Encola el mensaje inicial a los participantes indicados (o a todos los activos si no se indican).</summary>
    Task<ResultadoEncolarEnvio> EncolarInicialesAsync(
        string campaniaId,
        IReadOnlyCollection<string>? usuarioIds,
        string? mensajeInicialId,
        CancellationToken cancellationToken);

    /// <summary>Reenvia a quienes no respondieron (<c>estadoRespuesta=sinRespuesta</c>) (ARQ §4.4).</summary>
    Task<ResultadoEncolarEnvio> ReenviarSinRespuestaAsync(
        string campaniaId,
        string? mensajeInicialId,
        CancellationToken cancellationToken);

    /// <summary>Reintenta los envios en estado <c>error</c>.</summary>
    Task<ResultadoEncolarEnvio> ReintentarErroresAsync(
        string campaniaId,
        string? mensajeInicialId,
        CancellationToken cancellationToken);

    /// <summary>Estado de envio/respuesta por participante (04 §5.4 <c>GET .../envios</c>).</summary>
    Task<IReadOnlyCollection<EstadoEnvioParticipante>> ConsultarEstadoAsync(
        string campaniaId,
        CancellationToken cancellationToken);
}

/// <summary>Respuesta de un disparo de envio (202 Accepted, 04 §5.4).</summary>
public sealed record ResultadoEncolarEnvio(string JobId, int Encolados, string Estado);

/// <summary>Estado de envio/respuesta de un participante (04 §5.4).</summary>
public sealed record EstadoEnvioParticipante(
    string UsuarioId,
    string Numero,
    string EstadoEnvio,
    string EstadoRespuesta,
    string? Error);
