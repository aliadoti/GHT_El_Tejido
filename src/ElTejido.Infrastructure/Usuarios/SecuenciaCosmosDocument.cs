using Newtonsoft.Json;

namespace ElTejido.Infrastructure.Usuarios;

/// <summary>
/// Contador de <c>codigoUsuario</c> del contenedor <c>users</c> (03 §3.1.1, I-08 §3.1.b). Cosmos no
/// tiene autoincremento: se emula con un documento unico por secuencia y concurrencia optimista por
/// ETag. Lleva <c>claveUnicidad</c> como cualquier documento del contenedor para no colisionar con la
/// unique key <c>/claveUnicidad</c> (Cosmos trata el path ausente como <c>null</c> y tambien lo hace
/// unico).
/// </summary>
internal sealed class SecuenciaCosmosDocument
{
    public const string DocumentType = "Secuencia";
    public const string PartitionKeyValue = "secuencia";
    public const string IdUsuario = "seq_usuario";

    [JsonProperty("id")]
    public string Id { get; init; } = IdUsuario;

    [JsonProperty("type")]
    public string Type { get; init; } = DocumentType;

    [JsonProperty("pk")]
    public string Pk { get; init; } = PartitionKeyValue;

    [JsonProperty("claveUnicidad")]
    public string ClaveUnicidad { get; init; } = string.Empty;

    [JsonProperty("ultimoValor")]
    public int UltimoValor { get; init; }

    [JsonProperty("actualizadoEn")]
    public DateTimeOffset ActualizadoEn { get; init; }

    /// <summary>ETag del documento leido; solo lo puebla Cosmos y se usa como <c>If-Match</c>.</summary>
    [JsonProperty("_etag", NullValueHandling = NullValueHandling.Ignore)]
    public string? ETag { get; init; }

    public static SecuenciaCosmosDocument Crear(string id, int ultimoValor, DateTimeOffset actualizadoEn)
        => new()
        {
            Id = id,
            Type = DocumentType,
            Pk = PartitionKeyValue,
            ClaveUnicidad = ConstruirClaveUnicidad(id),
            UltimoValor = ultimoValor,
            ActualizadoEn = actualizadoEn,
        };

    public static string ConstruirClaveUnicidad(string id) => "seq|" + id;
}
