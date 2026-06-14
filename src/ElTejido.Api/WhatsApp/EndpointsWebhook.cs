using System.Text.Json;
using ElTejido.Api.Seguridad;
using ElTejido.Application.Seguridad;
using ElTejido.Application.WhatsApp;
using ElTejido.Infrastructure.WhatsApp;
using Microsoft.Extensions.Options;

namespace ElTejido.Api.WhatsApp;

/// <summary>
/// Webhook de WhatsApp (04 §6, 05 §2.4): verificacion GET de Meta (<c>hub.challenge</c>) y recepcion
/// POST con verificacion de firma HMAC, ack 200 inmediato y encolado para procesamiento asincrono
/// (nunca procesa sincrono dentro del request, ARQ §4.2). Rate limit por IP en el POST (10 §3).
/// </summary>
internal static class EndpointsWebhook
{
    private const string HeaderFirma = "X-Hub-Signature-256";

    private static readonly JsonSerializerOptions OpcionesJson = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static IEndpointRouteBuilder MapearEndpointsWebhook(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/webhook/whatsapp");

        grupo.MapGet("", VerificarAsync);
        grupo.MapPost("", RecibirAsync)
            .RequireRateLimiting(PoliticasRateLimiting.Webhook);

        return app;
    }

    private static async Task<IResult> VerificarAsync(
        HttpContext contexto,
        ISecretProvider secretos,
        IOptions<OpcionesWhatsApp> opciones,
        CancellationToken cancellationToken)
    {
        var query = contexto.Request.Query;
        var modo = query["hub.mode"].ToString();
        var token = query["hub.verify_token"].ToString();
        var challenge = query["hub.challenge"].ToString();

        string esperado;
        try
        {
            esperado = await secretos.ObtenerSecretoAsync(opciones.Value.VerifyTokenSecretName, cancellationToken);
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException)
        {
            // Sin token de verificacion configurado no se puede validar: se rechaza (04 §6.1).
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        if (string.Equals(modo, "subscribe", StringComparison.Ordinal)
            && !string.IsNullOrEmpty(token)
            && string.Equals(token, esperado, StringComparison.Ordinal))
        {
            // Meta espera el challenge en texto plano con 200 (04 §6.1).
            return Results.Text(challenge, "text/plain");
        }

        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    private static async Task<IResult> RecibirAsync(
        HttpContext contexto,
        IWhatsAppGateway gateway,
        IColaWebhook cola,
        ISecretProvider secretos,
        IOptions<OpcionesWhatsApp> opciones,
        CancellationToken cancellationToken)
    {
        var cuerpo = await LeerCuerpoAsync(contexto.Request, cancellationToken);

        string appSecret;
        try
        {
            appSecret = await secretos.ObtenerSecretoAsync(opciones.Value.AppSecretSecretName, cancellationToken);
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException)
        {
            // Sin app secret no se puede verificar la firma: se descarta (04 §6.2, 10 §3).
            return Results.StatusCode(StatusCodes.Status401Unauthorized);
        }

        var firma = contexto.Request.Headers[HeaderFirma].ToString();
        if (!gateway.VerificarFirma(cuerpo, firma, appSecret))
        {
            return Results.StatusCode(StatusCodes.Status401Unauthorized);
        }

        // Firma valida: ack 200 inmediato y encolado (ARQ §4.2). El parseo y el procesamiento
        // ocurren en el trabajador de cola; los errores de forma no afectan el ack.
        try
        {
            var payload = JsonSerializer.Deserialize<WhatsAppWebhookPayload>(cuerpo, OpcionesJson);
            if (payload is not null)
            {
                await cola.EncolarAsync(payload, cancellationToken);
            }
        }
        catch (JsonException)
        {
            // Payload malformado: se ignora pero se mantiene el ack 200 a Meta.
        }

        return Results.Ok();
    }

    private static async Task<byte[]> LeerCuerpoAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        using var memoria = new MemoryStream();
        await request.Body.CopyToAsync(memoria, cancellationToken);
        return memoria.ToArray();
    }
}
