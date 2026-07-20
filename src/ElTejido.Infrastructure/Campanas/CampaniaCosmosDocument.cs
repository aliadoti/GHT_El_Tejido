using ElTejido.Domain.Campanas;
using ElTejido.Domain.Common;
using Newtonsoft.Json;

namespace ElTejido.Infrastructure.Campanas;

internal sealed class CampaniaCosmosDocument
{
    public const string DocumentType = "Campania";

    [JsonProperty("id")]
    public string Id { get; init; } = string.Empty;

    [JsonProperty("type")]
    public string Type { get; init; } = DocumentType;

    [JsonProperty("nombre")]
    public string Nombre { get; init; } = string.Empty;

    [JsonProperty("descripcion")]
    public string Descripcion { get; init; } = string.Empty;

    [JsonProperty("objetivo")]
    public string Objetivo { get; init; } = string.Empty;

    [JsonProperty("estado")]
    public string Estado { get; init; } = string.Empty;

    [JsonProperty("mensajesIniciales")]
    public IReadOnlyCollection<MensajeInicialDocument> MensajesIniciales { get; init; } = [];

    [JsonProperty("preguntas")]
    public IReadOnlyCollection<PreguntaDocument> Preguntas { get; init; } = [];

    [JsonProperty("rubricaRef")]
    public string RubricaRef { get; init; } = string.Empty;

    [JsonProperty("promptRefs")]
    public IReadOnlyDictionary<string, string> PromptRefs { get; init; } = new Dictionary<string, string>();

    [JsonProperty("configLLMRef")]
    public string ConfigLlmRef { get; init; } = string.Empty;

    [JsonProperty("configMarkdown")]
    public ConfigMarkdownDocument ConfigMarkdown { get; init; } = new();

    [JsonProperty("configConversacional")]
    public ConfigConversacionalDocument ConfigConversacional { get; init; } = new();

    [JsonProperty("configSeguridad")]
    public LimitesSeguridadDocument ConfigSeguridad { get; init; } = new();

    [JsonProperty("usuariosHabilitados")]
    public IReadOnlyCollection<string> UsuariosHabilitados { get; init; } = [];

    [JsonProperty("creadoEn")]
    public DateTimeOffset CreadoEn { get; init; }

    [JsonProperty("actualizadoEn")]
    public DateTimeOffset ActualizadoEn { get; init; }

    public static CampaniaCosmosDocument FromDomain(Campania campania)
    {
        return new CampaniaCosmosDocument
        {
            Id = campania.Id,
            Type = DocumentType,
            Nombre = campania.Nombre,
            Descripcion = campania.Descripcion,
            Objetivo = campania.Objetivo,
            Estado = ToCosmosEstado(campania.Estado),
            MensajesIniciales = campania.MensajesIniciales
                .Select(MensajeInicialDocument.FromDomain)
                .ToArray(),
            Preguntas = campania.Preguntas
                .Select(PreguntaDocument.FromDomain)
                .ToArray(),
            RubricaRef = campania.RubricaRef,
            PromptRefs = new Dictionary<string, string>(campania.PromptRefs, StringComparer.Ordinal),
            ConfigLlmRef = campania.ConfigLlmRef,
            ConfigMarkdown = ConfigMarkdownDocument.FromDomain(campania.ConfigMarkdown),
            ConfigConversacional = ConfigConversacionalDocument.FromDomain(campania.ConfigConversacional),
            ConfigSeguridad = LimitesSeguridadDocument.FromDomain(campania.ConfigSeguridad),
            UsuariosHabilitados = campania.UsuariosHabilitados.ToArray(),
            CreadoEn = campania.CreadoEn,
            ActualizadoEn = campania.ActualizadoEn,
        };
    }

    public Campania ToDomain()
    {
        return Campania.Crear(
            Id,
            Nombre,
            Descripcion,
            Objetivo,
            ParseEstadoCampania(Estado),
            MensajesIniciales.Select(mensaje => mensaje.ToDomain()),
            Preguntas.Select(pregunta => pregunta.ToDomain()),
            RubricaRef,
            PromptRefs,
            ConfigLlmRef,
            ConfigMarkdown.ToDomain(),
            ConfigConversacional.ToDomain(),
            ConfigSeguridad.ToDomain(),
            UsuariosHabilitados,
            CreadoEn,
            ActualizadoEn);
    }

    public static string ToCosmosEstado(EstadoCampania estado)
    {
        return estado switch
        {
            EstadoCampania.Borrador => "borrador",
            EstadoCampania.Activa => "activa",
            EstadoCampania.Cerrada => "cerrada",
            EstadoCampania.Archivada => "archivada",
            _ => throw new InvalidOperationException($"Estado de campania no soportado: {estado}."),
        };
    }

    private static EstadoCampania ParseEstadoCampania(string estado)
    {
        return estado switch
        {
            "borrador" => EstadoCampania.Borrador,
            "activa" => EstadoCampania.Activa,
            "cerrada" => EstadoCampania.Cerrada,
            "archivada" => EstadoCampania.Archivada,
            _ => throw new InvalidOperationException($"Estado de campania no soportado en Cosmos: {estado}."),
        };
    }

    private static string ToCosmosEstado(EstadoRegistro estado)
    {
        return estado switch
        {
            EstadoRegistro.Activo => "activo",
            EstadoRegistro.Inactivo => "inactivo",
            _ => throw new InvalidOperationException($"Estado de registro no soportado: {estado}."),
        };
    }

    private static EstadoRegistro ParseEstadoRegistro(string estado)
    {
        return estado switch
        {
            "activo" => EstadoRegistro.Activo,
            "inactivo" => EstadoRegistro.Inactivo,
            _ => throw new InvalidOperationException($"Estado de registro no soportado en Cosmos: {estado}."),
        };
    }

    private static string ToCosmosTipoArtefacto(TipoArtefactoMarkdown tipoArtefacto)
    {
        return tipoArtefacto switch
        {
            TipoArtefactoMarkdown.Respuesta => "respuesta",
            TipoArtefactoMarkdown.Participante => "participante",
            TipoArtefactoMarkdown.Campania => "campania",
            TipoArtefactoMarkdown.Entidad => "entidad",
            TipoArtefactoMarkdown.Capitulo => "capitulo",
            _ => throw new InvalidOperationException($"Tipo de artefacto no soportado: {tipoArtefacto}."),
        };
    }

    private static TipoArtefactoMarkdown ParseTipoArtefacto(string tipoArtefacto)
    {
        return tipoArtefacto switch
        {
            "respuesta" => TipoArtefactoMarkdown.Respuesta,
            "participante" => TipoArtefactoMarkdown.Participante,
            "campania" => TipoArtefactoMarkdown.Campania,
            "entidad" => TipoArtefactoMarkdown.Entidad,
            "capitulo" => TipoArtefactoMarkdown.Capitulo,
            _ => throw new InvalidOperationException($"Tipo de artefacto no soportado en Cosmos: {tipoArtefacto}."),
        };
    }

    internal sealed class MensajeInicialDocument
    {
        [JsonProperty("id")]
        public string Id { get; init; } = string.Empty;

        [JsonProperty("nombreInterno")]
        public string NombreInterno { get; init; } = string.Empty;

        [JsonProperty("texto")]
        public string Texto { get; init; } = string.Empty;

        [JsonProperty("orden")]
        public int Orden { get; init; }

        [JsonProperty("variablesDinamicas")]
        public IReadOnlyCollection<string> VariablesDinamicas { get; init; } = [];

        [JsonProperty("estado")]
        public string Estado { get; init; } = string.Empty;

        [JsonProperty("plantillaWhatsApp")]
        public PlantillaWhatsAppDocument? PlantillaWhatsApp { get; init; }

        public static MensajeInicialDocument FromDomain(MensajeInicial mensaje)
        {
            return new MensajeInicialDocument
            {
                Id = mensaje.Id,
                NombreInterno = mensaje.NombreInterno,
                Texto = mensaje.Texto,
                Orden = mensaje.Orden,
                VariablesDinamicas = mensaje.VariablesDinamicas.ToArray(),
                Estado = ToCosmosEstado(mensaje.Estado),
                PlantillaWhatsApp = mensaje.PlantillaWhatsApp is null
                    ? null
                    : PlantillaWhatsAppDocument.FromDomain(mensaje.PlantillaWhatsApp),
            };
        }

        public MensajeInicial ToDomain()
        {
            return MensajeInicial.Crear(
                Id,
                NombreInterno,
                Texto,
                Orden,
                VariablesDinamicas,
                ParseEstadoRegistro(Estado),
                PlantillaWhatsApp?.ToDomain());
        }
    }

    internal sealed class PlantillaWhatsAppDocument
    {
        [JsonProperty("nombre")]
        public string Nombre { get; init; } = string.Empty;

        [JsonProperty("idioma")]
        public string Idioma { get; init; } = string.Empty;

        [JsonProperty("componentes")]
        public IReadOnlyCollection<string> Componentes { get; init; } = [];

        public static PlantillaWhatsAppDocument FromDomain(PlantillaWhatsApp plantilla)
        {
            return new PlantillaWhatsAppDocument
            {
                Nombre = plantilla.Nombre,
                Idioma = plantilla.Idioma,
                Componentes = plantilla.Componentes.ToArray(),
            };
        }

        public PlantillaWhatsApp ToDomain()
        {
            return PlantillaWhatsApp.Crear(Nombre, Idioma, Componentes);
        }
    }

    internal sealed class PreguntaDocument
    {
        [JsonProperty("id")]
        public string Id { get; init; } = string.Empty;

        [JsonProperty("texto")]
        public string Texto { get; init; } = string.Empty;

        [JsonProperty("instruccion")]
        public string Instruccion { get; init; } = string.Empty;

        [JsonProperty("categoria")]
        public string Categoria { get; init; } = string.Empty;

        [JsonProperty("orden")]
        public int Orden { get; init; }

        [JsonProperty("estado")]
        public string Estado { get; init; } = string.Empty;

        [JsonProperty("rubricaRef")]
        public string? RubricaRef { get; init; }

        [JsonProperty("versionRubrica")]
        public int? VersionRubrica { get; init; }

        [JsonProperty("promptRefs")]
        public IReadOnlyDictionary<string, string> PromptRefs { get; init; } = new Dictionary<string, string>();

        [JsonProperty("maxRepreguntas")]
        public int MaxRepreguntas { get; init; }

        [JsonProperty("limitesSeguridad")]
        public LimitesSeguridadPreguntaDocument LimitesSeguridad { get; init; } = new();

        [JsonProperty("configMarkdown")]
        public ConfigMarkdownDocument ConfigMarkdown { get; init; } = new();

        public static PreguntaDocument FromDomain(Pregunta pregunta)
        {
            return new PreguntaDocument
            {
                Id = pregunta.Id,
                Texto = pregunta.Texto,
                Instruccion = pregunta.Instruccion,
                Categoria = pregunta.Categoria,
                Orden = pregunta.Orden,
                Estado = ToCosmosEstado(pregunta.Estado),
                RubricaRef = pregunta.RubricaRef,
                VersionRubrica = pregunta.VersionRubrica,
                PromptRefs = new Dictionary<string, string>(pregunta.PromptRefs, StringComparer.Ordinal),
                MaxRepreguntas = pregunta.MaxRepreguntas,
                LimitesSeguridad = LimitesSeguridadPreguntaDocument.FromDomain(pregunta.LimitesSeguridad),
                ConfigMarkdown = ConfigMarkdownDocument.FromDomain(pregunta.ConfigMarkdown),
            };
        }

        public Pregunta ToDomain()
        {
            return Pregunta.Crear(
                Id,
                Texto,
                Instruccion,
                Categoria,
                Orden,
                ParseEstadoRegistro(Estado),
                RubricaRef,
                VersionRubrica,
                PromptRefs,
                MaxRepreguntas,
                LimitesSeguridad.ToDomain(),
                ConfigMarkdown.ToDomain());
        }
    }

    internal sealed class ConfigMarkdownDocument
    {
        [JsonProperty("tipoArtefacto")]
        public string TipoArtefacto { get; init; } = "respuesta";

        public static ConfigMarkdownDocument FromDomain(ConfigMarkdown config)
        {
            return new ConfigMarkdownDocument
            {
                TipoArtefacto = ToCosmosTipoArtefacto(config.TipoArtefacto),
            };
        }

        public ConfigMarkdown ToDomain()
        {
            return ElTejido.Domain.Campanas.ConfigMarkdown.Crear(ParseTipoArtefacto(TipoArtefacto));
        }
    }

    internal sealed class ConfigConversacionalDocument
    {
        [JsonProperty("maxRepreguntas")]
        public int MaxRepreguntas { get; init; }

        [JsonProperty("mensajeCierre")]
        public string MensajeCierre { get; init; } = string.Empty;

        [JsonProperty("segmentacionIdeas")]
        public bool SegmentacionIdeas { get; init; }

        // I-09 (aditivo, 03 §3.3): documento viejo sin el campo deserializa false = autocontenido.
        [JsonProperty("tejidoColectivo")]
        public bool TejidoColectivo { get; init; }

        // I-05 (aditivo, 03 §3.3): documento viejo sin el campo deserializa false = retro clásica.
        [JsonProperty("parafraseo")]
        public bool Parafraseo { get; init; }

        public static ConfigConversacionalDocument FromDomain(ConfigConversacional config)
        {
            return new ConfigConversacionalDocument
            {
                MaxRepreguntas = config.MaxRepreguntas,
                MensajeCierre = config.MensajeCierre,
                SegmentacionIdeas = config.SegmentacionIdeas,
                TejidoColectivo = config.TejidoColectivo,
                Parafraseo = config.Parafraseo,
            };
        }

        public ConfigConversacional ToDomain()
        {
            return ElTejido.Domain.Campanas.ConfigConversacional.Crear(
                MaxRepreguntas, MensajeCierre, SegmentacionIdeas, TejidoColectivo, Parafraseo);
        }
    }

    internal sealed class LimitesSeguridadDocument
    {
        [JsonProperty("maxCaracteresMensaje")]
        public int MaxCaracteresMensaje { get; init; }

        [JsonProperty("maxMensajesPorUsuario")]
        public int MaxMensajesPorUsuario { get; init; }

        [JsonProperty("maxLlamadasLlmPorUsuario")]
        public int MaxLlamadasLlmPorUsuario { get; init; }

        // P-10: presupuesto de tokens de la campaña (aditivo, default 0 = off). Ausente en docs previos.
        [JsonProperty("presupuestoTokensCampania")]
        public int PresupuestoTokensCampania { get; init; }

        public static LimitesSeguridadDocument FromDomain(LimitesSeguridad limites)
        {
            return new LimitesSeguridadDocument
            {
                MaxCaracteresMensaje = limites.MaxCaracteresMensaje,
                MaxMensajesPorUsuario = limites.MaxMensajesPorUsuario,
                MaxLlamadasLlmPorUsuario = limites.MaxLlamadasLlmPorUsuario,
                PresupuestoTokensCampania = limites.PresupuestoTokensCampania,
            };
        }

        public LimitesSeguridad ToDomain()
        {
            return LimitesSeguridad.Crear(
                MaxCaracteresMensaje,
                MaxMensajesPorUsuario,
                MaxLlamadasLlmPorUsuario,
                PresupuestoTokensCampania);
        }
    }

    internal sealed class LimitesSeguridadPreguntaDocument
    {
        [JsonProperty("maxCaracteresMensaje")]
        public int MaxCaracteresMensaje { get; init; }

        [JsonProperty("maxLlamadasLlm")]
        public int MaxLlamadasLlm { get; init; }

        public static LimitesSeguridadPreguntaDocument FromDomain(LimitesSeguridad limites)
        {
            return new LimitesSeguridadPreguntaDocument
            {
                MaxCaracteresMensaje = limites.MaxCaracteresMensaje,
                MaxLlamadasLlm = limites.MaxLlamadasLlmPorUsuario,
            };
        }

        public LimitesSeguridad ToDomain()
        {
            return LimitesSeguridad.ParaPregunta(MaxCaracteresMensaje, MaxLlamadasLlm);
        }
    }
}
