using System.Security.Cryptography;
using System.Text;
using ElTejido.Application.WhatsApp;
using ElTejido.Infrastructure.WhatsApp;
using ElTejido.UnitTests.Soporte;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace ElTejido.UnitTests.WhatsApp;

public sealed class WhatsAppGatewayTests
{
    private const string AppSecret = "app-secret-de-prueba";

    [Fact]
    public void VerificarFirma_FirmaValida_DevuelveTrue()
    {
        var gateway = Construir();
        var cuerpo = Encoding.UTF8.GetBytes("{\"hola\":\"mundo\"}");

        var resultado = gateway.VerificarFirma(cuerpo, "sha256=" + Hmac(cuerpo, AppSecret), AppSecret);

        resultado.Should().BeTrue();
    }

    [Fact]
    public void VerificarFirma_SinPrefijoSha256_DevuelveTrue()
    {
        var gateway = Construir();
        var cuerpo = Encoding.UTF8.GetBytes("payload");

        gateway.VerificarFirma(cuerpo, Hmac(cuerpo, AppSecret), AppSecret).Should().BeTrue();
    }

    [Fact]
    public void VerificarFirma_FirmaIncorrecta_DevuelveFalse()
    {
        var gateway = Construir();
        var cuerpo = Encoding.UTF8.GetBytes("payload");

        gateway.VerificarFirma(cuerpo, "sha256=" + Hmac(cuerpo, "otro-secreto"), AppSecret).Should().BeFalse();
    }

    [Fact]
    public void VerificarFirma_HeaderVacio_DevuelveFalse()
    {
        var gateway = Construir();

        gateway.VerificarFirma(Encoding.UTF8.GetBytes("x"), null, AppSecret).Should().BeFalse();
    }

    [Fact]
    public void ParsearWebhook_MensajeTexto_DevuelveMensajeEntrante()
    {
        var gateway = Construir();
        var payload = new WhatsAppWebhookPayload
        {
            Entry = new[]
            {
                new WhatsAppEntry
                {
                    Changes = new[]
                    {
                        new WhatsAppChange
                        {
                            Value = new WhatsAppChangeValue
                            {
                                Messages = new[]
                                {
                                    new WhatsAppMessage
                                    {
                                        From = "573001112233",
                                        Id = "wamid.ABC",
                                        Timestamp = "1700000000",
                                        Type = "text",
                                        Text = new WhatsAppMessageText { Body = "Mi idea" },
                                    },
                                },
                            },
                        },
                    },
                },
            },
        };

        var mensaje = gateway.ParsearWebhook(payload);

        mensaje.Should().NotBeNull();
        mensaje!.NumeroE164.Should().Be("573001112233");
        mensaje.Texto.Should().Be("Mi idea");
        mensaje.WhatsappMessageId.Should().Be("wamid.ABC");
        mensaje.Timestamp.Should().Be(DateTimeOffset.FromUnixTimeSeconds(1700000000));
    }

    [Fact]
    public void ParsearWebhook_SinMensajes_DevuelveNull()
    {
        var gateway = Construir();
        var payload = new WhatsAppWebhookPayload
        {
            Entry = new[] { new WhatsAppEntry { Changes = new[] { new WhatsAppChange { Value = new WhatsAppChangeValue() } } } },
        };

        gateway.ParsearWebhook(payload).Should().BeNull();
    }

    [Fact]
    public void ParsearWebhook_MensajeNoTexto_DevuelveNull()
    {
        var gateway = Construir();
        var payload = new WhatsAppWebhookPayload
        {
            Entry = new[]
            {
                new WhatsAppEntry
                {
                    Changes = new[]
                    {
                        new WhatsAppChange
                        {
                            Value = new WhatsAppChangeValue
                            {
                                Messages = new[]
                                {
                                    new WhatsAppMessage { From = "573001112233", Id = "wamid.X", Type = "image" },
                                },
                            },
                        },
                    },
                },
            },
        };

        gateway.ParsearWebhook(payload).Should().BeNull();
    }

    private static WhatsAppGateway Construir()
        => new(
            new HttpClient(),
            Substitute.For<ElTejido.Application.Seguridad.ISecretProvider>(),
            Options.Create(new OpcionesWhatsApp()),
            new RelojFijo(DateTimeOffset.UnixEpoch),
            NullLogger<WhatsAppGateway>.Instance);

    private static string Hmac(byte[] cuerpo, string secreto)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secreto));
        return Convert.ToHexString(hmac.ComputeHash(cuerpo)).ToLowerInvariant();
    }
}
