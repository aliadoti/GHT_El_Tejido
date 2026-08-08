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

    public Task<IReadOnlyCollection<Usuario>> ListarUsuariosPorNumeroAsync(
        string numero,
        CancellationToken cancellationToken)
        => _usuarios.ListarUsuariosPorNumeroAsync(_normalizador.Normalizar(numero), cancellationToken);

    public async Task<Usuario> CrearUsuarioAsync(
        SolicitudCrearUsuario solicitud,
        CancellationToken cancellationToken)
    {
        var numero = _normalizador.Normalizar(solicitud.Numero);
        var existente = await _usuarios.ObtenerUsuarioPorNumeroAsync(numero, cancellationToken);
        if (existente is not null)
        {
            throw new ErrorConflicto("Ya existe un usuario activo con ese numero de WhatsApp.");
        }

        await AsegurarEmailDisponibleAsync(solicitud.Email, usuarioIdPropio: null, cancellationToken);

        var ahora = _tiempo.GetUtcNow();
        // El codigo legible lo entrega la secuencia del maestro, nunca el cliente (03 §3.1.1).
        var codigoUsuario = await _usuarios.ReservarCodigosUsuarioAsync(1, cancellationToken);
        var usuario = Usuario.Crear(
            "u_" + Guid.NewGuid().ToString("N"),
            codigoUsuario,
            solicitud.Nombre,
            numero,
            solicitud.Rol,
            solicitud.Estado,
            solicitud.Area,
            solicitud.Empresa,
            solicitud.Tags,
            solicitud.PropiedadesDinamicas,
            ahora,
            ahora,
            solicitud.UsuarioWhatsapp,
            solicitud.EmpresaId,
            solicitud.Sede,
            solicitud.Cargo,
            solicitud.Email,
            solicitud.AntiguedadAnios,
            solicitud.Idioma);

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
        await AsegurarEmailDisponibleAsync(solicitud.Email, existente.Id, cancellationToken);
        var ahora = _tiempo.GetUtcNow();

        // codigoUsuario no cambia nunca (03 §3.1.1). El resto: null conserva, valor presente manda.
        var actualizado = Usuario.Crear(
            existente.Id,
            existente.CodigoUsuario,
            ResolverTexto(solicitud.Nombre, existente.Nombre),
            numero,
            solicitud.Rol ?? existente.Rol,
            solicitud.Estado ?? existente.Estado,
            ResolverOpcional(solicitud.Area, existente.Area),
            ResolverOpcional(solicitud.Empresa, existente.Empresa),
            solicitud.Tags ?? existente.Tags,
            solicitud.PropiedadesDinamicas ?? existente.PropiedadesDinamicas,
            existente.CreadoEn,
            ahora,
            ResolverOpcional(solicitud.UsuarioWhatsapp, existente.UsuarioWhatsapp),
            ResolverOpcional(solicitud.EmpresaId, existente.EmpresaId),
            ResolverOpcional(solicitud.Sede, existente.Sede),
            ResolverOpcional(solicitud.Cargo, existente.Cargo),
            ResolverOpcional(solicitud.Email, existente.Email),
            solicitud.AntiguedadAnios ?? existente.AntiguedadAnios,
            ResolverOpcional(solicitud.Idioma, existente.Idioma));

        await _usuarios.GuardarUsuarioAsync(actualizado, cancellationToken);
        return actualizado;
    }

    public async Task<ResultadoReasignacionNumero> ReasignarNumeroAsync(
        string id,
        SolicitudReasignarNumero solicitud,
        CancellationToken cancellationToken)
    {
        var anterior = await ObtenerUsuarioAsync(id, cancellationToken);
        if (anterior.Estado != EstadoRegistro.Activo)
        {
            throw new ErrorConflicto("Solo se puede reasignar el numero de un usuario activo.");
        }

        await AsegurarEmailDisponibleAsync(solicitud.Email, usuarioIdPropio: null, cancellationToken);

        var ahora = _tiempo.GetUtcNow();

        // Orden obligatorio (03 §3.1): primero inactivar —su claveUnicidad pasa de wa|<numero> a
        // hist|<id>— y solo entonces crear al nuevo. Al reves, la unique key rechaza la operacion.
        await _usuarios.GuardarUsuarioAsync(Con(anterior, EstadoRegistro.Inactivo, ahora), cancellationToken);

        var codigoUsuario = await _usuarios.ReservarCodigosUsuarioAsync(1, cancellationToken);
        var nuevo = Usuario.Crear(
            "u_" + Guid.NewGuid().ToString("N"),
            codigoUsuario,
            solicitud.Nombre,
            anterior.WhatsappNormalizado,
            RolUsuario.Participante,
            EstadoRegistro.Activo,
            area: null,
            empresa: null,
            // El nuevo titular no hereda tags, rol ni historial (I-08 §4.4).
            tags: null,
            propiedadesDinamicas: null,
            ahora,
            ahora,
            solicitud.UsuarioWhatsapp,
            solicitud.EmpresaId,
            solicitud.Sede,
            solicitud.Cargo,
            solicitud.Email,
            solicitud.AntiguedadAnios,
            solicitud.Idioma);

        try
        {
            await _usuarios.GuardarUsuarioAsync(nuevo, cancellationToken);
        }
        catch (Exception)
        {
            // Compensacion: el numero no puede quedarse sin titular activo por un fallo a medias.
            await _usuarios.GuardarUsuarioAsync(anterior, cancellationToken);
            throw;
        }

        return new ResultadoReasignacionNumero(nuevo, anterior.Id, anterior.CodigoUsuario);
    }

    private static Usuario Con(Usuario usuario, EstadoRegistro estado, DateTimeOffset ahora)
        => Usuario.Crear(
            usuario.Id,
            usuario.CodigoUsuario,
            usuario.Nombre,
            usuario.WhatsappNormalizado,
            usuario.Rol,
            estado,
            usuario.Area,
            usuario.Empresa,
            usuario.Tags,
            usuario.PropiedadesDinamicas,
            usuario.CreadoEn,
            ahora,
            usuario.UsuarioWhatsapp,
            usuario.EmpresaId,
            usuario.Sede,
            usuario.Cargo,
            usuario.Email,
            usuario.AntiguedadAnios,
            usuario.Idioma);

    /// <summary>
    /// El email, si viene, es unico <b>entre usuarios activos</b> (I-08 §3.1.g). Es nullable, asi que
    /// no admite unique key en Cosmos: la validacion vive aqui. El maestro es pequeno, de modo que una
    /// sola consulta de activos basta.
    /// </summary>
    private async Task AsegurarEmailDisponibleAsync(
        string? email,
        string? usuarioIdPropio,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        var normalizado = email.Trim();
        var activos = await _usuarios.BuscarUsuariosAsync(
            new FiltroUsuarios(estado: EstadoRegistro.Activo),
            cancellationToken);

        var enUso = activos.Any(u =>
            u.Id != usuarioIdPropio
            && u.Email is not null
            && string.Equals(u.Email, normalizado, StringComparison.OrdinalIgnoreCase));

        if (enUso)
        {
            throw new ErrorConflicto("Ya existe un usuario activo con ese email.");
        }
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

    /// <summary>Campos opcionales del maestro (<c>area</c>, <c>empresa</c>): ausente conserva, presente manda.</summary>
    private static string? ResolverOpcional(string? valor, string? actual)
        => valor is null ? actual : valor;
}
