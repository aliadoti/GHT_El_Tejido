using ElTejido.Domain.Common;

namespace ElTejido.Domain.Conversaciones;

/// <summary>
/// Hilo conversacional de un (usuario, campania, pregunta) (contenedor <c>conversations</c>,
/// 03 §3.6, REQ §29.11). Gobierna la maquina de repregunta unica (05 §4.2). Inmutable: las
/// transiciones devuelven una nueva instancia.
/// </summary>
public sealed class Conversacion
{
    private const int HorasVentanaServicio = 24;

    private Conversacion(
        string id,
        string campaniaId,
        string usuarioId,
        string preguntaId,
        string canal,
        EstadoConversacion estado,
        EstadoMaquinaConversacion estadoMaquina,
        int repreguntasUsadas,
        DateTimeOffset ventanaServicioVenceEn,
        string? correlationId,
        DateTimeOffset fechaInicio,
        DateTimeOffset? fechaCierre)
    {
        Id = id;
        CampaniaId = campaniaId;
        UsuarioId = usuarioId;
        PreguntaId = preguntaId;
        Canal = canal;
        Estado = estado;
        EstadoMaquina = estadoMaquina;
        RepreguntasUsadas = repreguntasUsadas;
        VentanaServicioVenceEn = ventanaServicioVenceEn;
        CorrelationId = correlationId;
        FechaInicio = fechaInicio;
        FechaCierre = fechaCierre;
    }

    public string Id { get; }

    public string CampaniaId { get; }

    public string UsuarioId { get; }

    public string PreguntaId { get; }

    public string Canal { get; }

    public EstadoConversacion Estado { get; }

    public EstadoMaquinaConversacion EstadoMaquina { get; }

    public int RepreguntasUsadas { get; }

    public DateTimeOffset VentanaServicioVenceEn { get; }

    public string? CorrelationId { get; }

    public DateTimeOffset FechaInicio { get; }

    public DateTimeOffset? FechaCierre { get; }

    /// <summary>¿La ventana de servicio de 24h sigue abierta? Decide texto libre vs plantilla (05 §2.2).</summary>
    public bool VentanaAbierta(DateTimeOffset ahora) => ahora < VentanaServicioVenceEn;

    public static Conversacion Crear(
        string id,
        string campaniaId,
        string usuarioId,
        string preguntaId,
        string canal,
        EstadoConversacion estado,
        EstadoMaquinaConversacion estadoMaquina,
        int repreguntasUsadas,
        DateTimeOffset ventanaServicioVenceEn,
        string? correlationId,
        DateTimeOffset fechaInicio,
        DateTimeOffset? fechaCierre)
    {
        if (repreguntasUsadas < 0)
        {
            throw new DomainValidationException(
                "REPREGUNTAS_USADAS_INVALIDAS",
                "Las repreguntas usadas no pueden ser negativas.");
        }

        return new Conversacion(
            DomainGuards.Required(id, nameof(id)),
            DomainGuards.Required(campaniaId, nameof(campaniaId)),
            DomainGuards.Required(usuarioId, nameof(usuarioId)),
            DomainGuards.Required(preguntaId, nameof(preguntaId)),
            DomainGuards.Required(canal, nameof(canal)),
            estado,
            estadoMaquina,
            repreguntasUsadas,
            ventanaServicioVenceEn.ToUniversalTime(),
            string.IsNullOrWhiteSpace(correlationId) ? null : correlationId.Trim(),
            fechaInicio.ToUniversalTime(),
            fechaCierre?.ToUniversalTime());
    }

    /// <summary>Inicia un hilo nuevo (esperando la respuesta inicial), con la ventana abierta desde <paramref name="ahora"/>.</summary>
    public static Conversacion Iniciar(
        string id,
        string campaniaId,
        string usuarioId,
        string preguntaId,
        string canal,
        string? correlationId,
        DateTimeOffset ahora)
        => Crear(
            id,
            campaniaId,
            usuarioId,
            preguntaId,
            canal,
            EstadoConversacion.Abierta,
            EstadoMaquinaConversacion.EsperandoRespuestaInicial,
            repreguntasUsadas: 0,
            ahora.AddHours(HorasVentanaServicio),
            correlationId,
            ahora,
            fechaCierre: null);

    /// <summary>Renueva la ventana de servicio desde el ultimo mensaje entrante (05 §2.2).</summary>
    public Conversacion RegistrarEntrante(DateTimeOffset timestampEntrante)
        => With(ventana: timestampEntrante.ToUniversalTime().AddHours(HorasVentanaServicio));

    public Conversacion AvanzarA(EstadoMaquinaConversacion estadoMaquina)
        => With(estadoMaquina: estadoMaquina);

    /// <summary>Cuenta una repregunta enviada y pasa a esperar la respuesta del usuario.</summary>
    public Conversacion RegistrarRepregunta()
        => With(estadoMaquina: EstadoMaquinaConversacion.EsperandoRepregunta, repreguntas: RepreguntasUsadas + 1);

    public Conversacion Cerrar(DateTimeOffset fechaCierre)
        => With(
            estado: EstadoConversacion.Cerrada,
            estadoMaquina: EstadoMaquinaConversacion.Cerrada,
            fechaCierre: fechaCierre.ToUniversalTime());

    private Conversacion With(
        EstadoConversacion? estado = null,
        EstadoMaquinaConversacion? estadoMaquina = null,
        int? repreguntas = null,
        DateTimeOffset? ventana = null,
        DateTimeOffset? fechaCierre = null)
        => new(
            Id,
            CampaniaId,
            UsuarioId,
            PreguntaId,
            Canal,
            estado ?? Estado,
            estadoMaquina ?? EstadoMaquina,
            repreguntas ?? RepreguntasUsadas,
            ventana ?? VentanaServicioVenceEn,
            CorrelationId,
            FechaInicio,
            fechaCierre ?? FechaCierre);
}
