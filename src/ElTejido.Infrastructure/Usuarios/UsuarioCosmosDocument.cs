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

    [JsonProperty("codigoUsuario")]
    public int CodigoUsuario { get; init; }

    /// <summary>
    /// Campo derivado con la unique key del contenedor (03 §3.1, I-08 §3.1.e). Lo calcula
    /// exclusivamente <see cref="FromDomain"/> para que no pueda desincronizarse del <c>estado</c>.
    /// </summary>
    [JsonProperty("claveUnicidad")]
    public string ClaveUnicidad { get; init; } = string.Empty;

    [JsonProperty("nombre")]
    public string Nombre { get; init; } = string.Empty;

    [JsonProperty("nombreSaludo")]
    public string? NombreSaludo { get; init; }

    [JsonProperty("whatsappNormalizado")]
    public string WhatsappNormalizado { get; init; } = string.Empty;

    [JsonProperty("usuarioWhatsapp")]
    public string? UsuarioWhatsapp { get; init; }

    [JsonProperty("rol")]
    public string Rol { get; init; } = string.Empty;

    [JsonProperty("estado")]
    public string Estado { get; init; } = string.Empty;

    [JsonProperty("area")]
    public string? Area { get; init; }

    [JsonProperty("empresa")]
    public string? Empresa { get; init; }

    [JsonProperty("empresaId")]
    public string? EmpresaId { get; init; }

    [JsonProperty("sede")]
    public string? Sede { get; init; }

    [JsonProperty("cargo")]
    public string? Cargo { get; init; }

    [JsonProperty("email")]
    public string? Email { get; init; }

    [JsonProperty("antiguedadAnios")]
    public decimal? AntiguedadAnios { get; init; }

    [JsonProperty("idioma")]
    public string? Idioma { get; init; }

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
            CodigoUsuario = usuario.CodigoUsuario,
            ClaveUnicidad = ConstruirClaveUnicidad(usuario),
            Nombre = usuario.Nombre,
            NombreSaludo = usuario.NombreSaludo,
            WhatsappNormalizado = usuario.WhatsappNormalizado.Valor,
            UsuarioWhatsapp = usuario.UsuarioWhatsapp,
            Rol = ToCosmosRol(usuario.Rol),
            Estado = ToCosmosEstado(usuario.Estado),
            Area = usuario.Area,
            Empresa = usuario.Empresa,
            EmpresaId = usuario.EmpresaId,
            Sede = usuario.Sede,
            Cargo = usuario.Cargo,
            Email = usuario.Email,
            AntiguedadAnios = usuario.AntiguedadAnios,
            Idioma = usuario.Idioma,
            Tags = usuario.Tags.ToArray(),
            PropiedadesDinamicas = new Dictionary<string, object?>(
                usuario.PropiedadesDinamicas,
                StringComparer.Ordinal),
            CreadoEn = usuario.CreadoEn,
            ActualizadoEn = usuario.ActualizadoEn,
        };
    }

    /// <summary>
    /// <c>wa|&lt;numero&gt;</c> mientras el usuario esta activo y <c>hist|&lt;id&gt;</c> cuando queda
    /// inactivo (03 §3.1). Asi la unique key garantiza un solo activo por telefono y deja convivir el
    /// historico de titulares del mismo numero.
    /// </summary>
    public static string ConstruirClaveUnicidad(Usuario usuario)
        => usuario.Estado == EstadoRegistro.Activo
            ? "wa|" + usuario.WhatsappNormalizado.Valor
            : "hist|" + usuario.Id;

    public Usuario ToDomain()
    {
        return Usuario.Crear(
            Id,
            CodigoUsuario,
            Nombre,
            NumeroWhatsApp.FromNormalized(WhatsappNormalizado),
            ParseRol(Rol),
            ParseEstado(Estado),
            Area,
            Empresa,
            Tags,
            PropiedadesDinamicas,
            CreadoEn,
            ActualizadoEn,
            UsuarioWhatsapp,
            EmpresaId,
            Sede,
            Cargo,
            Email,
            AntiguedadAnios,
            Idioma,
            NombreSaludo);
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
