using ElTejido.Api.Admin;
using ElTejido.Api.Auth;
using ElTejido.Api.Errores;
using ElTejido.Api.Observabilidad;
using ElTejido.Api.Seguridad;
using ElTejido.Application.Common;
using ElTejido.Infrastructure.Configuracion;

var builder = WebApplication.CreateBuilder(args);

// Limites de seguridad configurables (10 §2, §3; seccion "Seguridad" de 02 §6).
var opcionesSeguridad = new OpcionesSeguridad();
builder.Configuration.GetSection(OpcionesSeguridad.Seccion).Bind(opcionesSeguridad);

// Composition root: secretos (10 §4), persistencia Cosmos guardada (02 §6), autenticacion
// admin/identidad (06) y rate limiter.
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IProveedorCorrelacion, ProveedorCorrelacionHttp>();
builder.Services.AgregarSeguridad(builder.Configuration);
builder.Services.AgregarInfraestructura(builder.Configuration);
builder.Services.AgregarAutenticacion(builder.Configuration);
builder.Services.AgregarLimitadorTasa(opcionesSeguridad);

var app = builder.Build();

// El manejo de errores envuelve todo (04 §3); el correlationId va inmediatamente despues (10 §6.2).
app.UseMiddleware<MiddlewareManejoErrores>();
app.UseMiddleware<MiddlewareCorrelationId>();

// HTTPS/HSTS solo fuera de Development para no romper /health sobre http en pruebas (04 §8, 10 §3).
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseRateLimiter();

app.MapGet("/health", () => Results.Ok(new HealthResponse("ok")))
    .WithName("Health")
    .WithSummary("Liveness endpoint for App Service and CI smoke tests.");

// Identidad y autenticacion admin (04 §4, 06).
app.MapearEndpointsAuth();

if (app.Environment.IsDevelopment())
{
    // Endpoints de diagnostico (solo Development): ejercitan el modelo de errores (04 §3) y el
    // rate limiting (10 §2, §3). No se exponen en produccion.
    var diagnostico = app.MapGroup("/diagnostico");

    diagnostico.MapGet("/error", () =>
    {
        throw new InvalidOperationException("fallo simulado");
    });

    diagnostico.MapGet("/validacion", () =>
    {
        throw new ErrorValidacion(
            "El numero no tiene formato E.164.",
            new[] { new DetalleError("numero", "formato") });
    });

    diagnostico.MapGet("/limitado", () => Results.Ok(new HealthResponse("ok")))
        .RequireRateLimiting(PoliticasRateLimiting.Demo);

    // Guard comun de /api/admin/* (04 §5, 06 §4.4) ejercitado con endpoints minimos
    // solo en Development; los CRUD reales de Fase 4 reutilizaran este filtro.
    app.MapearEndpointsAdminDiagnostico();
}

app.Run();

public sealed record HealthResponse(string Status);

public partial class Program;
