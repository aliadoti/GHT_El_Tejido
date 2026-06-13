using ElTejido.Application.Common;
using ElTejido.Domain.Common;

namespace ElTejido.Api.Errores;

/// <summary>
/// Traduce excepciones al modelo de errores uniforme (04 §3):
/// <list type="bullet">
/// <item><see cref="ExcepcionAplicacion"/> usa su propio codigo/estado/detalles.</item>
/// <item><see cref="DomainValidationException"/> mapea a 400 VALIDATION_ERROR.</item>
/// <item>Cualquier otra excepcion mapea a 500 INTERNAL_ERROR sin filtrar detalles.</item>
/// </list>
/// </summary>
internal static class MapeadorErrores
{
    private const string MensajeInterno = "Ocurrio un error inesperado.";

    public static ResultadoMapeoError Map(Exception excepcion)
    {
        return excepcion switch
        {
            ExcepcionAplicacion app => new ResultadoMapeoError(
                app.EstadoHttp,
                app.Codigo,
                app.Message,
                app.Detalles
                    .Select(d => new CampoErrorRespuesta(d.Campo, d.Problema))
                    .ToArray()),

            DomainValidationException dominio => new ResultadoMapeoError(
                400,
                "VALIDATION_ERROR",
                dominio.Message,
                new[] { new CampoErrorRespuesta(null, dominio.Code) }),

            // No se filtra el mensaje real de excepciones no controladas (04 §3, 10 §6.3).
            _ => new ResultadoMapeoError(
                500,
                "INTERNAL_ERROR",
                MensajeInterno,
                Array.Empty<CampoErrorRespuesta>()),
        };
    }
}
