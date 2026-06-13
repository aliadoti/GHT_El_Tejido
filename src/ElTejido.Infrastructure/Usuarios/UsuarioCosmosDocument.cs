using ElTejido.Domain.Common;
using ElTejido.Domain.Identidad;
using ElTejido.Domain.Usuarios;
using Newtonsoft.Json;

namespace ElTejido.Infrastructure.Usuarios;

internal sealed class UsuarioCosmosDocument
{
    public const string DocumentType = "Usuario";
    public const string PartitionKeyValue = "usuario";

    [JsonProperty("id")]
    public string Id { get; init; } = string.Empty;

    [JsonProperty("type")]
    public string Type { get; init; } = DocumentType;

    [JsonProperty("pk")]
    public string Pk { get; init; } = PartitionKeyValue;

    [JsonProperty("nombre")]
    public string Nombre { get; init; } = string.Empty;

    [JsonProperty("whatsappNormalizado")]
    public string WhatsappNormalizado { get; init; } = string.Empty;

    [JsonProperty("rol")]
    public string Rol { get; init; } = string.Empty;

    [JsonProperty("estado")]
    public string Estado { get; init; } = string.Empty;

    [JsonProperty("area")]
    public string Area { get; init; } = string.Empty;

    [JsonProperty("empresa")]
    public string Empresa { get; init; } = string.Empty;

    [JsonProperty("tags")]
    public IReadOnlyCollection<string> Tags { get; init; } = [];

    [JsonProperty("propiedadesDinamicas")]
    public IReadOnlyDictionary<string, object?> PropiedadesDinamicas { get; init; }
        = new Dictionary<string, object?>();

    [JsonProperty("creadoEn")]
    public DateTimeOffset CreadoEn { get; init; }

    [JsonProperty("actualizadoEn")]
    public DateTimeOffset ActualizadoEn { get; init; }

    public static UsuarioCosmosDocument FromDomain(Usuario usuario)
    {
        return new UsuarioCosmosDocument
        {
            Id = usuario.Id,
            Type = DocumentType,
            Pk = PartitionKeyValue,
            Nombre = usuario.Nombre,
            WhatsappNormalizado = usuario.WhatsappNormalizado.Valor,
            Rol = ToCosmosRol(usuario.Rol),
            Estado = ToCosmosEstado(usuario.Estado),
            Area = usuario.Area,
            Empresa = usuario.Empresa,
            Tags = usuario.Tags.ToArray(),
            PropiedadesDinamicas = new Dictionary<string, object?>(
                usuario.PropiedadesDinamicas,
                StringComparer.Ordinal),
            CreadoEn = usuario.CreadoEn,
            ActualizadoEn = usuario.ActualizadoEn,
        };
    }

    public Usuario ToDomain()
    {
        return Usuario.Crear(
            Id,
            Nombre,
            NumeroWhatsApp.FromNormalized(WhatsappNormalizado),
            ParseRol(Rol),
            ParseEstado(Estado),
            Area,
            Empresa,
            Tags,
            PropiedadesDinamicas,
            CreadoEn,
            ActualizadoEn);
    }

    public static string ToCosmosRol(RolUsuario rol)
    {
        return rol switch
        {
            RolUsuario.Participante => "participante",
            RolUsuario.Admin => "admin",
            RolUsuario.Visor => "visor",
            _ => throw new InvalidOperationException($"Rol de usuario no soportado: {rol}."),
        };
    }

    public static string ToCosmosEstado(EstadoRegistro estado)
    {
        return estado switch
        {
            EstadoRegistro.Activo => "activo",
            EstadoRegistro.Inactivo => "inactivo",
            _ => throw new InvalidOperationException($"Estado de registro no soportado: {estado}."),
        };
    }

    private static RolUsuario ParseRol(string rol)
    {
        return rol switch
        {
            "participante" => RolUsuario.Participante,
            "admin" => RolUsuario.Admin,
            "visor" => RolUsuario.Visor,
            _ => throw new InvalidOperationException($"Rol de usuario no soportado en Cosmos: {rol}."),
        };
    }

    private static EstadoRegistro ParseEstado(string estado)
    {
        return estado switch
        {
            "activo" => EstadoRegistro.Activo,
            "inactivo" => EstadoRegistro.Inactivo,
            _ => throw new InvalidOperationException($"Estado de registro no soportado en Cosmos: {estado}."),
        };
    }
}
