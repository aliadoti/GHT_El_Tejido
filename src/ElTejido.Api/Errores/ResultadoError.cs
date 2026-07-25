using ElTejido.Api.Observabilidad;

namespace ElTejido.Api.Errores;

/// <summary>
/// P-17 (API-001) — <see cref="IResult"/> que escribe el cuerpo de error uniforme (04 §3) con su
/// <c>correlationId</c>, para que las rutas que hoy devuelven resultados directos (404/401/403 sin
/// cuerpo, u objetos anónimos) usen el mismo contrato que el middleware de excepciones. Es el único
/// camino reutilizable para convertir un estado + código en <see cref="ErrorRespuesta"/>: reutiliza
/// <see cref="EscritorRespuestaError"/> y <see cref="AccesorCorrelationId"/>. No revela detalle
/// sensible (excepción, secreto, firma, token ni estado interno): el mensaje es seguro y genérico. El
/// encabezado <c>X-Correlation-Id</c> ya lo fija <see cref="MiddlewareCorrelationId"/> para toda
/// respuesta, de modo que el <c>correlationId</c> del cuerpo coincide con el del encabezado.
/// </summary>
internal sealed class ResultadoError : IResult
{
    private readonly ResultadoMapeoError _resultado;

    private ResultadoError(ResultadoMapeoError resultado) => _resultado = resultado;

    public Task ExecuteAsync(HttpContext httpContext)
    {
        var correlationId = AccesorCorrelationId.ObtenerOCrear(httpContext);
        return EscritorRespuestaError.EscribirAsync(httpContext, _resultado, correlationId, httpContext.RequestAborted);
    }

    /// <summary>Crea el resultado con un estado y código estables y un mensaje seguro, sin detalles.</summary>
    public static ResultadoError Crear(int status, string codigo, string mensaje)
        => new(new ResultadoMapeoError(status, codigo, mensaje, Array.Empty<CampoErrorRespuesta>()));

    /// <summary>404 <c>NOT_FOUND</c> para recursos no encontrados o deliberadamente no revelados.</summary>
    public static ResultadoError NoEncontrado(string mensaje = "Recurso no encontrado.")
        => Crear(StatusCodes.Status404NotFound, "NOT_FOUND", mensaje);

    /// <summary>401 <c>UNAUTHENTICATED</c> para credenciales o firma inválidas (sin revelar cuál).</summary>
    public static ResultadoError NoAutenticado(string mensaje = "Credenciales inválidas.")
        => Crear(StatusCodes.Status401Unauthorized, "UNAUTHENTICATED", mensaje);

    /// <summary>403 <c>FORBIDDEN</c> para una operación o verificación no permitida.</summary>
    public static ResultadoError Prohibido(string mensaje = "Operación no permitida.")
        => Crear(StatusCodes.Status403Forbidden, "FORBIDDEN", mensaje);
}
