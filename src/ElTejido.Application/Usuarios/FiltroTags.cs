using ElTejido.Domain.Common;

namespace ElTejido.Application.Usuarios;

public sealed class FiltroTags
{
    public FiltroTags(string? tipoTag = null, EstadoRegistro? estado = null)
    {
        TipoTag = string.IsNullOrWhiteSpace(tipoTag) ? null : tipoTag.Trim();
        Estado = estado;
    }

    public string? TipoTag { get; }

    public EstadoRegistro? Estado { get; }
}
