using System.Text.Json;
using System.Text.Json.Serialization;

namespace ElTejido.Api.Errores;

/// <summary>
/// Escribe el cuerpo de error uniforme (04 §3) en la respuesta HTTP. Es el unico punto que
/// serializa el error, compartido por el middleware de excepciones y el rechazo de rate limiting,
/// para garantizar un formato consistente con su <c>correlationId</c>.
/// </summary>
internal static class EscritorRespuestaError
{
    private static readonly JsonSerializerOptions Opciones = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static async Task EscribirAsync(
        HttpContext context,
        ResultadoMapeoError resultado,
        string correlationId,
        CancellationToken cancellationToken)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.StatusCode = resultado.Status;
        context.Response.ContentType = "application/json; charset=utf-8";

        var cuerpo = new RespuestaError(new ErrorRespuesta(
            resultado.Code,
            resultado.Message,
            resultado.Details.Count == 0 ? null : resultado.Details,
            correlationId));

        await context.Response.WriteAsync(JsonSerializer.Serialize(cuerpo, Opciones), cancellationToken);
    }
}
