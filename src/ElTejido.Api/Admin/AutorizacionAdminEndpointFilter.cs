using ElTejido.Api.Auth;
using ElTejido.Application.Auth;
using ElTejido.Application.Common;
using ElTejido.Domain.Usuarios;

namespace ElTejido.Api.Admin;

/// <summary>
/// Autoriza rutas <c>/api/admin/*</c> segun 04 §1/§5 y 06 §4.4: GET admite
/// <c>admin</c>/<c>visor</c>; mutaciones exigen <c>admin</c> y header CSRF.
/// </summary>
internal sealed class AutorizacionAdminEndpointFilter : IEndpointFilter
{
    private const string HeaderCsrf = "X-CSRF-Token";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        var token = httpContext.Request.Cookies[CookiesSesion.Nombre];
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ErrorNoAutenticado("No hay una sesion activa.");
        }

        var servicioSesion = httpContext.RequestServices.GetRequiredService<IServicioSesion>();
        var principal = await servicioSesion.ValidarAsync(token, httpContext.RequestAborted);
        if (principal is null)
        {
            throw new ErrorNoAutenticado("La sesion no es valida o ha expirado.");
        }

        var esLectura = HttpMethods.IsGet(httpContext.Request.Method);
        if (esLectura)
        {
            ValidarRolLectura(principal);
            return await next(context);
        }

        ValidarRolMutacion(principal);
        ValidarCsrf(httpContext, principal);

        return await next(context);
    }

    private static void ValidarRolLectura(PrincipalSesion principal)
    {
        if (principal.Rol is not (RolUsuario.Admin or RolUsuario.Visor))
        {
            throw new ErrorProhibido("El rol no tiene permisos para consultar recursos administrativos.");
        }
    }

    private static void ValidarRolMutacion(PrincipalSesion principal)
    {
        if (principal.Rol is not RolUsuario.Admin)
        {
            throw new ErrorProhibido("El rol no tiene permisos para modificar recursos administrativos.");
        }
    }

    private static void ValidarCsrf(HttpContext httpContext, PrincipalSesion principal)
    {
        var recibido = httpContext.Request.Headers[HeaderCsrf].ToString();
        if (string.IsNullOrWhiteSpace(recibido) || !string.Equals(recibido, principal.CsrfToken, StringComparison.Ordinal))
        {
            throw new ErrorProhibido("El token CSRF no es valido.");
        }
    }
}
