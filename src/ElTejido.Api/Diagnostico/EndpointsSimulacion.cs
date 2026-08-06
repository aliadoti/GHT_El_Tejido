using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ElTejido.Application.Auth;
using ElTejido.Application.Common;
using ElTejido.Application.Seguridad;
using ElTejido.Application.Usuarios;
using ElTejido.Application.WhatsApp;
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
        // Fuera de Development el grupo exige la clave de diagnostico (X-Diag-Key); en Development
        // el filtro no exige clave. Estos endpoints crean admin y emiten OTP, por eso van protegidos.
        var grupo = app.MapGroup("/diagnostico/simulacion")
            .AddEndpointFilter<FiltroClaveDiagnostico>();

        grupo.MapPost("/admin-inicial", CrearAdminInicialAsync);
        grupo.MapPost("/otp-admin", CrearOtpAdminAsync);
        grupo.MapPost("/webhook-entrante", InyectarWebhookEntranteAsync);

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

    /// <summary>
    /// Inyecta un mensaje entrante ya autenticado por <see cref="FiltroClaveDiagnostico"/> en la
    /// misma cola que usa el webhook real tras validar su firma (DT-QA-01). Nunca recibe ni usa el
    /// App Secret de Meta; el procesamiento posterior conserva deduplicación y reglas de negocio.
    /// </summary>
    private static async Task<IResult> InyectarWebhookEntranteAsync(
        WebhookEntranteSimuladoRequest request,
        HttpContext contexto,
        INormalizadorNumero normalizador,
        IColaWebhook cola,
        TimeProvider tiempo,
        CancellationToken ct)
    {
        var numero = normalizador.Normalizar(Requerir(request.Numero, "numero"));
        var texto = Requerir(request.Texto, "texto");
        var idFueGenerado = string.IsNullOrWhiteSpace(request.WhatsappMessageId);
        var ahora = tiempo.GetUtcNow();
        var messageId = idFueGenerado
            ? GenerarIdMensaje(numero.Valor, texto, ahora)
            : request.WhatsappMessageId!.Trim();

        var payload = new WhatsAppWebhookPayload
        {
            Entry =
            [
                new WhatsAppEntry
                {
                    Changes =
                    [
                        new WhatsAppChange
                        {
                            Value = new WhatsAppChangeValue
                            {
                                Metadata = string.IsNullOrWhiteSpace(request.PhoneNumberIdDestino)
                                    ? null
                                    : new WhatsAppMetadata { PhoneNumberId = request.PhoneNumberIdDestino.Trim() },
                                Messages =
                                [
                                    new WhatsAppMessage
                                    {
                                        From = numero.Valor,
                                        Id = messageId,
                                        Timestamp = ahora.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
                                        Type = "text",
                                        Text = new WhatsAppMessageText { Body = texto },
                                    },
                                ],
                            },
                        },
                    ],
                },
            ],
        };

        // Mismo límite del webhook firmado: ack inmediato y procesamiento posterior por el worker.
        await cola.EncolarAsync(payload, ct);
        await RegistrarInyeccionAsync(contexto, idFueGenerado, ahora, ct);
        return Results.Ok();
    }

    private static string GenerarIdMensaje(string numero, string texto, DateTimeOffset ahora)
    {
        // La fecha es el día UTC, no el instante: el mismo reintento de la corrida mantiene el id
        // para que IRegistroWebhookDedupe aplique exactamente la protección ya existente.
        var material = $"{numero}\n{texto}\n{ahora:yyyy-MM-dd}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        return "sim_" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static async Task RegistrarInyeccionAsync(
        HttpContext contexto,
        bool idFueGenerado,
        DateTimeOffset ahora,
        CancellationToken ct)
    {
        var logs = contexto.RequestServices.GetService<IRepositorioLogSeguridad>();
        if (logs is null)
        {
            return;
        }

        var correlacion = contexto.RequestServices.GetService<IProveedorCorrelacion>();
        var log = LogSeguridad.Crear(
            "log_" + Guid.NewGuid().ToString("N"),
            TipoEventoSeguridad.SimulacionWebhookEntrante,
            usuarioId: null,
            numero: null,
            resultado: "encolado",
            detalle: $"origen=simulacionDiagnostico; idGenerado={idFueGenerado.ToString().ToLowerInvariant()}",
            correlationId: correlacion?.CorrelationIdActual,
            timestamp: ahora);
        await logs.RegistrarAsync(log, ct);
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

    private sealed record WebhookEntranteSimuladoRequest(
        string? Numero,
        string? Texto,
        string? WhatsappMessageId,
        string? PhoneNumberIdDestino);
}
