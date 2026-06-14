using ElTejido.Application.Common;
using ElTejido.Application.Usuarios;
using ElTejido.Domain.Common;
using ElTejido.Domain.Identidad;
using ElTejido.Domain.Usuarios;

namespace ElTejido.Application.Configuracion;

/// <summary>
/// Casos de uso administrativos para el catalogo de usuarios y tags (04 secciones 5.1-5.2, 07 seccion 1).
/// Mantiene la normalizacion E.164 y unicidad de WhatsApp fuera del edge HTTP.
/// </summary>
public sealed class ServicioGestionUsuarios : IServicioGestionUsuarios
{
    private readonly IRepositorioUsuarios _usuarios;
    private readonly INormalizadorNumero _normalizador;
    private readonly TimeProvider _tiempo;

    public ServicioGestionUsuarios(
        IRepositorioUsuarios usuarios,
        INormalizadorNumero normalizador,
        TimeProvider tiempo)
    {
        _usuarios = usuarios;
        _normalizador = normalizador;
        _tiempo = tiempo;
    }

    public Task<IReadOnlyCollection<Usuario>> BuscarUsuariosAsync(
        FiltroUsuarios filtro,
        CancellationToken cancellationToken)
        => _usuarios.BuscarUsuariosAsync(filtro, cancellationToken);

    public async Task<Usuario> ObtenerUsuarioAsync(string id, CancellationToken cancellationToken)
    {
        var usuario = await _usuarios.ObtenerUsuarioPorIdAsync(RequerirId(id), cancellationToken);
        return usuario ?? throw new ErrorNoEncontrado("El usuario no existe.");
    }

    public async Task<Usuario> CrearUsuarioAsync(
        SolicitudCrearUsuario solicitud,
        CancellationToken cancellationToken)
    {
        var numero = _normalizador.Normalizar(solicitud.Numero);
        var existente = await _usuarios.ObtenerUsuarioPorNumeroAsync(numero, cancellationToken);
        if (existente is not null)
        {
            throw new ErrorConflicto("Ya existe un usuario con ese numero de WhatsApp.");
        }

        var ahora = _tiempo.GetUtcNow();
        var usuario = Usuario.Crear(
            "u_" + Guid.NewGuid().ToString("N"),
            solicitud.Nombre,
            numero,
            solicitud.Rol,
            solicitud.Estado,
            solicitud.Area,
            solicitud.Empresa,
            solicitud.Tags,
            solicitud.PropiedadesDinamicas,
            ahora,
            ahora);

        await _usuarios.GuardarUsuarioAsync(usuario, cancellationToken);
        return usuario;
    }

    public async Task<Usuario> ActualizarUsuarioAsync(
        string id,
        SolicitudActualizarUsuario solicitud,
        CancellationToken cancellationToken)
    {
        var existente = await ObtenerUsuarioAsync(id, cancellationToken);
        var numero = await ResolverNumeroAsync(existente, solicitud.Numero, cancellationToken);
        var ahora = _tiempo.GetUtcNow();

        var actualizado = Usuario.Crear(
            existente.Id,
            ResolverTexto(solicitud.Nombre, existente.Nombre),
            numero,
            solicitud.Rol ?? existente.Rol,
            solicitud.Estado ?? existente.Estado,
            ResolverTexto(solicitud.Area, existente.Area),
            ResolverTexto(solicitud.Empresa, existente.Empresa),
            solicitud.Tags ?? existente.Tags,
            solicitud.PropiedadesDinamicas ?? existente.PropiedadesDinamicas,
            existente.CreadoEn,
            ahora);

        await _usuarios.GuardarUsuarioAsync(actualizado, cancellationToken);
        return actualizado;
    }

    public Task<Usuario> CambiarEstadoUsuarioAsync(
        string id,
        EstadoRegistro estado,
        CancellationToken cancellationToken)
        => ActualizarUsuarioAsync(
            id,
            new SolicitudActualizarUsuario(null, null, null, estado, null, null, null, null),
            cancellationToken);

    public Task<IReadOnlyCollection<Tag>> BuscarTagsAsync(
        FiltroTags filtro,
        CancellationToken cancellationToken)
        => _usuarios.BuscarTagsAsync(filtro, cancellationToken);

    public async Task<Tag> ObtenerTagAsync(string id, CancellationToken cancellationToken)
    {
        var tag = await _usuarios.ObtenerTagPorIdAsync(RequerirId(id), cancellationToken);
        return tag ?? throw new ErrorNoEncontrado("El tag no existe.");
    }

    public async Task<Tag> CrearTagAsync(SolicitudCrearTag solicitud, CancellationToken cancellationToken)
    {
        var tag = Tag.Crear(
            "t_" + Guid.NewGuid().ToString("N"),
            solicitud.Nombre,
            solicitud.TipoTag,
            solicitud.Descripcion,
            solicitud.Estado,
            _tiempo.GetUtcNow());

        await _usuarios.GuardarTagAsync(tag, cancellationToken);
        return tag;
    }

    public async Task<Tag> ActualizarTagAsync(
        string id,
        SolicitudActualizarTag solicitud,
        CancellationToken cancellationToken)
    {
        var existente = await ObtenerTagAsync(id, cancellationToken);
        var actualizado = Tag.Crear(
            existente.Id,
            ResolverTexto(solicitud.Nombre, existente.Nombre),
            ResolverTexto(solicitud.TipoTag, existente.TipoTag),
            solicitud.Descripcion ?? existente.Descripcion,
            solicitud.Estado ?? existente.Estado,
            existente.CreadoEn);

        await _usuarios.GuardarTagAsync(actualizado, cancellationToken);
        return actualizado;
    }

    public Task<Tag> CambiarEstadoTagAsync(
        string id,
        EstadoRegistro estado,
        CancellationToken cancellationToken)
        => ActualizarTagAsync(
            id,
            new SolicitudActualizarTag(null, null, null, estado),
            cancellationToken);

    private static string RequerirId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ErrorValidacion(
                "El id es obligatorio.",
                new[] { new DetalleError("id", "obligatorio") });
        }

        return id.Trim();
    }

    private async Task<NumeroWhatsApp> ResolverNumeroAsync(
        Usuario existente,
        string? numeroCrudo,
        CancellationToken cancellationToken)
    {
        if (numeroCrudo is null)
        {
            return existente.WhatsappNormalizado;
        }

        var numero = _normalizador.Normalizar(numeroCrudo);
        if (numero.Valor == existente.WhatsappNormalizado.Valor)
        {
            return numero;
        }

        var usuarioConNumero = await _usuarios.ObtenerUsuarioPorNumeroAsync(numero, cancellationToken);
        if (usuarioConNumero is not null && usuarioConNumero.Id != existente.Id)
        {
            throw new ErrorConflicto("Ya existe un usuario con ese numero de WhatsApp.");
        }

        return numero;
    }

    private static string ResolverTexto(string? valor, string actual)
        => valor is null ? actual : valor;
}
