using ElTejido.Domain.Identidad;
using ElTejido.Domain.Usuarios;

namespace ElTejido.Application.Usuarios;

/// <summary>
/// Puerto de persistencia del contenedor Cosmos <c>users</c> para Usuario y Tag.
/// Cubre REQ §12, §13, §26.3 y ARQ §8-§9 sin acoplar la aplicacion a Cosmos.
/// </summary>
public interface IRepositorioUsuarios
{
    Task GuardarUsuarioAsync(Usuario usuario, CancellationToken cancellationToken);

    Task<Usuario?> ObtenerUsuarioPorIdAsync(string id, CancellationToken cancellationToken);

    Task<Usuario?> ObtenerUsuarioPorNumeroAsync(NumeroWhatsApp numero, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Usuario>> BuscarUsuariosAsync(
        FiltroUsuarios filtro,
        CancellationToken cancellationToken);

    Task GuardarTagAsync(Tag tag, CancellationToken cancellationToken);

    Task<Tag?> ObtenerTagPorIdAsync(string id, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Tag>> BuscarTagsAsync(FiltroTags filtro, CancellationToken cancellationToken);
}
