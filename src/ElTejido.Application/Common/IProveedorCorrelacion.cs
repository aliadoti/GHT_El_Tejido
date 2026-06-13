namespace ElTejido.Application.Common;

/// <summary>
/// Expone el correlationId vigente de la peticion a la capa de aplicacion sin acoplarla a HTTP
/// (10 §6.2). El Edge lo implementa sobre <c>HttpContext</c>; fuera de una peticion devuelve null.
/// </summary>
public interface IProveedorCorrelacion
{
    string? CorrelationIdActual { get; }
}
