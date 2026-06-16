using ElTejido.Application.WhatsApp;

namespace ElTejido.Infrastructure.WhatsApp;

/// <summary>Una linea de estado de entrega lista para log (sin PII ni secretos).</summary>
public readonly record struct LineaEstadoEntrega(bool EsFallo, string Texto);

/// <summary>
/// Extrae las notificaciones de estado de entrega (sent/delivered/read/failed) de un payload de
/// webhook a lineas listas para log. Permite diagnosticar entregas que Meta acepto (200) pero no
/// entrego (p. ej. numero fuera de la lista de prueba en modo desarrollo, code 131030). No incluye el
/// numero destinatario (PII, 10 §5/§6); el detalle de error de Meta no contiene nuestros secretos.
/// </summary>
public static class ResumenEstadosEntrega
{
    public static IReadOnlyList<LineaEstadoEntrega> Describir(WhatsAppWebhookPayload payload)
    {
        var estados = payload.Entry?
            .SelectMany(entry => entry.Changes ?? Array.Empty<WhatsAppChange>())
            .Select(change => change.Value)
            .Where(value => value is not null)
            .SelectMany(value => value!.Statuses ?? Array.Empty<WhatsAppStatus>())
            .ToArray();

        if (estados is null || estados.Length == 0)
        {
            return Array.Empty<LineaEstadoEntrega>();
        }

        var lineas = new List<LineaEstadoEntrega>();
        foreach (var estado in estados)
        {
            if (estado.Errors is { Count: > 0 })
            {
                foreach (var error in estado.Errors)
                {
                    var detalle = error.ErrorData?.Details ?? error.Message;
                    lineas.Add(new LineaEstadoEntrega(
                        EsFallo: true,
                        $"Entrega WhatsApp '{estado.Status}' (mensaje {estado.Id}) fallida: code={error.Code} title=\"{error.Title}\" detalle=\"{detalle}\"."));
                }
            }
            else
            {
                lineas.Add(new LineaEstadoEntrega(
                    EsFallo: false,
                    $"Entrega WhatsApp '{estado.Status}' (mensaje {estado.Id})."));
            }
        }

        return lineas;
    }
}
