namespace ElTejido.Application.Common;

/// <summary>
/// Excepcion base de la capa de aplicacion que transporta el codigo de error, el estado HTTP
/// y los detalles que el Edge traducira al modelo de errores uniforme (04 §3).
/// El Edge NO debe filtrar secretos ni PII al construir <see cref="Exception.Message"/>.
/// </summary>
public abstract class ExcepcionAplicacion : Exception
{
    protected ExcepcionAplicacion(
        string codigo,
        int estadoHttp,
        string message,
        IReadOnlyList<DetalleError>? detalles = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Codigo = codigo;
        EstadoHttp = estadoHttp;
        Detalles = detalles ?? Array.Empty<DetalleError>();
    }

    /// <summary>Codigo estable del error (p. ej. <c>VALIDATION_ERROR</c>) segun 04 §3.</summary>
    public string Codigo { get; }

    /// <summary>Estado HTTP que el Edge devolvera para esta excepcion.</summary>
    public int EstadoHttp { get; }

    /// <summary>Detalles por campo; puede estar vacio.</summary>
    public IReadOnlyList<DetalleError> Detalles { get; }
}
