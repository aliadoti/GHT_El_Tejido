using ElTejido.Application.Usuarios;
using ElTejido.Domain.Common;
using ElTejido.Domain.Usuarios;

namespace ElTejido.Application.Configuracion;

public interface IServicioGestionUsuarios
{
    Task<IReadOnlyCollection<Usuario>> BuscarUsuariosAsync(
        FiltroUsuarios filtro,
        CancellationToken cancellationToken);

    Task<Usuario> ObtenerUsuarioAsync(string id, CancellationToken cancellationToken);

    Task<Usuario> CrearUsuarioAsync(
        SolicitudCrearUsuario solicitud,
        CancellationToken cancellationToken);

    Task<Usuario> ActualizarUsuarioAsync(
        string id,
        SolicitudActualizarUsuario solicitud,
        CancellationToken cancellationToken);

    Task<Usuario> CambiarEstadoUsuarioAsync(
        string id,
        EstadoRegistro estado,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Tag>> BuscarTagsAsync(
        FiltroTags filtro,
        CancellationToken cancellationToken);

    Task<Tag> ObtenerTagAsync(string id, CancellationToken cancellationToken);

    Task<Tag> CrearTagAsync(SolicitudCrearTag solicitud, CancellationToken cancellationToken);

    Task<Tag> ActualizarTagAsync(
        string id,
        SolicitudActualizarTag solicitud,
        CancellationToken cancellationToken);

    Task<Tag> CambiarEstadoTagAsync(
        string id,
        EstadoRegistro estado,
        CancellationToken cancellationToken);
}

public sealed record SolicitudCrearUsuario(
    string Nombre,
    string Numero,
    RolUsuario Rol,
    EstadoRegistro Estado,
    string Area,
    string Empresa,
    IEnumerable<string>? Tags,
    IReadOnlyDictionary<string, object?>? PropiedadesDinamicas);

public sealed record SolicitudActualizarUsuario(
    string? Nombre,
    string? Numero,
    RolUsuario? Rol,
    EstadoRegistro? Estado,
    string? Area,
    string? Empresa,
    IEnumerable<string>? Tags,
    IReadOnlyDictionary<string, object?>? PropiedadesDinamicas);

public sealed record SolicitudCrearTag(
    string Nombre,
    string TipoTag,
    string? Descripcion,
    EstadoRegistro Estado);

public sealed record SolicitudActualizarTag(
    string? Nombre,
    string? TipoTag,
    string? Descripcion,
    EstadoRegistro? Estado);
