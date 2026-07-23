using ElTejido.Api.Admin;
using ElTejido.Api.Auth;
using ElTejido.Api.Diagnostico;
using ElTejido.Api.Errores;
using ElTejido.Api.Observabilidad;
using ElTejido.Api.Seguridad;
using ElTejido.Api.WhatsApp;
using ElTejido.Application.Common;
using ElTejido.Application.Configuracion;
using ElTejido.Application.Mantenimiento;
using ElTejido.Application.Reinicio;
using ElTejido.Application.Usuarios.CargaMasiva;
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
builder.Services.AgregarWhatsApp(builder.Configuration);
builder.Services.AgregarLlm(builder.Configuration);
builder.Services.AgregarMarkdown(builder.Configuration);
builder.Services.AgregarDiagnostico(builder.Configuration);
if (OpcionesPersistencia.HayAlmacen(builder.Configuration))
{
    builder.Services.AddScoped<IServicioGestionUsuarios, ServicioGestionUsuarios>();
    builder.Services.AddScoped<IServicioGestionCampanias, ServicioGestionCampanias>();
    builder.Services.AddScoped<IServicioGestionConfiguracion, ServicioGestionConfiguracion>();
    builder.Services.AddScoped<IServicioReinicioDatos, ServicioReinicioDatos>();
    builder.Services.AddScoped<IServicioPurgaCampanias, ServicioPurgaCampanias>();
    // I-08: carga masiva de participantes. Lector CSV sin dependencia (Sprint 1a); el puerto admite
    // sumar un lector .xlsx en Infraestructura mas adelante sin tocar el servicio.
    builder.Services.AddSingleton<ILectorArchivoParticipantes, LectorCsvParticipantes>();
    builder.Services.AddScoped<IServicioCargaMasiva, ServicioCargaMasiva>();
}

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

// Sirve el portal Angular (SPA) publicado en wwwroot. En local el portal corre con `ng serve`;
// en el despliegue (App Service) lo sirve la propia API desde wwwroot. El fallback a index.html
// (al final) habilita el ruteo del cliente (/login, /simulacion-whatsapp, etc.).
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRateLimiter();

app.MapGet("/health", () => Results.Ok(new HealthResponse("ok")))
    .WithName("Health")
    .WithSummary("Liveness endpoint for App Service and CI smoke tests.");

// Readiness: verifica dependencias externas (Key Vault, Cosmos, Blob, WhatsApp) protegido por
// clave de diagnostico. Util para confirmar el despliegue tras cargar secretos (guia de Azure §11).
app.MapearEndpointsPreparacion();

// Identidad y autenticacion admin (04 §4, 06).
app.MapearEndpointsAuth();
app.MapearEndpointsAdminConfiguracion();
app.MapearEndpointsAdminFase4();
app.MapearEndpointsAdminMantenimiento();

// WhatsApp Gateway: webhook entrante y envio masivo (04 §5.4/§6, 05).
app.MapearEndpointsWebhook();
app.MapearEndpointsAdminEnvios();

// Consulta de resultados: conversaciones, respuestas, evaluaciones, Markdown (04 §5.8).
app.MapearEndpointsAdminResultados();

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

// Simulacion de WhatsApp (pagina /simulacion-whatsapp): siempre en Development; en el resto solo si
// Simulacion:Habilitada=true, y entonces protegida por la clave de diagnostico (X-Diag-Key). Permite
// la prueba E2E simulada contra el despliegue real (Cosmos/Key Vault) sin conectar WhatsApp todavia.
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Simulacion:Habilitada"))
{
    app.MapearEndpointsSimulacion();
}

// Rutas no resueltas: las del SPA (deep-links como /login, /simulacion-whatsapp) sirven index.html;
// los prefijos de la API conservan su contrato (404 propio) en vez de devolver el HTML del portal.
var indiceSpa = app.Environment.WebRootFileProvider.GetFileInfo("index.html");
app.MapFallback(async context =>
{
    var ruta = context.Request.Path.Value ?? string.Empty;
    var esApi = ruta.StartsWith("/api", StringComparison.OrdinalIgnoreCase)
        || ruta.StartsWith("/webhook", StringComparison.OrdinalIgnoreCase)
        || ruta.StartsWith("/health", StringComparison.OrdinalIgnoreCase)
        || ruta.StartsWith("/diagnostico", StringComparison.OrdinalIgnoreCase);

    if (esApi || !indiceSpa.Exists)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    context.Response.ContentType = "text/html";
    await context.Response.SendFileAsync(indiceSpa);
});

app.Run();

public sealed record HealthResponse(string Status);

public partial class Program;
