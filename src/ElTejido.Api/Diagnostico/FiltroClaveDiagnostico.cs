using System.Security.Cryptography;
using System.Text;
using ElTejido.Application.Diagnostico;

namespace ElTejido.Api.Diagnostico;

/// <summary>
/// Filtro que protege endpoints de diagnostico/simulacion fuera de Development con la clave de
/// diagnostico (header <c>X-Diag-Key</c>, misma que <c>/health/ready</c>). En Development no exige
/// clave (DX local). Fuera de Development, si no hay clave configurada o no coincide, responde 404
/// (indistinguible de no-mapeado), de modo que la simulacion nunca queda abierta en el App Service.
/// </summary>
public sealed class FiltroClaveDiagnostico : IEndpointFilter
{
    private const string HeaderClave = "X-Diag-Key";

    private readonly IWebHostEnvironment _entorno;
    private readonly IProveedorClaveDiagnostico _claves;

    public FiltroClaveDiagnostico(IWebHostEnvironment entorno, IProveedorClaveDiagnostico claves)
    {
        _entorno = entorno;
        _claves = claves;
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (_entorno.IsDevelopment())
        {
            return await next(context);
        }

        var http = context.HttpContext;
        var esperada = await _claves.ObtenerClaveEsperadaAsync(http.RequestAborted);
        var recibida = http.Request.Headers[HeaderClave].ToString();

        if (string.IsNullOrEmpty(esperada) || !ClaveCoincide(recibida, esperada))
        {
            return Results.NotFound();
        }

        return await next(context);
    }

    private static bool ClaveCoincide(string recibida, string esperada)
        => CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(recibida),
            Encoding.UTF8.GetBytes(esperada));
}
