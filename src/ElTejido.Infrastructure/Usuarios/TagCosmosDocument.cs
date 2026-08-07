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

    /// <summary>
    /// <c>tag|&lt;id&gt;</c> (03 §3.2, I-08 §3.1.e). Obligatorio aunque la Tag no tenga relacion con
    /// WhatsApp: comparte contenedor con <c>Usuario</c> y la unique key <c>/claveUnicidad</c> haria
    /// colisionar entre si a todos los documentos que omitieran el campo.
    /// </summary>
    [JsonProperty("claveUnicidad")]
    public string ClaveUnicidad { get; init; } = string.Empty;

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
            ClaveUnicidad = ConstruirClaveUnicidad(tag.Id),
            Nombre = tag.Nombre,
            TipoTag = tag.TipoTag,
            Descripcion = tag.Descripcion,
            Estado = UsuarioCosmosDocument.ToCosmosEstado(tag.Estado),
            CreadoEn = tag.CreadoEn,
        };
    }

    public static string ConstruirClaveUnicidad(string id) => "tag|" + id;

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
