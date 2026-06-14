using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace ElTejido.IntegrationTests;

/// <summary>
/// Verifica el webhook de WhatsApp (04 §6, 05 §2.4): verificacion GET de Meta, rechazo por firma
/// invalida (401) y ack 200 con firma valida. No requiere Cosmos (el procesamiento asincrono se
/// limita a registrar que la persistencia no esta configurada).
/// </summary>
public sealed class WebhookIntegrationTests
{
    private const string VerifyToken = "verify-123";
    private const string AppSecret = "appsec-xyz";

    [Fact]
    public async Task GetVerify_TokenCorrecto_Responde200ConChallenge()
    {
        using var fabrica = Construir();
        using var client = fabrica.CreateClient();

        using var respuesta = await client.GetAsync(
            $"/webhook/whatsapp?hub.mode=subscribe&hub.verify_token={VerifyToken}&hub.challenge=42");

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        (await respuesta.Content.ReadAsStringAsync()).Should().Be("42");
    }

    [Fact]
    public async Task GetVerify_TokenIncorrecto_Responde403()
    {
        using var fabrica = Construir();
        using var client = fabrica.CreateClient();

        using var respuesta = await client.GetAsync(
            "/webhook/whatsapp?hub.mode=subscribe&hub.verify_token=incorrecto&hub.challenge=42");

        respuesta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Post_FirmaInvalida_Responde401()
    {
        using var fabrica = Construir();
        using var client = fabrica.CreateClient();

        using var contenido = new StringContent("{\"entry\":[]}", Encoding.UTF8, "application/json");
        contenido.Headers.Add("X-Hub-Signature-256", "sha256=firma-falsa");

        using var respuesta = await client.PostAsync("/webhook/whatsapp", contenido);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_FirmaValida_Responde200()
    {
        using var fabrica = Construir();
        using var client = fabrica.CreateClient();

        const string cuerpo = "{\"entry\":[]}";
        using var contenido = new StringContent(cuerpo, Encoding.UTF8, "application/json");
        contenido.Headers.Add("X-Hub-Signature-256", "sha256=" + Firmar(cuerpo));

        using var respuesta = await client.PostAsync("/webhook/whatsapp", contenido);

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static WebApplicationFactory<Program> Construir()
        => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration(config =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Secretos:wa-verify-token"] = VerifyToken,
                    ["Secretos:wa-appsec"] = AppSecret,
                });
            });
        });

    private static string Firmar(string cuerpo)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(AppSecret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(cuerpo))).ToLowerInvariant();
    }
}
