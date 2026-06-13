using ElTejido.Domain.Common;
using ElTejido.Domain.Usuarios;
using Newtonsoft.Json;

namespace ElTejido.Infrastructure.Usuarios;

internal sealed class TagCosmosDocument
{
    public const string DocumentType = "Tag";
    public const string PartitionKeyValue = "tag";

    [JsonProperty("id")]
    public string Id { get; init; } = string.Empty;

    [JsonProperty("type")]
    public string Type { get; init; } = DocumentType;

    [JsonProperty("pk")]
    public string Pk { get; init; } = PartitionKeyValue;

    [JsonProperty("nombre")]
    public string Nombre { get; init; } = string.Empty;

    [JsonProperty("tipoTag")]
    public string TipoTag { get; init; } = string.Empty;

    [JsonProperty("descripcion")]
    public string? Descripcion { get; init; }

    [JsonProperty("estado")]
    public string Estado { get; init; } = string.Empty;

    [JsonProperty("creadoEn")]
    public DateTimeOffset CreadoEn { get; init; }

    public static TagCosmosDocument FromDomain(Tag tag)
    {
        return new TagCosmosDocument
        {
            Id = tag.Id,
            Type = DocumentType,
            Pk = PartitionKeyValue,
            Nombre = tag.Nombre,
            TipoTag = tag.TipoTag,
            Descripcion = tag.Descripcion,
            Estado = UsuarioCosmosDocument.ToCosmosEstado(tag.Estado),
            CreadoEn = tag.CreadoEn,
        };
    }

    public Tag ToDomain()
    {
        return Tag.Crear(
            Id,
            Nombre,
            TipoTag,
            Descripcion,
            ParseEstado(Estado),
            CreadoEn);
    }

    private static EstadoRegistro ParseEstado(string estado)
    {
        return estado switch
        {
            "activo" => EstadoRegistro.Activo,
            "inactivo" => EstadoRegistro.Inactivo,
            _ => throw new InvalidOperationException($"Estado de tag no soportado en Cosmos: {estado}."),
        };
    }
}
