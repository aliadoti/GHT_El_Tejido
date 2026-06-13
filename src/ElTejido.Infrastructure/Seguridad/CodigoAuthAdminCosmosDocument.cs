using ElTejido.Domain.Identidad;
using ElTejido.Domain.Seguridad;
using Newtonsoft.Json;

namespace ElTejido.Infrastructure.Seguridad;

internal sealed class CodigoAuthAdminCosmosDocument
{
    public const string DocumentType = "CodigoAuthAdmin";
    public const string PartitionKeyValue = "CodigoAuthAdmin";

    [JsonProperty("id")]
    public string Id { get; init; } = string.Empty;

    [JsonProperty("type")]
    public string Type { get; init; } = DocumentType;

    [JsonProperty("pk")]
    public string Pk { get; init; } = PartitionKeyValue;

    [JsonProperty("usuarioId")]
    public string UsuarioId { get; init; } = string.Empty;

    [JsonProperty("numero")]
    public string Numero { get; init; } = string.Empty;

    [JsonProperty("hashCodigo")]
    public string HashCodigo { get; init; } = string.Empty;

    [JsonProperty("expiracion")]
    public DateTimeOffset Expiracion { get; init; }

    [JsonProperty("intentosRestantes")]
    public int IntentosRestantes { get; init; }

    [JsonProperty("usado")]
    public bool Usado { get; init; }

    [JsonProperty("creadoEn")]
    public DateTimeOffset CreadoEn { get; init; }

    [JsonProperty("ttl")]
    public int Ttl { get; init; }

    public static CodigoAuthAdminCosmosDocument FromDomain(CodigoAuthAdmin codigo)
    {
        return new CodigoAuthAdminCosmosDocument
        {
            Id = codigo.Id,
            Type = DocumentType,
            Pk = PartitionKeyValue,
            UsuarioId = codigo.UsuarioId,
            Numero = codigo.Numero.Valor,
            HashCodigo = codigo.HashCodigo,
            Expiracion = codigo.Expiracion,
            IntentosRestantes = codigo.IntentosRestantes,
            Usado = codigo.Usado,
            CreadoEn = codigo.CreadoEn,
            Ttl = codigo.Ttl,
        };
    }

    public CodigoAuthAdmin ToDomain()
    {
        return CodigoAuthAdmin.Crear(
            Id,
            UsuarioId,
            NumeroWhatsApp.FromNormalized(Numero),
            HashCodigo,
            Expiracion,
            IntentosRestantes,
            Usado,
            CreadoEn,
            Ttl);
    }
}
