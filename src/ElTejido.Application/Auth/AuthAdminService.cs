using ElTejido.Application.Common;
using ElTejido.Application.Seguridad;
using ElTejido.Application.Usuarios;
using ElTejido.Domain.Common;
using ElTejido.Domain.Identidad;
using ElTejido.Domain.Seguridad;
using ElTejido.Domain.Usuarios;

namespace ElTejido.Application.Auth;

/// <summary>
/// Orquesta el login admin por OTP de WhatsApp (06 §4). Garantiza respuestas neutrales
/// (REQ §10.3.10): solo administradores activos reciben codigo (REQ §10.3.3), el codigo es de un
/// solo uso, con expiracion e intentos limitados (REQ §10.3.4-7) y nunca se guarda en claro
/// (REQ §10.3.8). Cada solicitud/verificacion registra un <c>LogSeguridad</c> sin el codigo.
/// </summary>
public sealed class AuthAdminService : IAuthAdminService
{
    private readonly INormalizadorNumero _normalizador;
    private readonly IRepositorioUsuarios _usuarios;
    private readonly IRepositorioCodigosAuth _codigos;
    private readonly IRepositorioLogSeguridad _logSeguridad;
    private readonly ISecretProvider _secretos;
    private readonly IHasherOtp _hasher;
    private readonly IGeneradorCodigoOtp _generador;
    private readonly INotificadorOtp _notificador;
    private readonly ILimitadorOtp _limitador;
    private readonly IServicioSesion _sesion;
    private readonly IProveedorCorrelacion _correlacion;
    private readonly OpcionesAuth _opciones;
    private readonly TimeProvider _tiempo;

    public AuthAdminService(
        INormalizadorNumero normalizador,
        IRepositorioUsuarios usuarios,
        IRepositorioCodigosAuth codigos,
        IRepositorioLogSeguridad logSeguridad,
        ISecretProvider secretos,
        IHasherOtp hasher,
        IGeneradorCodigoOtp generador,
        INotificadorOtp notificador,
        ILimitadorOtp limitador,
        IServicioSesion sesion,
        IProveedorCorrelacion correlacion,
        OpcionesAuth opciones,
        TimeProvider tiempo)
    {
        _normalizador = normalizador;
        _usuarios = usuarios;
        _codigos = codigos;
        _logSeguridad = logSeguridad;
        _secretos = secretos;
        _hasher = hasher;
        _generador = generador;
        _notificador = notificador;
        _limitador = limitador;
        _sesion = sesion;
        _correlacion = correlacion;
        _opciones = opciones;
        _tiempo = tiempo;
    }

    public async Task SolicitarCodigoAsync(string numeroCrudo, CancellationToken cancellationToken)
    {
        if (!_normalizador.TryNormalizar(numeroCrudo, out var numeroOpt) || numeroOpt is null)
        {
            await RegistrarAsync(TipoEventoSeguridad.SolicitudOtp, "ignorado", null, "numero invalido", null, cancellationToken);
            return;
        }

        var numero = numeroOpt;
        var usuario = await _usuarios.ObtenerUsuarioPorNumeroAsync(numero, cancellationToken);

        if (usuario is null || !usuario.EsAdministrativo || usuario.Estado != EstadoRegistro.Activo)
        {
            // Termina silenciosamente; el cliente ya recibe la respuesta neutral (06 §4.2 paso 3).
            await RegistrarAsync(TipoEventoSeguridad.SolicitudOtp, "ignorado", numero, "sin admin valido", usuario?.Id, cancellationToken);
            return;
        }

        if (!await _limitador.RegistrarYPermitirAsync(numero, cancellationToken))
        {
            await RegistrarAsync(TipoEventoSeguridad.RateLimit, "limitado", numero, "exceso solicitudes otp", usuario.Id, cancellationToken);
            return;
        }

        var codigo = _generador.Generar(_opciones.OtpLongitud);
        var pepper = await _secretos.ObtenerSecretoAsync(NombresSecretos.OtpSalt, cancellationToken);
        var hash = _hasher.Hashear(codigo, pepper);

        var ahora = _tiempo.GetUtcNow();
        var ttlSegundos = _opciones.OtpTtlMinutos * 60;
        var codigoAuth = CodigoAuthAdmin.Crear(
            "cod_" + Guid.NewGuid().ToString("N"),
            usuario.Id,
            numero,
            hash,
            ahora.AddMinutes(_opciones.OtpTtlMinutos),
            _opciones.OtpIntentos,
            usado: false,
            ahora,
            ttlSegundos);

        await _codigos.GuardarAsync(codigoAuth, cancellationToken);
        await _notificador.EnviarCodigoAsync(numero, codigo, cancellationToken);
        await RegistrarAsync(TipoEventoSeguridad.SolicitudOtp, "enviado", numero, null, usuario.Id, cancellationToken);
    }

    public async Task<SesionEmitida?> VerificarCodigoAsync(
        string numeroCrudo,
        string codigo,
        CancellationToken cancellationToken)
    {
        if (!_normalizador.TryNormalizar(numeroCrudo, out var numeroOpt) || numeroOpt is null)
        {
            await RegistrarAsync(TipoEventoSeguridad.LoginFallido, "fallido", null, "numero invalido", null, cancellationToken);
            return null;
        }

        var numero = numeroOpt;
        var ahora = _tiempo.GetUtcNow();
        var codigoAuth = await _codigos.ObtenerVigenteMasRecienteAsync(numero, cancellationToken);

        if (codigoAuth is null || !codigoAuth.EsVigente(ahora))
        {
            await RegistrarAsync(TipoEventoSeguridad.LoginFallido, "fallido", numero, "sin codigo vigente", codigoAuth?.UsuarioId, cancellationToken);
            return null;
        }

        var pepper = await _secretos.ObtenerSecretoAsync(NombresSecretos.OtpSalt, cancellationToken);
        if (!_hasher.Verificar(codigo, pepper, codigoAuth.HashCodigo))
        {
            await _codigos.GuardarAsync(codigoAuth.ConIntentoConsumido(), cancellationToken);
            await RegistrarAsync(TipoEventoSeguridad.LoginFallido, "fallido", numero, "codigo invalido", codigoAuth.UsuarioId, cancellationToken);
            return null;
        }

        await _codigos.GuardarAsync(codigoAuth.ComoUsado(), cancellationToken);

        var usuario = await _usuarios.ObtenerUsuarioPorIdAsync(codigoAuth.UsuarioId, cancellationToken);
        if (usuario is null || !usuario.EsAdministrativo || usuario.Estado != EstadoRegistro.Activo)
        {
            await RegistrarAsync(TipoEventoSeguridad.LoginFallido, "fallido", numero, "usuario no valido", codigoAuth.UsuarioId, cancellationToken);
            return null;
        }

        var sesion = await _sesion.EmitirAsync(usuario, cancellationToken);
        await RegistrarAsync(TipoEventoSeguridad.LoginExitoso, "exitoso", numero, null, usuario.Id, cancellationToken);
        return sesion;
    }

    private async Task RegistrarAsync(
        TipoEventoSeguridad tipo,
        string resultado,
        NumeroWhatsApp? numero,
        string? detalle,
        string? usuarioId,
        CancellationToken cancellationToken)
    {
        var log = LogSeguridad.Crear(
            "log_" + Guid.NewGuid().ToString("N"),
            tipo,
            usuarioId,
            numero?.Valor,
            resultado,
            detalle,
            _correlacion.CorrelationIdActual,
            _tiempo.GetUtcNow());

        await _logSeguridad.RegistrarAsync(log, cancellationToken);
    }
}
