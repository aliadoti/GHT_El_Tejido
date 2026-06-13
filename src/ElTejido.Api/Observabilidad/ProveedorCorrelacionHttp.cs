using ElTejido.Application.Common;

namespace ElTejido.Api.Observabilidad;

/// <summary>
/// Implementacion de <see cref="IProveedorCorrelacion"/> sobre <see cref="IHttpContextAccessor"/>:
/// expone a la capa de aplicacion el correlationId que el middleware guardo en
/// <c>HttpContext.Items</c> (10 §6.2), sin acoplarla a HTTP.
/// </summary>
internal sealed class ProveedorCorrelacionHttp : IProveedorCorrelacion
{
    private readonly IHttpContextAccessor _accessor;

    public ProveedorCorrelacionHttp(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    public string? CorrelationIdActual
    {
        get
        {
            var contexto = _accessor.HttpContext;
            if (contexto is null)
            {
                return null;
            }

            return contexto.Items.TryGetValue(CorrelacionConstantes.ClaveItems, out var valor)
                && valor is string correlationId
                ? correlationId
                : null;
        }
    }
}
