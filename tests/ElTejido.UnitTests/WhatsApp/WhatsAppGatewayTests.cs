using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ElTejido.Application.Seguridad;
using ElTejido.Application.WhatsApp;
using ElTejido.Domain.Campanas;
using ElTejido.Domain.Participantes;
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

    [Fact]
    public async Task EnviarTexto_SecretoTokenAusente_DevuelveFalloSinLanzar()
    {
        // Con el secreto del token ausente, el proveedor lanza KeyNotFoundException (contrato
        // normalizado). El envio no debe propagar la excepcion: degrada a Fallo con un mensaje
        // diciente que nombra el secreto faltante, para que el administrador sepa que configurar.
        var secretos = Substitute.For<ISecretProvider>();
        secretos.ObtenerSecretoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<string>(_ => throw new KeyNotFoundException("falta el secreto"));
        var gateway = Construir(secretos);

        var resultado = await gateway.EnviarTextoAsync(
            "573001112233",
            "Hola",
            TipoEnvioMensaje.Inicial,
            CancellationToken.None);

        resultado.Exito.Should().BeFalse();
        resultado.Error.Should().Contain(new OpcionesWhatsApp().AccessTokenSecretName);
    }

    [Fact]
    public async Task EnviarPlantillaAutenticacion_ArmaBodyYBotonConElCodigo()
    {
        var secretos = Substitute.For<ISecretProvider>();
        secretos.ObtenerSecretoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("token-de-prueba");
        var handler = new HandlerCapturador(HttpStatusCode.OK, "{\"messages\":[{\"id\":\"wamid.1\"}]}");
        var opciones = new OpcionesWhatsApp { PhoneNumberId = "123456" };
        var gateway = Construir(secretos, handler, opciones);

        var plantilla = PlantillaWhatsApp.Crear("el_tejido_otp", "es", componentes: null);
        var resultado = await gateway.EnviarPlantillaAutenticacionAsync(
            "573001112233",
            plantilla,
            "987654",
            TipoEnvioMensaje.Autenticacion,
            CancellationToken.None);

        resultado.Exito.Should().BeTrue();
        handler.UltimaRuta.Should().EndWith("/123456/messages");

        using var doc = JsonDocument.Parse(handler.UltimoCuerpo!);
        var componentes = doc.RootElement.GetProperty("template").GetProperty("components");
        componentes.GetArrayLength().Should().Be(2);

        var body = componentes[0];
        body.GetProperty("type").GetString().Should().Be("body");
        body.GetProperty("parameters")[0].GetProperty("text").GetString().Should().Be("987654");

        var boton = componentes[1];
        boton.GetProperty("type").GetString().Should().Be("button");
        boton.GetProperty("sub_type").GetString().Should().Be("url");
        boton.GetProperty("index").GetString().Should().Be("0");
        boton.GetProperty("parameters")[0].GetProperty("text").GetString().Should().Be("987654");
    }

    [Fact]
    public async Task EnviarPlantillaAutenticacion_RespuestaError_DevuelveFalloConDetalleDeMeta()
    {
        var secretos = Substitute.For<ISecretProvider>();
        secretos.ObtenerSecretoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("token-de-prueba");
        const string cuerpoError =
            "{\"error\":{\"message\":\"Unsupported post request.\",\"type\":\"GraphMethodException\",\"code\":100,\"fbtrace_id\":\"AbC123\"}}";
        var handler = new HandlerCapturador(HttpStatusCode.NotFound, cuerpoError);
        var gateway = Construir(secretos, handler, new OpcionesWhatsApp { PhoneNumberId = "123456", MaxReintentos = 0 });

        var plantilla = PlantillaWhatsApp.Crear("el_tejido_otp", "es", componentes: null);
        var resultado = await gateway.EnviarPlantillaAutenticacionAsync(
            "573001112233",
            plantilla,
            "987654",
            TipoEnvioMensaje.Autenticacion,
            CancellationToken.None);

        resultado.Exito.Should().BeFalse();
        resultado.Error.Should().Contain("HTTP 404");
        resultado.Error.Should().Contain("code=100");
        resultado.Error.Should().Contain("fbtrace_id=AbC123");
    }

    private static WhatsAppGateway Construir()
        => Construir(Substitute.For<ISecretProvider>());

    private static WhatsAppGateway Construir(ISecretProvider secretos)
        => new(
            new HttpClient(),
            secretos,
            Options.Create(new OpcionesWhatsApp()),
            new RelojFijo(DateTimeOffset.UnixEpoch),
            NullLogger<WhatsAppGateway>.Instance);

    private static WhatsAppGateway Construir(ISecretProvider secretos, HttpMessageHandler handler, OpcionesWhatsApp opciones)
        => new(
            new HttpClient(handler),
            secretos,
            Options.Create(opciones),
            new RelojFijo(DateTimeOffset.UnixEpoch),
            NullLogger<WhatsAppGateway>.Instance);

    private sealed class HandlerCapturador : HttpMessageHandler
    {
        private readonly HttpStatusCode _estado;
        private readonly string _cuerpoRespuesta;

        public HandlerCapturador(HttpStatusCode estado, string cuerpoRespuesta)
        {
            _estado = estado;
            _cuerpoRespuesta = cuerpoRespuesta;
        }

        public string? UltimaRuta { get; private set; }

        public string? UltimoCuerpo { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            UltimaRuta = request.RequestUri?.ToString();
            UltimoCuerpo = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(_estado) { Content = new StringContent(_cuerpoRespuesta) };
        }
    }

    private static string Hmac(byte[] cuerpo, string secreto)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secreto));
        return Convert.ToHexString(hmac.ComputeHash(cuerpo)).ToLowerInvariant();
    }
}
