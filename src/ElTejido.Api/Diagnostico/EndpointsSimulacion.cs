using ElTejido.Application.Auth;
using ElTejido.Application.Common;
using ElTejido.Application.Seguridad;
using ElTejido.Application.Usuarios;
using ElTejido.Domain.Common;
using ElTejido.Domain.Identidad;
using ElTejido.Domain.Seguridad;
using ElTejido.Domain.Usuarios;

namespace ElTejido.Api.Diagnostico;

/// <summary>
/// Utilidades de simulacion local para pruebas humanas. Solo se mapean en Development desde
/// <c>Program.cs</c>; no forman parte del contrato productivo ni reemplazan WhatsApp real.
/// </summary>
internal static class EndpointsSimulacion
{
    public static IEndpointRouteBuilder MapearEndpointsSimulacion(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/diagnostico/simulacion");

        grupo.MapPost("/admin-inicial", CrearAdminInicialAsync);
        grupo.MapPost("/otp-admin", CrearOtpAdminAsync);

        return app;
    }

    private static async Task<IResult> CrearAdminInicialAsync(
        AdminInicialRequest request,
        HttpContext contexto,
        INormalizadorNumero normalizador,
        TimeProvider tiempo,
        CancellationToken ct)
    {
        var usuarios = Resolver<IRepositorioUsuarios>(contexto);
        var numero = normalizador.Normalizar(Requerir(request.Numero, "numero"));
        var ahora = tiempo.GetUtcNow();
        var existente = await usuarios.ObtenerUsuarioPorNumeroAsync(numero, ct);

        var admin = Usuario.Crear(
            existente?.Id ?? "u_admin_" + Guid.NewGuid().ToString("N"),
            string.IsNullOrWhiteSpace(request.Nombre) ? "Administrador prueba" : request.Nombre.Trim(),
            numero,
            RolUsuario.Admin,
            EstadoRegistro.Activo,
            string.IsNullOrWhiteSpace(request.Area) ? "Administracion" : request.Area.Trim(),
            string.IsNullOrWhiteSpace(request.Empresa) ? "GHT" : request.Empresa.Trim(),
            request.Tags,
            null,
            existente?.CreadoEn ?? ahora,
            ahora);

        await usuarios.GuardarUsuarioAsync(admin, ct);
        return Results.Ok(new
        {
            admin.Id,
            admin.Nombre,
            whatsappNormalizado = admin.WhatsappNormalizado.Valor,
            rol = "admin",
            estado = "activo",
        });
    }

    private static async Task<IResult> CrearOtpAdminAsync(
        OtpAdminRequest request,
        HttpContext contexto,
        INormalizadorNumero normalizador,
        IHasherOtp hasher,
        ISecretProvider secretos,
        OpcionesAuth opciones,
        TimeProvider tiempo,
        CancellationToken ct)
    {
        var usuarios = Resolver<IRepositorioUsuarios>(contexto);
        var codigos = Resolver<IRepositorioCodigosAuth>(contexto);
        var numero = normalizador.Normalizar(Requerir(request.Numero, "numero"));
        var usuario = await usuarios.ObtenerUsuarioPorNumeroAsync(numero, ct);

        if (usuario is null || usuario.Rol != RolUsuario.Admin || usuario.Estado != EstadoRegistro.Activo)
        {
            throw new ErrorValidacion(
                "El numero no corresponde a un administrador activo.",
                new[] { new DetalleError("numero", "admin_no_activo") });
        }

        var codigo = string.IsNullOrWhiteSpace(request.Codigo)
            ? "123456"
            : request.Codigo.Trim();
        if (codigo.Length != opciones.OtpLongitud || !codigo.All(char.IsDigit))
        {
            throw new ErrorValidacion(
                $"El codigo debe tener {opciones.OtpLongitud} digitos.",
                new[] { new DetalleError("codigo", "formato") });
        }

        string pepper;
        try
        {
            pepper = await secretos.ObtenerSecretoAsync(NombresSecretos.OtpSalt, ct);
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException)
        {
            throw new ErrorValidacion(
                "Configura el secreto local Secretos:otp-salt antes de emitir OTP de prueba.",
                new[] { new DetalleError("Secretos:otp-salt", "requerido") });
        }

        var ahora = tiempo.GetUtcNow();
        var otp = CodigoAuthAdmin.Crear(
            "cod_" + Guid.NewGuid().ToString("N"),
            usuario.Id,
            numero,
            hasher.Hashear(codigo, pepper),
            ahora.AddMinutes(opciones.OtpTtlMinutos),
            opciones.OtpIntentos,
            usado: false,
            ahora,
            opciones.OtpTtlMinutos * 60);

        await codigos.GuardarAsync(otp, ct);
        return Results.Ok(new
        {
            numero = numero.Valor,
            codigo,
            otp.Expiracion,
            intentos = otp.IntentosRestantes,
            nota = "Solo Development. Usa este codigo en /login.",
        });
    }

    private static T Resolver<T>(HttpContext contexto)
        where T : notnull
        => contexto.RequestServices.GetService<T>()
            ?? throw new ErrorReglaNegocio("La simulacion requiere persistencia configurada para este entorno.");

    private static string Requerir(string? valor, string campo)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            throw new ErrorValidacion(
                $"El campo {campo} es obligatorio.",
                new[] { new DetalleError(campo, "obligatorio") });
        }

        return valor.Trim();
    }

    private sealed record AdminInicialRequest(
        string? Numero,
        string? Nombre,
        string? Area,
        string? Empresa,
        IReadOnlyCollection<string>? Tags);

    private sealed record OtpAdminRequest(string? Numero, string? Codigo);
}
