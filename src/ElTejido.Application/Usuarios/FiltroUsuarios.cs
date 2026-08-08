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
        string? busqueda = null,
        string? empresaId = null,
        string? sede = null,
        string? idioma = null)
    {
        Rol = rol;
        Estado = estado;
        Area = NormalizeOptional(area);
        Empresa = NormalizeOptional(empresa);
        Tags = NormalizeTags(tags);
        Busqueda = NormalizeOptional(busqueda);
        EmpresaId = NormalizeOptional(empresaId);
        Sede = NormalizeOptional(sede);
        Idioma = NormalizeOptional(idioma)?.ToLowerInvariant();
    }

    public RolUsuario? Rol { get; }

    public EstadoRegistro? Estado { get; }

    public string? Area { get; }

    public string? Empresa { get; }

    public IReadOnlyCollection<string> Tags { get; }

    /// <summary>Texto libre: nombre, numero, email o <c>codigoUsuario</c> (04 §5.1).</summary>
    public string? Busqueda { get; }

    /// <summary>Codigo corto de empresa de la plantilla oficial (I-08 §3, columna B).</summary>
    public string? EmpresaId { get; }

    public string? Sede { get; }

    public string? Idioma { get; }

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
