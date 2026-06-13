using ElTejido.Application.Auth;
using ElTejido.Application.Identidad;
using ElTejido.Domain.Identidad;
using ElTejido.Infrastructure.Notificaciones;
using ElTejido.Infrastructure.Seguridad;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace ElTejido.Infrastructure.Configuracion;

/// <summary>
/// Registra la identidad/matricula (06 §3) y la autenticacion admin por OTP (06 §4): puertos de
/// Application enlazados a sus adaptadores de Infrastructure, opciones de <c>Auth</c> y
/// <see cref="TimeProvider"/>. Consume <c>ISecretProvider</c> (Fase 2) y los repositorios Cosmos
/// (Fase 1); estos ultimos solo existen si hay configuracion Cosmos (registro guardado).
/// </summary>
public static class ServiciosAutenticacion
{
    public static IServiceCollection AgregarAutenticacion(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMemoryCache();
        services.Configure<OpcionesAuth>(configuration.GetSection(OpcionesAuth.Seccion));
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<OpcionesAuth>>().Value);
        services.TryAddSingleton(TimeProvider.System);

        // Servicios hoja sin dependencia de persistencia: se registran siempre.
        services.AddSingleton<INormalizadorNumero, NormalizadorNumero>();
        services.AddSingleton<IHasherOtp, HasherOtpBcrypt>();
        services.AddSingleton<IGeneradorCodigoOtp, GeneradorCodigoOtpCsprng>();
        services.AddSingleton<ILimitadorOtp, LimitadorOtpMemoria>();
        services.AddSingleton<IServicioSesion, ServicioSesionJwt>();
        services.AddSingleton<INotificadorOtp, NotificadorOtpLog>();

        // Los orquestadores dependen de los repositorios Cosmos (Fase 1), que solo existen si hay
        // configuracion Cosmos (registro guardado). Se gatillan con la misma condicion para que la
        // app arranque y valide su contenedor sin almacen (p. ej. /health). Ver SUPUESTOS.md.
        if (!string.IsNullOrWhiteSpace(configuration["Cosmos:AccountEndpoint"]))
        {
            services.AddScoped<IResolutorParticipante, ResolutorParticipante>();
            services.AddScoped<IAuthAdminService, AuthAdminService>();
        }

        return services;
    }
}
