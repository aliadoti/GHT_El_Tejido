namespace ElTejido.Infrastructure.Usuarios;

internal sealed record FiltroUsuariosCosmos(
    string? WhatsappNormalizado,
    string? Rol,
    string? Estado,
    string? Area,
    string? Empresa,
    IReadOnlyCollection<string> Tags,
    string? Busqueda,
    string? EmpresaId = null,
    string? Sede = null,
    string? Idioma = null,
    /// <summary>P-34 §4.1: acota la consulta a un bloque de ids dentro de la misma particion.</summary>
    IReadOnlyCollection<string>? Ids = null);
