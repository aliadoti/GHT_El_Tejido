using ElTejido.Application.WhatsApp;
using ElTejido.Domain.Campanas;
using ElTejido.Domain.Identidad;
using ElTejido.Domain.Participantes;
using ElTejido.Infrastructure.Notificaciones;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace ElTejido.UnitTests.Notificaciones;

public sealed class NotificadorOtpWhatsAppTests
{
    private const string Numero = "573001112233";
    private const string Codigo = "123456";

    [Fact]
    public async Task EnviarCodigoAsync_ConPlantilla_EnviaPlantillaAutenticacionConCodigoYTipoAutenticacion()
    {
        var gateway = Substitute.For<IWhatsAppGateway>();
        gateway
            .EnviarPlantillaAutenticacionAsync(
                Arg.Any<string>(),
                Arg.Any<PlantillaWhatsApp>(),
                Arg.Any<string>(),
                Arg.Any<TipoEnvioMensaje>(),
                Arg.Any<CancellationToken>())
            .Returns(EnvioResultado.Ok("wamid.1"));

        var notificador = Construir(gateway, new OpcionesOtpWhatsApp
        {
            Habilitado = true,
            PlantillaNombre = "el_tejido_otp",
            PlantillaIdioma = "es",
        });

        await notificador.EnviarCodigoAsync(NumeroWhatsApp.FromNormalized(Numero), Codigo, CancellationToken.None);

        await gateway.Received(1).EnviarPlantillaAutenticacionAsync(
            Numero,
            Arg.Is<PlantillaWhatsApp>(p => p.Nombre == "el_tejido_otp" && p.Idioma == "es"),
            Codigo,
            TipoEnvioMensaje.Autenticacion,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnviarCodigoAsync_GatewayDevuelveFallo_NoLanza()
    {
        var gateway = Substitute.For<IWhatsAppGateway>();
        gateway
            .EnviarPlantillaAutenticacionAsync(
                Arg.Any<string>(),
                Arg.Any<PlantillaWhatsApp>(),
                Arg.Any<string>(),
                Arg.Any<TipoEnvioMensaje>(),
                Arg.Any<CancellationToken>())
            .Returns(EnvioResultado.Fallo("WhatsApp rechazo el envio (HTTP 404)."));

        var notificador = Construir(gateway, new OpcionesOtpWhatsApp
        {
            Habilitado = true,
            PlantillaNombre = "el_tejido_otp",
        });

        var enviar = async () =>
            await notificador.EnviarCodigoAsync(NumeroWhatsApp.FromNormalized(Numero), Codigo, CancellationToken.None);

        await enviar.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnviarCodigoAsync_GatewayLanza_NoPropaga()
    {
        var gateway = Substitute.For<IWhatsAppGateway>();
        gateway
            .EnviarPlantillaAutenticacionAsync(
                Arg.Any<string>(),
                Arg.Any<PlantillaWhatsApp>(),
                Arg.Any<string>(),
                Arg.Any<TipoEnvioMensaje>(),
                Arg.Any<CancellationToken>())
            .Returns<EnvioResultado>(_ => throw new HttpRequestException("boom"));

        var notificador = Construir(gateway, new OpcionesOtpWhatsApp
        {
            Habilitado = true,
            PlantillaNombre = "el_tejido_otp",
        });

        var enviar = async () =>
            await notificador.EnviarCodigoAsync(NumeroWhatsApp.FromNormalized(Numero), Codigo, CancellationToken.None);

        await enviar.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnviarCodigoAsync_SinPlantilla_NoLlamaGateway()
    {
        var gateway = Substitute.For<IWhatsAppGateway>();

        var notificador = Construir(gateway, new OpcionesOtpWhatsApp
        {
            Habilitado = true,
            PlantillaNombre = string.Empty,
        });

        await notificador.EnviarCodigoAsync(NumeroWhatsApp.FromNormalized(Numero), Codigo, CancellationToken.None);

        await gateway.DidNotReceiveWithAnyArgs().EnviarPlantillaAutenticacionAsync(
            default!,
            default!,
            default!,
            default,
            default);
    }

    private static NotificadorOtpWhatsApp Construir(IWhatsAppGateway gateway, OpcionesOtpWhatsApp opciones)
        => new(gateway, Options.Create(opciones), NullLogger<NotificadorOtpWhatsApp>.Instance);
}
