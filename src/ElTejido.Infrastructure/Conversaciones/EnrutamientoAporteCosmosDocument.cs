using ElTejido.Domain.Conversaciones;
using Newtonsoft.Json;

namespace ElTejido.Infrastructure.Conversaciones;

/// <summary>
/// Documento Cosmos del tipo P-26 <c>EnrutamientoAporte</c> (03 §3.6.1) en el contenedor existente
/// <c>conversations</c>. El campo <c>campaniaId</c> guarda la particion interna reservada
/// <c>routing:&lt;usuarioId&gt;</c>, nunca una campania real; las consultas normales de conversaciones
/// filtran por <c>type</c> y no ven estos documentos.
/// </summary>
internal sealed class EnrutamientoAporteCosmosDocument
{
    public const string DocumentType = "EnrutamientoAporte";

    [JsonProperty("id")]
    public string Id { get; init; } = string.Empty;

    [JsonProperty("type")]
    public string Type { get; init; } = DocumentType;

    [JsonProperty("campaniaId")]
    public string CampaniaId { get; init; } = string.Empty;

    [JsonProperty("usuarioId")]
    public string UsuarioId { get; init; } = string.Empty;

    [JsonProperty("idioma", NullValueHandling = NullValueHandling.Ignore)]
    public string? Idioma { get; init; }

    [JsonProperty("whatsappMessageId")]
    public string WhatsappMessageId { get; init; } = string.Empty;

    [JsonProperty("phoneNumberIdDestino", NullValueHandling = NullValueHandling.Ignore)]
    public string? PhoneNumberIdDestino { get; init; }

    [JsonProperty("textoOriginal")]
    public string TextoOriginal { get; init; } = string.Empty;

    [JsonProperty("estado")]
    public string Estado { get; init; } = "seleccionCampania";

    [JsonProperty("campaniasOfrecidas")]
    public IReadOnlyList<OpcionCampaniaDocument> CampaniasOfrecidas { get; init; } = Array.Empty<OpcionCampaniaDocument>();

    [JsonProperty("campaniaSeleccionadaId")]
    public string? CampaniaSeleccionadaId { get; init; }

    [JsonProperty("preguntasOfrecidas")]
    public IReadOnlyList<OpcionPreguntaDocument> PreguntasOfrecidas { get; init; } = Array.Empty<OpcionPreguntaDocument>();

    [JsonProperty("preguntaSeleccionadaId")]
    public string? PreguntaSeleccionadaId { get; init; }

    [JsonProperty("modo", NullValueHandling = NullValueHandling.Ignore)]
    public string? Modo { get; init; }

    [JsonProperty("ideasOfrecidas")]
    public IReadOnlyList<OpcionIdeaDocument> IdeasOfrecidas { get; init; } = Array.Empty<OpcionIdeaDocument>();

    [JsonProperty("ideaSeleccionadaId", NullValueHandling = NullValueHandling.Ignore)]
    public string? IdeaSeleccionadaId { get; init; }

    [JsonProperty("conversacionId")]
    public string? ConversacionId { get; init; }

    [JsonProperty("intentosSeleccion")]
    public IReadOnlyList<IntentoSeleccionDocument> IntentosSeleccion { get; init; } = Array.Empty<IntentoSeleccionDocument>();

    [JsonProperty("creadoEn")]
    public DateTimeOffset CreadoEn { get; init; }

    [JsonProperty("actualizadoEn")]
    public DateTimeOffset ActualizadoEn { get; init; }

    [JsonProperty("venceEn")]
    public DateTimeOffset VenceEn { get; init; }

    [JsonProperty("procesadoEn")]
    public DateTimeOffset? ProcesadoEn { get; init; }

    [JsonProperty("esEntradaProactiva")]
    public bool EsEntradaProactiva { get; init; }

    public static EnrutamientoAporteCosmosDocument FromDomain(EnrutamientoAporte enrutamiento)
        => new()
        {
            Id = enrutamiento.Id,
            Type = DocumentType,
            CampaniaId = enrutamiento.ParticionRouting,
            UsuarioId = enrutamiento.UsuarioId,
            Idioma = enrutamiento.Idioma,
            WhatsappMessageId = enrutamiento.WhatsappMessageId,
            PhoneNumberIdDestino = enrutamiento.PhoneNumberIdDestino,
            TextoOriginal = enrutamiento.TextoOriginal,
            Estado = MapearEstado(enrutamiento.Estado),
            CampaniasOfrecidas = enrutamiento.CampaniasOfrecidas
                .Select(o => new OpcionCampaniaDocument { CampaniaId = o.CampaniaId, NombreSnapshot = o.NombreSnapshot, Orden = o.Orden })
                .ToArray(),
            CampaniaSeleccionadaId = enrutamiento.CampaniaSeleccionadaId,
            PreguntasOfrecidas = enrutamiento.PreguntasOfrecidas
                .Select(o => new OpcionPreguntaDocument { PreguntaId = o.PreguntaId, TextoSnapshot = o.TextoSnapshot, Orden = o.Orden })
                .ToArray(),
            PreguntaSeleccionadaId = enrutamiento.PreguntaSeleccionadaId,
            Modo = MapearModo(enrutamiento.Modo),
            IdeasOfrecidas = enrutamiento.IdeasOfrecidas
                .Select(o => new OpcionIdeaDocument
                {
                    IdeaId = o.IdeaId,
                    ConversacionId = o.ConversacionId,
                    ResumenSnapshot = o.ResumenSnapshot,
                    EstadoSnapshot = o.EstadoSnapshot,
                    Orden = o.Orden,
                })
                .ToArray(),
            IdeaSeleccionadaId = enrutamiento.IdeaSeleccionadaId,
            ConversacionId = enrutamiento.ConversacionId,
            IntentosSeleccion = enrutamiento.IntentosSeleccion
                .Select(i => new IntentoSeleccionDocument
                {
                    WhatsappMessageId = i.WhatsappMessageId,
                    Tipo = i.Tipo switch
                    {
                        TipoIntentoSeleccion.Pregunta => "pregunta",
                        TipoIntentoSeleccion.Idea => "idea",
                        _ => "campania",
                    },
                    Resultado = i.Resultado == ResultadoIntentoSeleccion.Valido ? "valido" : "invalido",
                    Fecha = i.Fecha,
                })
                .ToArray(),
            CreadoEn = enrutamiento.CreadoEn,
            ActualizadoEn = enrutamiento.ActualizadoEn,
            VenceEn = enrutamiento.VenceEn,
            ProcesadoEn = enrutamiento.ProcesadoEn,
            EsEntradaProactiva = enrutamiento.EsEntradaProactiva,
        };

    public EnrutamientoAporte ToDomain()
        => EnrutamientoAporte.Crear(
            UsuarioId,
            WhatsappMessageId,
            TextoOriginal,
            MapearEstado(Estado),
            CreadoEn,
            PhoneNumberIdDestino,
            CampaniasOfrecidas.Select(o => new OpcionCampaniaOfrecida(o.CampaniaId, o.NombreSnapshot, o.Orden)),
            CampaniaSeleccionadaId,
            PreguntasOfrecidas.Select(o => new OpcionPreguntaOfrecida(o.PreguntaId, o.TextoSnapshot, o.Orden)),
            PreguntaSeleccionadaId,
            ConversacionId,
            IntentosSeleccion.Select(i => new IntentoSeleccion(
                i.WhatsappMessageId,
                i.Tipo switch
                {
                    "pregunta" => TipoIntentoSeleccion.Pregunta,
                    "idea" => TipoIntentoSeleccion.Idea,
                    _ => TipoIntentoSeleccion.Campania,
                },
                i.Resultado == "valido" ? ResultadoIntentoSeleccion.Valido : ResultadoIntentoSeleccion.Invalido,
                i.Fecha)),
            ActualizadoEn,
            VenceEn,
            ProcesadoEn,
            EsEntradaProactiva,
            MapearModo(Modo, EsEntradaProactiva),
            IdeasOfrecidas.Select(o => new OpcionIdeaOfrecida(
                o.IdeaId, o.ConversacionId, o.ResumenSnapshot, o.EstadoSnapshot, o.Orden)),
            IdeaSeleccionadaId,
            Idioma ?? "es");

    private static string MapearEstado(EstadoEnrutamientoAporte estado)
        => estado switch
        {
            EstadoEnrutamientoAporte.SeleccionCampania => "seleccionCampania",
            EstadoEnrutamientoAporte.SeleccionPregunta => "seleccionPregunta",
            EstadoEnrutamientoAporte.SeleccionIdea => "seleccionIdea",
            EstadoEnrutamientoAporte.Listo => "listo",
            EstadoEnrutamientoAporte.EnIdea => "enIdea",
            EstadoEnrutamientoAporte.Completado => "completado",
            EstadoEnrutamientoAporte.Expirado => "expirado",
            EstadoEnrutamientoAporte.Cancelado => "cancelado",
            _ => throw new InvalidOperationException($"Estado de enrutamiento no soportado: {estado}."),
        };

    private static EstadoEnrutamientoAporte MapearEstado(string estado)
        => estado switch
        {
            "seleccionCampania" => EstadoEnrutamientoAporte.SeleccionCampania,
            "seleccionPregunta" => EstadoEnrutamientoAporte.SeleccionPregunta,
            "seleccionIdea" => EstadoEnrutamientoAporte.SeleccionIdea,
            "listo" => EstadoEnrutamientoAporte.Listo,
            "enIdea" => EstadoEnrutamientoAporte.EnIdea,
            "completado" => EstadoEnrutamientoAporte.Completado,
            "expirado" => EstadoEnrutamientoAporte.Expirado,
            "cancelado" => EstadoEnrutamientoAporte.Cancelado,
            _ => throw new InvalidOperationException($"Estado de enrutamiento no soportado en Cosmos: {estado}."),
        };

    private static string MapearModo(ModoEnrutamientoAporte modo)
        => modo switch
        {
            ModoEnrutamientoAporte.EntradaProactiva => "entradaProactiva",
            ModoEnrutamientoAporte.RetomarIdea => "retomarIdea",
            ModoEnrutamientoAporte.ConsultarIdea => "consultarIdea",
            _ => "aporte",
        };

    private static ModoEnrutamientoAporte MapearModo(string? modo, bool esEntradaProactiva)
        => modo switch
        {
            "entradaProactiva" => ModoEnrutamientoAporte.EntradaProactiva,
            "retomarIdea" => ModoEnrutamientoAporte.RetomarIdea,
            "consultarIdea" => ModoEnrutamientoAporte.ConsultarIdea,
            _ when esEntradaProactiva => ModoEnrutamientoAporte.EntradaProactiva,
            _ => ModoEnrutamientoAporte.Aporte,
        };

    internal sealed class OpcionCampaniaDocument
    {
        [JsonProperty("campaniaId")]
        public string CampaniaId { get; init; } = string.Empty;

        [JsonProperty("nombreSnapshot")]
        public string NombreSnapshot { get; init; } = string.Empty;

        [JsonProperty("orden")]
        public int Orden { get; init; }
    }

    internal sealed class OpcionPreguntaDocument
    {
        [JsonProperty("preguntaId")]
        public string PreguntaId { get; init; } = string.Empty;

        [JsonProperty("textoSnapshot")]
        public string TextoSnapshot { get; init; } = string.Empty;

        [JsonProperty("orden")]
        public int Orden { get; init; }
    }

    internal sealed class OpcionIdeaDocument
    {
        [JsonProperty("ideaId")]
        public string IdeaId { get; init; } = string.Empty;

        [JsonProperty("conversacionId")]
        public string ConversacionId { get; init; } = string.Empty;

        [JsonProperty("resumenSnapshot")]
        public string ResumenSnapshot { get; init; } = string.Empty;

        [JsonProperty("estadoSnapshot")]
        public string EstadoSnapshot { get; init; } = string.Empty;

        [JsonProperty("orden")]
        public int Orden { get; init; }
    }

    internal sealed class IntentoSeleccionDocument
    {
        [JsonProperty("whatsappMessageId")]
        public string WhatsappMessageId { get; init; } = string.Empty;

        [JsonProperty("tipo")]
        public string Tipo { get; init; } = "campania";

        [JsonProperty("resultado")]
        public string Resultado { get; init; } = "invalido";

        [JsonProperty("fecha")]
        public DateTimeOffset Fecha { get; init; }
    }
}
