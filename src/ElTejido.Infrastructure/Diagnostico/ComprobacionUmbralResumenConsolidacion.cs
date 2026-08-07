using ElTejido.Application.Conversacion;
using ElTejido.Application.Diagnostico;
using Microsoft.Extensions.Options;

namespace ElTejido.Infrastructure.Diagnostico;

/// <summary>
/// P-31: advierte una configuracion global que no puede disparar resumen antes del cierre por madurez.
/// Es un diagnostico operativo, no bloquea el arranque ni modifica configuracion remota.
/// </summary>
public sealed class ComprobacionUmbralResumenConsolidacion : IComprobacionPreparacion
{
    private readonly OpcionesConversacion _opciones;

    public ComprobacionUmbralResumenConsolidacion(IOptions<OpcionesConversacion> opciones)
    {
        _opciones = opciones.Value;
    }

    public Task<IReadOnlyList<ResultadoComprobacion>> ComprobarAsync(CancellationToken cancellationToken)
    {
        if (!_opciones.ResumenConsolidacionHabilitado || _opciones.UmbralResumenConsolidacion <= 0)
        {
            return Task.FromResult<IReadOnlyList<ResultadoComprobacion>>([
                new("conversacion:umbralResumenConsolidacion", EstadoPreparacion.NoAplica, "Resumen de consolidacion desactivado.")]);
        }

        var inalcanzable = _opciones.UmbralResumenConsolidacion >= _opciones.UmbralCierreAnticipado;
        return Task.FromResult<IReadOnlyList<ResultadoComprobacion>>([
            new(
                "conversacion:umbralResumenConsolidacion",
                inalcanzable ? EstadoPreparacion.Faltante : EstadoPreparacion.Ok,
                inalcanzable
                    ? "El umbral de resumen global es igual o mayor al umbral base; no disparara antes del cierre."
                    : "Configuracion global compatible con el resumen antes del cierre.")]);
    }
}
