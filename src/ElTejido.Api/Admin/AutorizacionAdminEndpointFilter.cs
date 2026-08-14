using ElTejido.Api.Auth;
using ElTejido.Application.Auth;
using ElTejido.Application.Common;
using ElTejido.Domain.Usuarios;

namespace ElTejido.Api.Admin;

/// <summary>
/// Marca un endpoint que, pese a no ser <c>GET</c>, no produce ningun efecto persistente (por
/// ejemplo la prevalidacion de un JSON que necesita cuerpo). DT-P32-02 §4: admite <c>admin|visor</c>
/// como cualquier lectura, pero conserva la exigencia de CSRF por tratarse de un POST del navegador.
/// </summary>
internal sealed record LecturaSinEfectosAdmin;

/// <summary>
/// Autoriza rutas <c>/api/admin/*</c> segun 04 §1/§5 y 06 §4.4: GET admite
/// <c>admin</c>/<c>visor</c>; mutaciones exigen <c>admin</c> y header CSRF.
/// </summary>
internal sealed class AutorizacionAdminEndpointFilter : IEndpointFilter
{
    private const string HeaderCsrf = "X-CSRF-Token";
    internal const string PrincipalItemKey = "ElTejido.Admin.Principal";

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

        httpContext.Items[PrincipalItemKey] = principal;

        if (HttpMethods.IsGet(httpContext.Request.Method))
        {
            ValidarRolLectura(principal);
            return await next(context);
        }

        if (EsLecturaSinEfectos(httpContext))
        {
            ValidarRolLectura(principal);
            ValidarCsrf(httpContext, principal);
            return await next(context);
        }

        ValidarRolMutacion(principal);
        ValidarCsrf(httpContext, principal);

        return await next(context);
    }

    internal static PrincipalSesion ObtenerPrincipal(HttpContext context)
        => context.Items.TryGetValue(PrincipalItemKey, out var value) && value is PrincipalSesion principal
            ? principal
            : throw new ErrorNoAutenticado("No hay una sesion administrativa validada.");

    private static bool EsLecturaSinEfectos(HttpContext httpContext)
        => httpContext.GetEndpoint()?.Metadata.GetMetadata<LecturaSinEfectosAdmin>() is not null;

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
