using ElTejido.Domain.Common;

namespace ElTejido.Domain.Conversaciones;

/// <summary>
/// Hilo conversacional de un (usuario, campania, pregunta, ciclo) (contenedor <c>conversations</c>,
/// 03 §3.6, REQ §29.11). Gobierna la maquina de repregunta unica (05 §4.2). Inmutable: las
/// transiciones devuelven una nueva instancia. P-26: <c>CicloParticipacion</c> permite mas de una
/// conversacion para la misma combinacion usuario/campania/pregunta sin mezclar ideas; documento
/// historico sin el campo equivale al ciclo 1.
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
        DateTimeOffset? fechaCierre,
        CoachingIdeas? coachingIdeas,
        int cicloParticipacion,
        string? origenAporteMessageId,
        string? enrutamientoAporteId,
        IntencionControlPendiente? intencionControlPendiente,
        string idioma)
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
        CoachingIdeas = coachingIdeas;
        CicloParticipacion = cicloParticipacion;
        OrigenAporteMessageId = origenAporteMessageId;
        EnrutamientoAporteId = enrutamientoAporteId;
        IntencionControlPendiente = intencionControlPendiente;
        Idioma = idioma;
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

    /// <summary>I-18: cola opcional; ausente conserva la maquina legacy.</summary>
    public CoachingIdeas? CoachingIdeas { get; }

    /// <summary>P-26: numero de ciclo (>= 1) de esta combinacion usuario/campania/pregunta.</summary>
    public int CicloParticipacion { get; }

    /// <summary>P-26: id del mensaje raiz de WhatsApp que abrio el ciclo; hace idempotente el ciclo posterior.</summary>
    public string? OrigenAporteMessageId { get; }

    /// <summary>P-26: id del EnrutamientoAporte que resolvio la seleccion de campania/pregunta, para auditoria.</summary>
    public string? EnrutamientoAporteId { get; }

    /// <summary>P-27: aclaración pendiente de salida; ausente conserva el flujo histórico.</summary>
    public IntencionControlPendiente? IntencionControlPendiente { get; }

    public string Idioma { get; }

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
        DateTimeOffset? fechaCierre,
        CoachingIdeas? coachingIdeas = null,
        int cicloParticipacion = 1,
        string? origenAporteMessageId = null,
        string? enrutamientoAporteId = null,
        IntencionControlPendiente? intencionControlPendiente = null,
        string idioma = "es")
    {
        if (repreguntasUsadas < 0)
        {
            throw new DomainValidationException(
                "REPREGUNTAS_USADAS_INVALIDAS",
                "Las repreguntas usadas no pueden ser negativas.");
        }

        if (cicloParticipacion < 1)
        {
            throw new DomainValidationException(
                "CICLO_PARTICIPACION_INVALIDO",
                "El ciclo de participacion debe ser mayor o igual a 1.");
        }

        if (intencionControlPendiente is not null
            && estadoMaquina != EstadoMaquinaConversacion.EsperandoConfirmacionSalida)
        {
            throw new DomainValidationException(
                "INTENCION_CONTROL_PENDIENTE_INVALIDA",
                "La aclaración de salida solo puede existir mientras se espera su confirmación.");
        }

        var idiomaNormalizado = NormalizarIdioma(idioma);

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
            fechaCierre?.ToUniversalTime(),
            coachingIdeas,
            cicloParticipacion,
            string.IsNullOrWhiteSpace(origenAporteMessageId) ? null : origenAporteMessageId.Trim(),
            string.IsNullOrWhiteSpace(enrutamientoAporteId) ? null : enrutamientoAporteId.Trim(),
            intencionControlPendiente,
            idiomaNormalizado);
    }

    /// <summary>Inicia un hilo nuevo (esperando la respuesta inicial), con la ventana abierta desde <paramref name="ahora"/>.</summary>
    public static Conversacion Iniciar(
        string id,
        string campaniaId,
        string usuarioId,
        string preguntaId,
        string canal,
        string? correlationId,
        DateTimeOffset ahora,
        int cicloParticipacion = 1,
        string? origenAporteMessageId = null,
        string? enrutamientoAporteId = null,
        string idioma = "es")
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
            fechaCierre: null,
            coachingIdeas: null,
            cicloParticipacion: cicloParticipacion,
            origenAporteMessageId: origenAporteMessageId,
            enrutamientoAporteId: enrutamientoAporteId,
            intencionControlPendiente: null,
            idioma: idioma);

    /// <summary>Renueva la ventana de servicio desde el ultimo mensaje entrante (05 §2.2).</summary>
    public Conversacion RegistrarEntrante(DateTimeOffset timestampEntrante)
        => With(ventana: timestampEntrante.ToUniversalTime().AddHours(HorasVentanaServicio));

    public Conversacion AvanzarA(EstadoMaquinaConversacion estadoMaquina)
        => With(estadoMaquina: estadoMaquina);

    public Conversacion ConCoachingIdeas(CoachingIdeas coachingIdeas)
        => With(coachingIdeas: coachingIdeas, reemplazarCoaching: true);

    public Conversacion ConIntencionControlPendiente(IntencionControlPendiente pendiente)
        => With(intencionControlPendiente: pendiente, reemplazarIntencionControlPendiente: true);

    /// <summary>Cuenta una repregunta enviada y pasa a esperar la respuesta del usuario.</summary>
    public Conversacion RegistrarRepregunta()
        => With(estadoMaquina: EstadoMaquinaConversacion.EsperandoRepregunta, repreguntas: RepreguntasUsadas + 1);

    public Conversacion Cerrar(DateTimeOffset fechaCierre)
        => With(
            estado: EstadoConversacion.Cerrada,
            estadoMaquina: EstadoMaquinaConversacion.Cerrada,
            fechaCierre: fechaCierre.ToUniversalTime());

    /// <summary>
    /// P-26 §5.8: reabre este hilo para atender una petición explícita de complementar/revisitar una
    /// idea que vive en él. Es lo que permite conservar el <c>ideaId</c> (I-19 §4.7) en vez de abrir
    /// un ciclo nuevo; un aporte normal posterior sí crea otro ciclo y deja este hilo intacto. Vuelve
    /// a <c>esperandoRepregunta</c>, limpia la fecha de cierre y renueva la ventana de servicio.
    /// </summary>
    public Conversacion Reabrir(DateTimeOffset ahora)
        => new(
            Id,
            CampaniaId,
            UsuarioId,
            PreguntaId,
            Canal,
            EstadoConversacion.Abierta,
            EstadoMaquinaConversacion.EsperandoRepregunta,
            RepreguntasUsadas,
            ahora.ToUniversalTime().AddHours(HorasVentanaServicio),
            CorrelationId,
            FechaInicio,
            fechaCierre: null,
            CoachingIdeas,
            CicloParticipacion,
            OrigenAporteMessageId,
            EnrutamientoAporteId,
            intencionControlPendiente: null,
            idioma: Idioma);

    private Conversacion With(
        EstadoConversacion? estado = null,
        EstadoMaquinaConversacion? estadoMaquina = null,
        int? repreguntas = null,
        DateTimeOffset? ventana = null,
        DateTimeOffset? fechaCierre = null,
        CoachingIdeas? coachingIdeas = null,
        bool reemplazarCoaching = false,
        IntencionControlPendiente? intencionControlPendiente = null,
        bool reemplazarIntencionControlPendiente = false)
    {
        var maquinaDestino = estadoMaquina ?? EstadoMaquina;
        var pendienteDestino = reemplazarIntencionControlPendiente
            ? intencionControlPendiente
            : maquinaDestino == EstadoMaquinaConversacion.EsperandoConfirmacionSalida
                ? IntencionControlPendiente
                : null;

        return new Conversacion(
            Id,
            CampaniaId,
            UsuarioId,
            PreguntaId,
            Canal,
            estado ?? Estado,
            maquinaDestino,
            repreguntas ?? RepreguntasUsadas,
            ventana ?? VentanaServicioVenceEn,
            CorrelationId,
            FechaInicio,
            fechaCierre ?? FechaCierre,
            reemplazarCoaching ? coachingIdeas : CoachingIdeas,
            CicloParticipacion,
            OrigenAporteMessageId,
            EnrutamientoAporteId,
            pendienteDestino,
            Idioma);
    }

    private static string NormalizarIdioma(string? idioma)
    {
        var normalizado = string.IsNullOrWhiteSpace(idioma) ? "es" : idioma.Trim().ToLowerInvariant();
        if (normalizado is not ("es" or "en"))
        {
            throw new DomainValidationException(
                "IDIOMA_CONVERSACION_INVALIDO",
                "El idioma de la conversacion debe ser 'es' o 'en'.");
        }

        return normalizado;
    }
}
