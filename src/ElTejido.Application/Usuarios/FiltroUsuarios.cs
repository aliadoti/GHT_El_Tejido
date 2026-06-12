using ElTejido.Domain.Common;
using ElTejido.Domain.Usuarios;

namespace ElTejido.Application.Usuarios;

public sealed class FiltroUsuarios
{
    public FiltroUsuarios(
        RolUsuario? rol = null,
        EstadoRegistro? estado = null,
        string? area = null,
        string? empresa = null,
        IEnumerable<string>? tags = null,
        string? busqueda = null)
    {
        Rol = rol;
        Estado = estado;
        Area = NormalizeOptional(area);
        Empresa = NormalizeOptional(empresa);
        Tags = NormalizeTags(tags);
        Busqueda = NormalizeOptional(busqueda);
    }

    public RolUsuario? Rol { get; }

    public EstadoRegistro? Estado { get; }

    public string? Area { get; }

    public string? Empresa { get; }

    public IReadOnlyCollection<string> Tags { get; }

    public string? Busqueda { get; }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

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
}
