using System.Text.Json;
using ElTejido.Domain.Common;
using ElTejido.Domain.Configuracion;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ElTejido.Infrastructure.Configuracion;

internal sealed class ConfigCosmosDocument
{
    public const string RubricaType = "Rubrica";
    public const string PromptType = "Prompt";
    public const string ConfigLlmType = "ConfigLLM";

    [JsonProperty("id")]
    public string Id { get; init; } = string.Empty;

    [JsonProperty("type")]
    public string Type { get; init; } = string.Empty;

    [JsonProperty("pk")]
    public string Pk { get; init; } = string.Empty;

    [JsonProperty("familiaId")]
    public string? FamiliaId { get; init; }

    [JsonProperty("nombre")]
    public string Nombre { get; init; } = string.Empty;

    [JsonProperty("descripcion")]
    public string? Descripcion { get; init; }

    [JsonProperty("contenidoMarkdown")]
    public string? ContenidoMarkdown { get; init; }

    [JsonProperty("escala")]
    public EscalaDocument? Escala { get; init; }

    [JsonProperty("criterios")]
    public IReadOnlyCollection<CriterioDocument>? Criterios { get; init; }

    [JsonProperty("contenido")]
    public string? Contenido { get; init; }

    [JsonProperty("tipoPrompt")]
    public string? TipoPrompt { get; init; }

    [JsonProperty("aprobadoPor")]
    public string? AprobadoPor { get; init; }

    [JsonProperty("fechaAprobacion")]
    public DateTimeOffset? FechaAprobacion { get; init; }

    [JsonProperty("proveedor")]
    public string? Proveedor { get; init; }

    [JsonProperty("modelo")]
    public string? Modelo { get; init; }

    [JsonProperty("endpoint")]
    public string? Endpoint { get; init; }

    [JsonProperty("apiKeyRef")]
    public string? ApiKeyRef { get; init; }

    [JsonProperty("parametros")]
    public IReadOnlyDictionary<string, object?>? Parametros { get; init; }

    [JsonProperty("limitesTokens")]
    public LimitesTokensDocument? LimitesTokens { get; init; }

    [JsonProperty("timeoutSegundos")]
    public int? TimeoutSegundos { get; init; }

    [JsonProperty("maxReintentos")]
    public int? MaxReintentos { get; init; }

    [JsonProperty("version")]
    public int Version { get; init; }

    [JsonProperty("estado")]
    public string Estado { get; init; } = string.Empty;

    [JsonProperty("creadoEn")]
    public DateTimeOffset CreadoEn { get; init; }

    [JsonProperty("actualizadoEn")]
    public DateTimeOffset ActualizadoEn { get; init; }

    public static ConfigCosmosDocument FromRubrica(Rubrica rubrica)
        => new()
        {
            Id = VersionedId(rubrica.Id, rubrica.Version),
            Type = RubricaType,
            Pk = RubricaType,
            FamiliaId = rubrica.Id,
            Nombre = rubrica.Nombre,
            Descripcion = rubrica.Descripcion,
            ContenidoMarkdown = rubrica.ContenidoMarkdown,
            Escala = EscalaDocument.FromDomain(rubrica.Escala),
            Criterios = rubrica.Criterios.Select(CriterioDocument.FromDomain).ToArray(),
            Version = rubrica.Version,
            Estado = ToCosmosEstadoRubrica(rubrica.Estado),
            CreadoEn = rubrica.CreadoEn,
            ActualizadoEn = rubrica.ActualizadoEn,
        };

    public Rubrica ToRubrica()
        => Rubrica.Crear(
            FamiliaId ?? Id,
            Nombre,
            Descripcion ?? string.Empty,
            ContenidoMarkdown ?? string.Empty,
            (Escala ?? new EscalaDocument()).ToDomain(),
            (Criterios ?? Array.Empty<CriterioDocument>()).Select(c => c.ToDomain()),
            Version,
            ParseEstadoRubrica(Estado),
            CreadoEn,
            ActualizadoEn);

    public static ConfigCosmosDocument FromPrompt(Prompt prompt)
        => new()
        {
            Id = VersionedId(prompt.Id, prompt.Version),
            Type = PromptType,
            Pk = PromptType,
            FamiliaId = prompt.Id,
            Nombre = prompt.Nombre,
            TipoPrompt = prompt.TipoPrompt,
            Contenido = prompt.Contenido,
            Version = prompt.Version,
            Estado = ToCosmosEstadoPrompt(prompt.Estado),
            AprobadoPor = prompt.AprobadoPor,
            FechaAprobacion = prompt.FechaAprobacion,
            CreadoEn = prompt.CreadoEn,
            ActualizadoEn = prompt.ActualizadoEn,
        };

    public Prompt ToPrompt()
        => Prompt.Crear(
            FamiliaId ?? Id,
            Nombre,
            TipoPrompt ?? string.Empty,
            Contenido ?? string.Empty,
            Version,
            ParseEstadoPrompt(Estado),
            AprobadoPor,
            FechaAprobacion,
            CreadoEn,
            ActualizadoEn);

    public static ConfigCosmosDocument FromConfigLlm(ConfigLlm config)
        => new()
        {
            Id = config.Id,
            Type = ConfigLlmType,
            Pk = ConfigLlmType,
            Nombre = config.Nombre,
            Proveedor = config.Proveedor,
            Modelo = config.Modelo,
            Endpoint = config.Endpoint,
            ApiKeyRef = config.ApiKeyRef,
            Parametros = NormalizeParametros(config.Parametros),
            LimitesTokens = LimitesTokensDocument.FromDomain(config.LimitesTokens),
            TimeoutSegundos = config.TimeoutSegundos,
            MaxReintentos = config.MaxReintentos,
            Estado = ToCosmosEstadoRegistro(config.Estado),
            CreadoEn = config.CreadoEn,
            ActualizadoEn = config.ActualizadoEn,
        };

    public ConfigLlm ToConfigLlm()
        => ConfigLlm.Crear(
            Id,
            Nombre,
            Proveedor ?? string.Empty,
            Modelo ?? string.Empty,
            Endpoint ?? string.Empty,
            ApiKeyRef ?? string.Empty,
            NormalizeParametros(Parametros),
            (LimitesTokens ?? new LimitesTokensDocument()).ToDomain(),
            TimeoutSegundos ?? 30,
            MaxReintentos ?? 2,
            ParseEstadoRegistro(Estado),
            CreadoEn,
            ActualizadoEn);

    private static string VersionedId(string id, int version) => $"{id}_v{version}";

    private static IReadOnlyDictionary<string, object?> NormalizeParametros(IReadOnlyDictionary<string, object?>? parametros)
        => parametros?.ToDictionary(p => p.Key, p => NormalizeValue(p.Value), StringComparer.Ordinal)
            ?? new Dictionary<string, object?>();

    // Convierte los valores de parametros a escalares nativos antes de persistir/al leer. La API los
    // entrega como System.Text.Json.JsonElement y Cosmos como Newtonsoft.JValue; sin esto un numero
    // (p. ej. temperature 0.2) se serializaba como objeto y el proveedor LLM lo rechazaba (400).
    private static object? NormalizeValue(object? value)
        => value switch
        {
            JValue jValue => jValue.Value,
            JsonElement element => FromJsonElement(element),
            _ => value,
        };

    private static object? FromJsonElement(JsonElement element)
        => element.ValueKind switch
        {
            JsonValueKind.Number => element.TryGetInt64(out var entero) ? entero : element.GetDouble(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.GetRawText(),
        };

    private static string ToCosmosEstadoRubrica(EstadoRubrica estado)
        => estado switch
        {
            EstadoRubrica.Activa => "activa",
            EstadoRubrica.Archivada => "archivada",
            EstadoRubrica.Borrador => "borrador",
            _ => throw new InvalidOperationException($"Estado de rubrica no soportado: {estado}."),
        };

    private static EstadoRubrica ParseEstadoRubrica(string estado)
        => estado switch
        {
            "activa" => EstadoRubrica.Activa,
            "archivada" => EstadoRubrica.Archivada,
            "borrador" => EstadoRubrica.Borrador,
            _ => throw new InvalidOperationException($"Estado de rubrica no soportado: {estado}."),
        };

    public static string ToCosmosEstadoPrompt(EstadoPrompt estado)
        => estado switch
        {
            EstadoPrompt.Borrador => "borrador",
            EstadoPrompt.Activo => "activo",
            EstadoPrompt.Inactivo => "inactivo",
            _ => throw new InvalidOperationException($"Estado de prompt no soportado: {estado}."),
        };

    private static EstadoPrompt ParseEstadoPrompt(string estado)
        => estado switch
        {
            "borrador" => EstadoPrompt.Borrador,
            "activo" => EstadoPrompt.Activo,
            "inactivo" => EstadoPrompt.Inactivo,
            _ => throw new InvalidOperationException($"Estado de prompt no soportado: {estado}."),
        };

    public static string ToCosmosEstadoRegistro(EstadoRegistro estado)
        => estado == EstadoRegistro.Activo ? "activa" : "inactiva";

    private static EstadoRegistro ParseEstadoRegistro(string estado)
        => estado switch
        {
            "activa" => EstadoRegistro.Activo,
            "inactiva" => EstadoRegistro.Inactivo,
            _ => throw new InvalidOperationException($"Estado de config LLM no soportado: {estado}."),
        };

    internal sealed class EscalaDocument
    {
        [JsonProperty("min")]
        public int Min { get; init; } = 1;

        [JsonProperty("max")]
        public int Max { get; init; } = 5;

        public static EscalaDocument FromDomain(EscalaRubrica escala)
            => new() { Min = escala.Min, Max = escala.Max };

        public EscalaRubrica ToDomain() => EscalaRubrica.Crear(Min, Max);
    }

    internal sealed class CriterioDocument
    {
        [JsonProperty("nombre")]
        public string Nombre { get; init; } = string.Empty;

        [JsonProperty("peso")]
        public decimal Peso { get; init; }

        public static CriterioDocument FromDomain(CriterioRubrica criterio)
            => new() { Nombre = criterio.Nombre, Peso = criterio.Peso };

        public CriterioRubrica ToDomain() => CriterioRubrica.Crear(Nombre, Peso);
    }

    internal sealed class LimitesTokensDocument
    {
        [JsonProperty("maxPrompt")]
        public int MaxPrompt { get; init; } = 6000;

        [JsonProperty("maxCompletion")]
        public int MaxCompletion { get; init; } = 800;

        public static LimitesTokensDocument FromDomain(LimitesTokensLlm limites)
            => new() { MaxPrompt = limites.MaxPrompt, MaxCompletion = limites.MaxCompletion };

        public LimitesTokensLlm ToDomain() => LimitesTokensLlm.Crear(MaxPrompt, MaxCompletion);
    }
}
