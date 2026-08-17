using System.Globalization;
using ElTejido.Domain.Common;
using ElTejido.Domain.Identidad;
using ElTejido.Domain.Localizacion;

namespace ElTejido.Domain.Usuarios;

public sealed class Usuario
{
    /// <summary>Idioma por defecto del participante cuando la plantilla no lo trae (I-08 §3, columna H).</summary>
    public const string IdiomaPorDefecto = IdiomaConversacion.CodigoEspanol;

    private Usuario(
        string id,
        int codigoUsuario,
        string nombre,
        NumeroWhatsApp whatsappNormalizado,
        string? usuarioWhatsapp,
        RolUsuario rol,
        EstadoRegistro estado,
        string? area,
        string? empresa,
        string? empresaId,
        string? sede,
        string? cargo,
        string? email,
        decimal? antiguedadAnios,
        IdiomaConversacion idioma,
        IReadOnlyCollection<string> tags,
        IReadOnlyDictionary<string, object?> propiedadesDinamicas,
        DateTimeOffset creadoEn,
        DateTimeOffset actualizadoEn)
    {
        Id = id;
        CodigoUsuario = codigoUsuario;
        Nombre = nombre;
        WhatsappNormalizado = whatsappNormalizado;
        UsuarioWhatsapp = usuarioWhatsapp;
        Rol = rol;
        Estado = estado;
        Area = area;
        Empresa = empresa;
        EmpresaId = empresaId;
        Sede = sede;
        Cargo = cargo;
        Email = email;
        AntiguedadAnios = antiguedadAnios;
        IdiomaInterno = idioma;
        Tags = tags;
        PropiedadesDinamicas = propiedadesDinamicas;
        CreadoEn = creadoEn;
        ActualizadoEn = actualizadoEn;
    }

    /// <summary>Identificador tecnico (<c>u_&lt;guid&gt;</c>); es el que referencian el resto de contenedores (03 §3.1).</summary>
    public string Id { get; }

    /// <summary>
    /// Identificador secuencial y legible del maestro (03 §3.1.1). Unico e inmutable: acompana al
    /// usuario incluso cuando queda inactivo. Lo asigna el contador <c>seq_usuario</c>, nunca el cliente.
    /// </summary>
    public int CodigoUsuario { get; }

    public string Nombre { get; }

    public NumeroWhatsApp WhatsappNormalizado { get; }

    /// <summary>
    /// Identificacion por usuario de WhatsApp (I-08 §3.1.c). Opcional, solo se captura desde el portal;
    /// no se carga de archivo y todavia no participa en el enrutamiento (05, 06 §2).
    /// </summary>
    public string? UsuarioWhatsapp { get; }

    public RolUsuario Rol { get; }

    public EstadoRegistro Estado { get; }

    public string? Area { get; }

    public string? Empresa { get; }

    /// <summary>Codigo corto de la empresa en la plantilla oficial (<c>AL</c>, <c>GR</c>, ...); manda sobre <see cref="Empresa"/>.</summary>
    public string? EmpresaId { get; }

    public string? Sede { get; }

    public string? Cargo { get; }

    /// <summary>Correo normalizado en minusculas. Opcional; si viene, es unico entre usuarios activos (I-08 §3.1.g).</summary>
    public string? Email { get; }

    /// <summary>Antiguedad en anos tal cual la trae el archivo, sin redondear (I-08 §3, columna G).</summary>
    public decimal? AntiguedadAnios { get; }

    /// <summary>Idioma del participante (<c>es</c> | <c>en</c>), con <c>es</c> por defecto.</summary>
    public IdiomaConversacion IdiomaInterno { get; }

    public string Idioma => IdiomaInterno.Codigo;

    public IReadOnlyCollection<string> Tags { get; }

    public IReadOnlyDictionary<string, object?> PropiedadesDinamicas { get; }

    public DateTimeOffset CreadoEn { get; }

    public DateTimeOffset ActualizadoEn { get; }

    public bool EsAdministrativo => Rol is RolUsuario.Admin or RolUsuario.Visor;

    /// <summary>Forma legible del codigo secuencial (<c>U-000042</c>), para portal y reportes (03 §3.1).</summary>
    public string CodigoUsuarioLegible => FormatearCodigo(CodigoUsuario);

    public static Usuario Crear(
        string id,
        int codigoUsuario,
        string nombre,
        NumeroWhatsApp whatsappNormalizado,
        RolUsuario rol,
        EstadoRegistro estado,
        string? area,
        string? empresa,
        IEnumerable<string>? tags,
        IReadOnlyDictionary<string, object?>? propiedadesDinamicas,
        DateTimeOffset creadoEn,
        DateTimeOffset actualizadoEn,
        string? usuarioWhatsapp = null,
        string? empresaId = null,
        string? sede = null,
        string? cargo = null,
        string? email = null,
        decimal? antiguedadAnios = null,
        string? idioma = null)
    {
        var fechaCreacionUtc = creadoEn.ToUniversalTime();
        var fechaActualizacionUtc = actualizadoEn.ToUniversalTime();

        if (fechaActualizacionUtc < fechaCreacionUtc)
        {
            throw new DomainValidationException(
                "FECHA_ACTUALIZACION_INVALIDA",
                "La fecha de actualizacion no puede ser anterior a la fecha de creacion.");
        }

        if (codigoUsuario < 1)
        {
            // El codigo lo entrega el contador seq_usuario (03 §3.1.1): un 0 significa que alguien
            // construyo el usuario sin pasar por la reserva, y eso no debe llegar a persistencia.
            throw new DomainValidationException(
                "CODIGO_USUARIO_INVALIDO",
                "El codigo de usuario debe ser un entero positivo asignado por la secuencia.");
        }

        return new Usuario(
            DomainGuards.Required(id, nameof(id)),
            codigoUsuario,
            NormalizarNombre(DomainGuards.Required(nombre, nameof(nombre))),
            whatsappNormalizado,
            Opcional(usuarioWhatsapp),
            rol,
            estado,
            Opcional(area),
            Opcional(empresa),
            Opcional(empresaId),
            Opcional(sede),
            Opcional(cargo),
            NormalizarEmail(email),
            antiguedadAnios,
            CrearIdioma(idioma),
            NormalizeTags(tags),
            NormalizeProperties(propiedadesDinamicas),
            fechaCreacionUtc,
            fechaActualizacionUtc);
    }

    /// <summary>Forma legible del codigo secuencial (<c>U-000042</c>).</summary>
    public static string FormatearCodigo(int codigoUsuario)
        => "U-" + codigoUsuario.ToString("D6", CultureInfo.InvariantCulture);

    /// <summary>Indica si el idioma es uno de los soportados por la plantilla oficial (<c>es</c> | <c>en</c>).</summary>
    public static bool EsIdiomaSoportado(string? idioma)
        => IdiomaConversacion.TryCrear(idioma, out _);

    private static IdiomaConversacion CrearIdioma(string? idioma)
    {
        try
        {
            return IdiomaConversacion.DesdeFronteraHistorica(idioma);
        }
        catch (DomainValidationException)
        {
            throw new DomainValidationException(
                "IDIOMA_NO_SOPORTADO",
                "El idioma del usuario debe ser 'es' o 'en'.");
        }
    }

    private static string NormalizarNombre(string nombre)
    {
        // La plantilla oficial trae nombres con espacios dobles; se colapsan sin re-capitalizar (I-08 §3).
        var partes = nombre.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(' ', partes);
    }

    private static string? NormalizarEmail(string? email)
    {
        var valor = Opcional(email);
        return valor?.ToLowerInvariant();
    }

    private static string? Opcional(string? valor)
        => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static IReadOnlyCollection<string> NormalizeTags(IEnumerable<string>? tags)
    {
        if (tags is null)
        {
            return Array.Empty<string>();
        }

        return tags
            .Select(tag => tag.Trim())
            .Where(tag => tag.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyDictionary<string, object?> NormalizeProperties(
        IReadOnlyDictionary<string, object?>? properties)
    {
        if (properties is null || properties.Count == 0)
        {
            return new Dictionary<string, object?>();
        }

        return properties
            .Where(property => !string.IsNullOrWhiteSpace(property.Key))
            .ToDictionary(
                property => property.Key.Trim(),
                property => property.Value,
                StringComparer.Ordinal);
    }
}
