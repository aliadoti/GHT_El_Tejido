using ElTejido.Application.Diagnostico;
using ElTejido.Infrastructure.WhatsApp;
using Microsoft.Extensions.Options;

namespace ElTejido.Infrastructure.Diagnostico;

/// <summary>
/// Comprueba la configuracion <b>no secreta</b> de WhatsApp (05 §2.6, guia de Azure §10): que
/// <c>WhatsApp:PhoneNumberId</c> y <c>WhatsApp:GraphApiBaseUrl</c> esten poblados. Los secretos
/// (<c>wa-token</c>, <c>wa-appsec</c>, <c>wa-verify-token</c>) los cubre <see cref="ComprobacionSecretos"/>.
/// </summary>
public sealed class ComprobacionWhatsApp : IComprobacionPreparacion
{
    private readonly OpcionesWhatsApp _opciones;

    public ComprobacionWhatsApp(IOptions<OpcionesWhatsApp> opciones)
    {
        _opciones = opciones.Value;
    }

    public Task<IReadOnlyList<ResultadoComprobacion>> ComprobarAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<ResultadoComprobacion> resultados = new[]
        {
            Evaluar("whatsapp:PhoneNumberId", _opciones.PhoneNumberId, "Falta el Phone number ID de Meta."),
            Evaluar("whatsapp:GraphApiBaseUrl", _opciones.GraphApiBaseUrl, "Falta la URL base de Graph API."),
        };

        return Task.FromResult(resultados);
    }

    private static ResultadoComprobacion Evaluar(string componente, string valor, string detalleFaltante)
        => string.IsNullOrWhiteSpace(valor)
            ? new ResultadoComprobacion(componente, EstadoPreparacion.Faltante, detalleFaltante)
            : new ResultadoComprobacion(componente, EstadoPreparacion.Ok, "Configurado.");
}
