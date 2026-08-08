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

    /// <summary>
    /// Historico de titulares de un numero: el activo y los inactivos, por <c>creadoEn</c>
    /// (I-08 §3.1.f, 11 §Usuarios). Es el unico camino para ver inactivos desde el portal.
    /// </summary>
    Task<IReadOnlyCollection<Usuario>> ListarUsuariosPorNumeroAsync(
        string numero,
        CancellationToken cancellationToken);

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

    /// <summary>
    /// Reasignacion manual de un numero a otra persona (I-08 §4.4, 04 §5.1). Inactiva al titular
    /// <paramref name="id"/> conservando su numero e historial y <b>crea</b> un usuario nuevo (nuevo
    /// <c>id</c> y <c>codigoUsuario</c>) con el mismo numero, sin heredar rol, tags ni historial.
    /// Los dos pasos van ordenados por la unique key; si el alta falla, se revierte la inactivacion.
    /// </summary>
    Task<ResultadoReasignacionNumero> ReasignarNumeroAsync(
        string id,
        SolicitudReasignarNumero solicitud,
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

/// <summary>
/// Alta individual (04 §5.1). Obligatorios: <c>Nombre</c> y <c>Numero</c>; <c>Area</c> y
/// <c>Empresa</c> dejaron de serlo con la plantilla oficial (I-08 §3.1.h). Los campos del maestro se
/// agregan al final de forma <b>aditiva</b>: un cliente viejo sigue compilando y funcionando.
/// <c>codigoUsuario</c> no esta aqui a proposito: lo asigna el servidor (03 §3.1.1).
/// </summary>
public sealed record SolicitudCrearUsuario(
    string Nombre,
    string Numero,
    RolUsuario Rol,
    EstadoRegistro Estado,
    string? Area,
    string? Empresa,
    IEnumerable<string>? Tags,
    IReadOnlyDictionary<string, object?>? PropiedadesDinamicas,
    string? Email = null,
    string? EmpresaId = null,
    string? Sede = null,
    string? Cargo = null,
    decimal? AntiguedadAnios = null,
    string? Idioma = null,
    string? UsuarioWhatsapp = null);

/// <summary>
/// Edicion individual (04 §5.1): un campo <c>null</c> conserva el valor actual. No cambia
/// <c>codigoUsuario</c> (es inmutable).
/// </summary>
public sealed record SolicitudActualizarUsuario(
    string? Nombre,
    string? Numero,
    RolUsuario? Rol,
    EstadoRegistro? Estado,
    string? Area,
    string? Empresa,
    IEnumerable<string>? Tags,
    IReadOnlyDictionary<string, object?>? PropiedadesDinamicas,
    string? Email = null,
    string? EmpresaId = null,
    string? Sede = null,
    string? Cargo = null,
    decimal? AntiguedadAnios = null,
    string? Idioma = null,
    string? UsuarioWhatsapp = null);

/// <summary>Datos del nuevo titular en una reasignacion manual de numero (I-08 §4.4).</summary>
public sealed record SolicitudReasignarNumero(
    string Nombre,
    string? Email = null,
    string? EmpresaId = null,
    string? Sede = null,
    string? Cargo = null,
    decimal? AntiguedadAnios = null,
    string? Idioma = null,
    string? UsuarioWhatsapp = null);

/// <summary>
/// Resultado de una reasignacion: el usuario nuevo y la identidad del titular anterior, que queda
/// inactivo conservando su historial (04 §5.1).
/// </summary>
public sealed record ResultadoReasignacionNumero(
    Usuario Nuevo,
    string UsuarioIdAnterior,
    int CodigoUsuarioAnterior);

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
