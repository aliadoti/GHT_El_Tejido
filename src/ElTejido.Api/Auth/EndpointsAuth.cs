using ElTejido.Api.Seguridad;
using ElTejido.Application.Auth;
using ElTejido.Application.Common;
using Microsoft.Extensions.DependencyInjection;

namespace ElTejido.Api.Auth;

/// <summary>
/// Endpoints de autenticacion admin por OTP de WhatsApp (04 §4, 06 §4). Respuestas neutrales
/// (REQ §10.3.10); rate limit por IP en endpoints publicos (10 §3). La sesion viaja en una cookie
/// <c>httpOnly/Secure/SameSite=Strict</c>.
/// </summary>
internal static class EndpointsAuth
{
    public static IEndpointRouteBuilder MapearEndpointsAuth(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/auth");

        grupo.MapPost("/request-code", SolicitarCodigoAsync)
            .RequireRateLimiting(PoliticasRateLimiting.Publico);

        grupo.MapPost("/verify-code", VerificarCodigoAsync)
            .RequireRateLimiting(PoliticasRateLimiting.Publico);

        grupo.MapPost("/logout", CerrarSesion);

        grupo.MapGet("/me", ObtenerSesionAsync);

        return app;
    }

    private static async Task<IResult> SolicitarCodigoAsync(
        SolicitudCodigoRequest peticion,
        HttpContext contexto,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(peticion.Numero))
        {
            throw new ErrorValidacion(
                "El numero es obligatorio.",
                new[] { new DetalleError("numero", "obligatorio") });
        }

        // El servicio esta guardado tras la presencia de Cosmos (necesita el almacen); se resuelve
        // aqui para no condicionar la inferencia de parametros del endpoint.
        var auth = contexto.RequestServices.GetRequiredService<IAuthAdminService>();
        await auth.SolicitarCodigoAsync(peticion.Numero, cancellationToken);

        // Siempre neutral: no revela si el numero existe (04 §4.1, REQ §10.3.10).
        return Results.Ok(new RespuestaNeutralCodigo(
            "Si el numero esta habilitado, recibiras un codigo por WhatsApp."));
    }

    private static async Task<IResult> VerificarCodigoAsync(
        VerificarCodigoRequest peticion,
        HttpContext contexto,
        IWebHostEnvironment entorno,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(peticion.Numero) || string.IsNullOrWhiteSpace(peticion.Codigo))
        {
            throw new ErrorValidacion(
                "El numero y el codigo son obligatorios.",
                ConstruirDetalles(peticion));
        }

        var auth = contexto.RequestServices.GetRequiredService<IAuthAdminService>();
        var sesion = await auth.VerificarCodigoAsync(peticion.Numero, peticion.Codigo, cancellationToken);
        if (sesion is null)
        {
            // Mensaje neutral: no distingue codigo invalido, vencido o usado (06 §4.3).
            throw new ErrorNoAutenticado("El codigo es invalido o ha expirado.");
        }

        EstablecerCookieSesion(contexto, entorno, sesion);
        return Results.Ok(new RespuestaSesion(sesion.Usuario, sesion.CsrfToken, sesion.ExpiraEn));
    }

    private static IResult CerrarSesion(HttpContext contexto, IWebHostEnvironment entorno)
    {
        contexto.Response.Cookies.Append(
            CookiesSesion.Nombre,
            string.Empty,
            ConstruirOpcionesCookie(entorno, DateTimeOffset.UnixEpoch));

        return Results.NoContent();
    }

    private static async Task<IResult> ObtenerSesionAsync(
        HttpContext contexto,
        IServicioSesion servicioSesion,
        CancellationToken cancellationToken)
    {
        var token = contexto.Request.Cookies[CookiesSesion.Nombre];
        if (string.IsNullOrEmpty(token))
        {
            throw new ErrorNoAutenticado("No hay una sesion activa.");
        }

        var principal = await servicioSesion.ValidarAsync(token, cancellationToken);
        if (principal is null)
        {
            throw new ErrorNoAutenticado("La sesion no es valida o ha expirado.");
        }

        var usuario = new UsuarioSesion(
            principal.UsuarioId,
            principal.Nombre,
            principal.Rol.ToString().ToLowerInvariant());

        return Results.Ok(new RespuestaMe(usuario));
    }

    private static void EstablecerCookieSesion(HttpContext contexto, IWebHostEnvironment entorno, SesionEmitida sesion)
    {
        contexto.Response.Cookies.Append(
            CookiesSesion.Nombre,
            sesion.Token,
            ConstruirOpcionesCookie(entorno, sesion.ExpiraEn));
    }

    private static CookieOptions ConstruirOpcionesCookie(IWebHostEnvironment entorno, DateTimeOffset expira)
        => new()
        {
            HttpOnly = true,
            // Secure fuera de Development; en pruebas (http sobre TestServer) la cookie viaja igual.
            Secure = !entorno.IsDevelopment(),
            SameSite = SameSiteMode.Strict,
            Expires = expira,
            Path = "/",
            IsEssential = true,
        };

    private static DetalleError[] ConstruirDetalles(VerificarCodigoRequest peticion)
    {
        var detalles = new List<DetalleError>(2);
        if (string.IsNullOrWhiteSpace(peticion.Numero))
        {
            detalles.Add(new DetalleError("numero", "obligatorio"));
        }

        if (string.IsNullOrWhiteSpace(peticion.Codigo))
        {
            detalles.Add(new DetalleError("codigo", "obligatorio"));
        }

        return detalles.ToArray();
    }
}
