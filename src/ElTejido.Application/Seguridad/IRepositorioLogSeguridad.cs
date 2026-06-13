using ElTejido.Domain.Seguridad;

namespace ElTejido.Application.Seguridad;

/// <summary>
/// Puerto append-only del contenedor Cosmos <c>security</c> para LogSeguridad.
/// Cubre 03 §3.15, 10 §6.4 y REQ §30.
/// </summary>
public interface IRepositorioLogSeguridad
{
    Task RegistrarAsync(LogSeguridad log, CancellationToken cancellationToken);
}
