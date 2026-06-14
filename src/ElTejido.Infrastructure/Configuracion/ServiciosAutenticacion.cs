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

        // Los orquestadores dependen de los repositorios de Fase 1, que se registran via
        // AgregarInfraestructura en modo Cosmos o Memoria. Si no hay almacen configurado la app
        // sigue arrancando solo para /health.
        if (OpcionesPersistencia.HayAlmacen(configuration))
        {
            services.AddScoped<IResolutorParticipante, ResolutorParticipante>();
            services.AddScoped<IAuthAdminService, AuthAdminService>();
        }

        return services;
    }
}
