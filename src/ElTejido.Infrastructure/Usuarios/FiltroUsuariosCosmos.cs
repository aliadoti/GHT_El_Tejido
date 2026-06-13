namespace ElTejido.Infrastructure.Usuarios;

internal sealed record FiltroUsuariosCosmos(
    string? WhatsappNormalizado,
    string? Rol,
    string? Estado,
    string? Area,
    string? Empresa,
    IReadOnlyCollection<string> Tags,
    string? Busqueda);
