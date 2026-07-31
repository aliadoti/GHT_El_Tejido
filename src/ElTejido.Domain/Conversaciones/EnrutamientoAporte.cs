using ElTejido.Domain.Common;

namespace ElTejido.Domain.Conversaciones;

/// <summary>
/// P-26 (03 §3.6.1): conserva el aporte recibido antes de que el participante elija campania/pregunta
/// y funciona como afinidad temporal durante el coaching. Vive en el contenedor <c>conversations</c>
/// bajo la particion interna determinista <c>routing:&lt;usuarioId&gt;</c>; no atribuye el aporte a una
/// campania real antes de la seleccion. El vencimiento es logico (24 h) para conservar auditoria; el
/// texto original pertenece al plano de negocio y nunca se copia a telemetria tecnica. Inmutable: las
/// transiciones devuelven una nueva instancia.
/// </summary>
public sealed class EnrutamientoAporte
{
    private const int HorasVigencia = 24;
    private const string PrefijoParticionRouting = "routing:";
    private const string PrefijoId = "route_";

    private EnrutamientoAporte(
        string id,
        string usuarioId,
        string whatsappMessageId,
        string? phoneNumberIdDestino,
        string textoOriginal,
        EstadoEnrutamientoAporte estado,
        IReadOnlyList<OpcionCampaniaOfrecida> campaniasOfrecidas,
        string? campaniaSeleccionadaId,
        IReadOnlyList<OpcionPreguntaOfrecida> preguntasOfrecidas,
        string? preguntaSeleccionadaId,
        string? conversacionId,
        IReadOnlyList<IntentoSeleccion> intentosSeleccion,
        DateTimeOffset creadoEn,
        DateTimeOffset actualizadoEn,
        DateTimeOffset venceEn,
        DateTimeOffset? procesadoEn)
    {
        Id = id;
        UsuarioId = usuarioId;
        WhatsappMessageId = whatsappMessageId;
        PhoneNumberIdDestino = phoneNumberIdDestino;
        TextoOriginal = textoOriginal;
        Estado = estado;
        CampaniasOfrecidas = campaniasOfrecidas;
        CampaniaSeleccionadaId = campaniaSeleccionadaId;
        PreguntasOfrecidas = preguntasOfrecidas;
        PreguntaSeleccionadaId = preguntaSeleccionadaId;
        ConversacionId = conversacionId;
        IntentosSeleccion = intentosSeleccion;
        CreadoEn = creadoEn;
        ActualizadoEn = actualizadoEn;
        VenceEn = venceEn;
        ProcesadoEn = procesadoEn;
    }

    public string Id { get; }

    public string UsuarioId { get; }

    /// <summary>Id del mensaje entrante de Meta; junto al usuario hace determinista el Id (idempotencia ante reintentos).</summary>
    public string WhatsappMessageId { get; }

    /// <summary>P-21: numero de WhatsApp por el que llego el aporte, para responder por el mismo emisor.</summary>
    public string? PhoneNumberIdDestino { get; }

    /// <summary>Aporte original conservado; mismo control de acceso/retencion que Mensaje. Nunca va a logs tecnicos.</summary>
    public string TextoOriginal { get; }

    public EstadoEnrutamientoAporte Estado { get; }

    /// <summary>Snapshot auditable de las opciones ofrecidas; la autorizacion se revalida al seleccionar.</summary>
    public IReadOnlyList<OpcionCampaniaOfrecida> CampaniasOfrecidas { get; }

    public string? CampaniaSeleccionadaId { get; }

    public IReadOnlyList<OpcionPreguntaOfrecida> PreguntasOfrecidas { get; }

    public string? PreguntaSeleccionadaId { get; }

    /// <summary>Conversacion (ciclo) a la que quedo enrutado el aporte cuando el estado es enIdea o posterior.</summary>
    public string? ConversacionId { get; }

    /// <summary>Intentos de seleccion auditables: ids, tipo, resultado y fecha; nunca el texto libre recibido.</summary>
    public IReadOnlyList<IntentoSeleccion> IntentosSeleccion { get; }

    public DateTimeOffset CreadoEn { get; }

    public DateTimeOffset ActualizadoEn { get; }

    /// <summary>Vencimiento logico de la seleccion pendiente; el documento permanece auditable.</summary>
    public DateTimeOffset VenceEn { get; }

    /// <summary>Instante en el que el aporte original quedo persistido en la conversacion resuelta.</summary>
    public DateTimeOffset? ProcesadoEn { get; }

    /// <summary>Particion interna reservada del contenedor <c>conversations</c> para este usuario.</summary>
    public string ParticionRouting => ParticionRoutingDe(UsuarioId);

    public static string ParticionRoutingDe(string usuarioId)
        => PrefijoParticionRouting + DomainGuards.Required(usuarioId, nameof(usuarioId));

    /// <summary>
    /// Id determinista por usuario + mensaje raiz: un reintento de Meta o interno reutiliza el mismo
    /// documento y no puede crear dos ciclos para el mismo <c>whatsappMessageId</c>.
    /// </summary>
    public static string GenerarId(string usuarioId, string whatsappMessageId)
        => PrefijoId
            + DomainGuards.Required(usuarioId, nameof(usuarioId))
            + "_"
            + DomainGuards.Required(whatsappMessageId, nameof(whatsappMessageId));

    public static EnrutamientoAporte Crear(
        string usuarioId,
        string whatsappMessageId,
        string textoOriginal,
        EstadoEnrutamientoAporte estado,
        DateTimeOffset creadoEn,
        string? phoneNumberIdDestino = null,
        IEnumerable<OpcionCampaniaOfrecida>? campaniasOfrecidas = null,
        string? campaniaSeleccionadaId = null,
        IEnumerable<OpcionPreguntaOfrecida>? preguntasOfrecidas = null,
        string? preguntaSeleccionadaId = null,
        string? conversacionId = null,
        IEnumerable<IntentoSeleccion>? intentosSeleccion = null,
        DateTimeOffset? actualizadoEn = null,
        DateTimeOffset? venceEn = null,
        DateTimeOffset? procesadoEn = null)
    {
        var usuario = DomainGuards.Required(usuarioId, nameof(usuarioId));
        var mensaje = DomainGuards.Required(whatsappMessageId, nameof(whatsappMessageId));
        var creado = creadoEn.ToUniversalTime();

        return new EnrutamientoAporte(
            GenerarId(usuario, mensaje),
            usuario,
            mensaje,
            string.IsNullOrWhiteSpace(phoneNumberIdDestino) ? null : phoneNumberIdDestino.Trim(),
            DomainGuards.Required(textoOriginal, nameof(textoOriginal)),
            estado,
            (campaniasOfrecidas ?? Array.Empty<OpcionCampaniaOfrecida>()).ToArray(),
            string.IsNullOrWhiteSpace(campaniaSeleccionadaId) ? null : campaniaSeleccionadaId.Trim(),
            (preguntasOfrecidas ?? Array.Empty<OpcionPreguntaOfrecida>()).ToArray(),
            string.IsNullOrWhiteSpace(preguntaSeleccionadaId) ? null : preguntaSeleccionadaId.Trim(),
            string.IsNullOrWhiteSpace(conversacionId) ? null : conversacionId.Trim(),
            (intentosSeleccion ?? Array.Empty<IntentoSeleccion>()).ToArray(),
            creado,
            (actualizadoEn ?? creado).ToUniversalTime(),
            (venceEn ?? creado.AddHours(HorasVigencia)).ToUniversalTime(),
            procesadoEn?.ToUniversalTime());
    }

    /// <summary>¿La seleccion pendiente ya vencio logicamente? Solo aplica a los estados de seleccion.</summary>
    public bool SeleccionVencida(DateTimeOffset ahora)
        => Estado is EstadoEnrutamientoAporte.SeleccionCampania or EstadoEnrutamientoAporte.SeleccionPregunta
            && ahora.ToUniversalTime() >= VenceEn;

    /// <summary>Reemplaza el snapshot de campanias ofrecidas (oferta inicial o recalculo tras revalidar).</summary>
    public EnrutamientoAporte OfrecerCampanias(IEnumerable<OpcionCampaniaOfrecida> opciones, DateTimeOffset ahora)
        => With(
            estado: EstadoEnrutamientoAporte.SeleccionCampania,
            campaniasOfrecidas: opciones.ToArray(),
            actualizadoEn: ahora);

    /// <summary>Audita un intento de seleccion (solo ids/tipo/resultado/fecha, nunca el texto libre).</summary>
    public EnrutamientoAporte RegistrarIntento(IntentoSeleccion intento, DateTimeOffset ahora)
        => With(
            intentosSeleccion: IntentosSeleccion.Append(intento).ToArray(),
            actualizadoEn: ahora);

    /// <summary>
    /// Fija la campania elegida (ya revalidada por el servidor). En el corte 2 el enrutamiento pasa a
    /// <c>listo</c>; la seleccion de pregunta (corte 3) inserta el estado intermedio.
    /// </summary>
    public EnrutamientoAporte SeleccionarCampania(string campaniaId, DateTimeOffset ahora)
    {
        ExigirEstado(EstadoEnrutamientoAporte.SeleccionCampania, "seleccionar la campania");
        return With(
            estado: EstadoEnrutamientoAporte.Listo,
            campaniaSeleccionadaId: DomainGuards.Required(campaniaId, nameof(campaniaId)),
            actualizadoEn: ahora);
    }

    /// <summary>
    /// El aporte original quedo persistido en la conversacion resuelta: solo una ejecucion puede pasar
    /// de <c>listo</c> a <c>enIdea</c> y fijar <c>procesadoEn</c> (03 §3.6.1).
    /// </summary>
    public EnrutamientoAporte MarcarEnIdea(string? conversacionId, DateTimeOffset procesadoEn)
    {
        ExigirEstado(EstadoEnrutamientoAporte.Listo, "marcar el aporte en idea");
        return With(
            estado: EstadoEnrutamientoAporte.EnIdea,
            conversacionId: conversacionId,
            procesadoEn: procesadoEn,
            actualizadoEn: procesadoEn);
    }

    /// <summary>Vencimiento logico: el texto permanece auditable pero ya no se procesa automaticamente.</summary>
    public EnrutamientoAporte Expirar(DateTimeOffset ahora)
        => With(estado: EstadoEnrutamientoAporte.Expirado, actualizadoEn: ahora);

    /// <summary>El enrutamiento deja de ser procesable (p. ej. ninguna campania siguio elegible); queda auditable.</summary>
    public EnrutamientoAporte Cancelar(DateTimeOffset ahora)
        => With(estado: EstadoEnrutamientoAporte.Cancelado, actualizadoEn: ahora);

    private void ExigirEstado(EstadoEnrutamientoAporte esperado, string accion)
    {
        if (Estado != esperado)
        {
            throw new DomainValidationException(
                "ENRUTAMIENTO_ESTADO_INVALIDO",
                $"No se puede {accion} desde el estado {Estado}.");
        }
    }

    private EnrutamientoAporte With(
        EstadoEnrutamientoAporte? estado = null,
        IReadOnlyList<OpcionCampaniaOfrecida>? campaniasOfrecidas = null,
        string? campaniaSeleccionadaId = null,
        IReadOnlyList<OpcionPreguntaOfrecida>? preguntasOfrecidas = null,
        string? preguntaSeleccionadaId = null,
        string? conversacionId = null,
        IReadOnlyList<IntentoSeleccion>? intentosSeleccion = null,
        DateTimeOffset? actualizadoEn = null,
        DateTimeOffset? procesadoEn = null)
        => new(
            Id,
            UsuarioId,
            WhatsappMessageId,
            PhoneNumberIdDestino,
            TextoOriginal,
            estado ?? Estado,
            campaniasOfrecidas ?? CampaniasOfrecidas,
            campaniaSeleccionadaId ?? CampaniaSeleccionadaId,
            preguntasOfrecidas ?? PreguntasOfrecidas,
            preguntaSeleccionadaId ?? PreguntaSeleccionadaId,
            conversacionId ?? ConversacionId,
            intentosSeleccion ?? IntentosSeleccion,
            CreadoEn,
            (actualizadoEn ?? ActualizadoEn).ToUniversalTime(),
            VenceEn,
            (procesadoEn ?? ProcesadoEn)?.ToUniversalTime());
}

/// <summary>Estados del enrutamiento (03 §3.6.1). El vencimiento y las transiciones son server-side.</summary>
public enum EstadoEnrutamientoAporte
{
    SeleccionCampania,
    SeleccionPregunta,
    Listo,
    EnIdea,
    Completado,
    Expirado,
    Cancelado,
}

/// <summary>Snapshot auditable de una campania ofrecida en la lista numerada.</summary>
public sealed record OpcionCampaniaOfrecida(string CampaniaId, string NombreSnapshot, int Orden);

/// <summary>Snapshot auditable de una pregunta ofrecida en la lista numerada.</summary>
public sealed record OpcionPreguntaOfrecida(string PreguntaId, string TextoSnapshot, int Orden);

/// <summary>Intento de seleccion auditado: solo ids, tipo (campania|pregunta), resultado y fecha.</summary>
public sealed record IntentoSeleccion(
    string WhatsappMessageId,
    TipoIntentoSeleccion Tipo,
    ResultadoIntentoSeleccion Resultado,
    DateTimeOffset Fecha);

public enum TipoIntentoSeleccion
{
    Campania,
    Pregunta,
}

public enum ResultadoIntentoSeleccion
{
    Valido,
    Invalido,
}
