namespace ElTejido.Application.Common;

// Grupo cohesionado de excepciones tipadas de la capa de aplicacion.
// Cada una fija su codigo y estado HTTP segun la tabla de 04 §3, para que el Edge
// las traduzca al modelo de errores uniforme sin logica de mapeo dispersa.
// El estado 500 INTERNAL_ERROR NO tiene tipo: corresponde a fallos no controlados.

/// <summary>400 VALIDATION_ERROR — payload invalido (04 §3).</summary>
public sealed class ErrorValidacion : ExcepcionAplicacion
{
    public ErrorValidacion(string message, IReadOnlyList<DetalleError>? detalles = null)
        : base("VALIDATION_ERROR", 400, message, detalles)
    {
    }
}

/// <summary>401 UNAUTHENTICATED — sin sesion valida (04 §3). Mensaje neutral en auth (REQ §10.3.10).</summary>
public sealed class ErrorNoAutenticado : ExcepcionAplicacion
{
    public ErrorNoAutenticado(string message, IReadOnlyList<DetalleError>? detalles = null)
        : base("UNAUTHENTICATED", 401, message, detalles)
    {
    }
}

/// <summary>403 FORBIDDEN — rol insuficiente (04 §3).</summary>
public sealed class ErrorProhibido : ExcepcionAplicacion
{
    public ErrorProhibido(string message, IReadOnlyList<DetalleError>? detalles = null)
        : base("FORBIDDEN", 403, message, detalles)
    {
    }
}

/// <summary>404 NOT_FOUND — recurso inexistente (04 §3).</summary>
public sealed class ErrorNoEncontrado : ExcepcionAplicacion
{
    public ErrorNoEncontrado(string message, IReadOnlyList<DetalleError>? detalles = null)
        : base("NOT_FOUND", 404, message, detalles)
    {
    }
}

/// <summary>409 CONFLICT — estado invalido o duplicado (04 §3).</summary>
public sealed class ErrorConflicto : ExcepcionAplicacion
{
    public ErrorConflicto(string message, IReadOnlyList<DetalleError>? detalles = null)
        : base("CONFLICT", 409, message, detalles)
    {
    }
}

/// <summary>422 BUSINESS_RULE — regla de negocio violada (04 §3).</summary>
public sealed class ErrorReglaNegocio : ExcepcionAplicacion
{
    public ErrorReglaNegocio(string message, IReadOnlyList<DetalleError>? detalles = null)
        : base("BUSINESS_RULE", 422, message, detalles)
    {
    }
}

/// <summary>429 RATE_LIMITED — limite de abuso/consumo (04 §3, 10 §2). El Edge agrega <c>Retry-After</c>.</summary>
public sealed class ErrorLimiteTasa : ExcepcionAplicacion
{
    public ErrorLimiteTasa(string message, IReadOnlyList<DetalleError>? detalles = null)
        : base("RATE_LIMITED", 429, message, detalles)
    {
    }
}

/// <summary>502 UPSTREAM_ERROR — fallo de WhatsApp/LLM aguas arriba (04 §3).</summary>
public sealed class ErrorUpstream : ExcepcionAplicacion
{
    public ErrorUpstream(string message, IReadOnlyList<DetalleError>? detalles = null, Exception? innerException = null)
        : base("UPSTREAM_ERROR", 502, message, detalles, innerException)
    {
    }
}
