using ElTejido.Domain.Campanas;

namespace ElTejido.Application.Campanas;

public sealed class FiltroCampanias
{
    public FiltroCampanias(EstadoCampania? estado = null, string? busqueda = null)
    {
        Estado = estado;
        Busqueda = string.IsNullOrWhiteSpace(busqueda) ? null : busqueda.Trim();
    }

    public EstadoCampania? Estado { get; }

    public string? Busqueda { get; }
}
