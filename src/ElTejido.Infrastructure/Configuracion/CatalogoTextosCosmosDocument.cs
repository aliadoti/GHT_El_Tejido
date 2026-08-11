using ElTejido.Domain.Configuracion;
using Newtonsoft.Json;

namespace ElTejido.Infrastructure.Configuracion;

internal sealed class CatalogoTextosCosmosDocument
{
    public const string DocumentType = "CatalogoTextosConversacion";
    public const string PartitionKeyValue = "CatalogoTextosConversacion";

    [JsonProperty("id")]
    public string Id { get; init; } = string.Empty;

    [JsonProperty("type")]
    public string Type { get; init; } = DocumentType;

    [JsonProperty("pk")]
    public string Pk { get; init; } = PartitionKeyValue;

    [JsonProperty("familiaId")]
    public string FamiliaId { get; init; } = string.Empty;

    [JsonProperty("idioma")]
    public string Idioma { get; init; } = string.Empty;

    [JsonProperty("version")]
    public int Version { get; init; }

    [JsonProperty("estado")]
    public string Estado { get; init; } = string.Empty;

    [JsonProperty("mensajes")]
    public IReadOnlyDictionary<string, string> Mensajes { get; init; }
        = new Dictionary<string, string>();

    [JsonProperty("frases")]
    public IReadOnlyDictionary<string, IReadOnlyCollection<string>> Frases { get; init; }
        = new Dictionary<string, IReadOnlyCollection<string>>();

    [JsonProperty("creadoPor")]
    public string CreadoPor { get; init; } = string.Empty;

    [JsonProperty("aprobadoPor", NullValueHandling = NullValueHandling.Ignore)]
    public string? AprobadoPor { get; init; }

    [JsonProperty("creadoEn")]
    public DateTimeOffset CreadoEn { get; init; }

    [JsonProperty("actualizadoEn")]
    public DateTimeOffset ActualizadoEn { get; init; }

    [JsonProperty("activadoEn", NullValueHandling = NullValueHandling.Ignore)]
    public DateTimeOffset? ActivadoEn { get; init; }

    [JsonProperty("huella")]
    public string Huella { get; init; } = string.Empty;

    [JsonProperty("_etag")]
    public string Etag { get; init; } = string.Empty;

    public static CatalogoTextosCosmosDocument FromDomain(CatalogoTextosConversacion catalogo)
        => new()
        {
            Id = CrearId(catalogo.FamiliaId, catalogo.Idioma, catalogo.Version),
            FamiliaId = catalogo.FamiliaId,
            Idioma = catalogo.Idioma,
            Version = catalogo.Version,
            Estado = ToCosmosEstado(catalogo.Estado),
            Mensajes = catalogo.Mensajes,
            Frases = catalogo.Frases,
            CreadoPor = catalogo.CreadoPor,
            AprobadoPor = catalogo.AprobadoPor,
            CreadoEn = catalogo.CreadoEn,
            ActualizadoEn = catalogo.ActualizadoEn,
            ActivadoEn = catalogo.ActivadoEn,
            Huella = catalogo.Huella,
        };

    public CatalogoTextosConversacion ToDomain()
        => CatalogoTextosConversacion.Crear(
            FamiliaId,
            Idioma,
            Version,
            Estado switch
            {
                "borrador" => EstadoCatalogoTextos.Borrador,
                "activo" => EstadoCatalogoTextos.Activo,
                "inactivo" => EstadoCatalogoTextos.Inactivo,
                _ => throw new InvalidOperationException($"Estado de catalogo no soportado: {Estado}."),
            },
            Mensajes,
            Frases,
            CreadoPor,
            AprobadoPor,
            CreadoEn,
            ActualizadoEn,
            ActivadoEn,
            Huella);

    public static string CrearId(string familiaId, string idioma, int version)
        => $"{familiaId}_{idioma}_v{version}";

    public static string ToCosmosEstado(EstadoCatalogoTextos estado)
        => estado switch
        {
            EstadoCatalogoTextos.Borrador => "borrador",
            EstadoCatalogoTextos.Activo => "activo",
            EstadoCatalogoTextos.Inactivo => "inactivo",
            _ => throw new InvalidOperationException($"Estado de catalogo no soportado: {estado}."),
        };
}

/// <summary>
/// Puntero singleton por idioma. Su ETag participa en el batch de activacion y evita que dos
/// activaciones concurrentes, cuando aun no existe un activo, dejen dos snapshots activos.
/// </summary>
internal sealed record CatalogoTextosActivoCosmosDocument
{
    public const string DocumentType = "CatalogoTextosActivo";

    [JsonProperty("id")]
    public string Id { get; init; } = string.Empty;

    [JsonProperty("type")]
    public string Type { get; init; } = DocumentType;

    [JsonProperty("pk")]
    public string Pk { get; init; } = CatalogoTextosCosmosDocument.PartitionKeyValue;

    [JsonProperty("idioma")]
    public string Idioma { get; init; } = string.Empty;

    [JsonProperty("familiaId")]
    public string FamiliaId { get; init; } = string.Empty;

    [JsonProperty("version")]
    public int Version { get; init; }

    [JsonProperty("actualizadoEn")]
    public DateTimeOffset ActualizadoEn { get; init; }

    [JsonProperty("_etag")]
    public string Etag { get; init; } = string.Empty;

    public static string CrearId(string idioma) => $"catalogo_textos_activo_{idioma}";

    public static CatalogoTextosActivoCosmosDocument Crear(CatalogoTextosConversacion catalogo)
        => new()
        {
            Id = CrearId(catalogo.Idioma),
            Idioma = catalogo.Idioma,
            FamiliaId = catalogo.FamiliaId,
            Version = catalogo.Version,
            ActualizadoEn = catalogo.ActualizadoEn,
        };
}
