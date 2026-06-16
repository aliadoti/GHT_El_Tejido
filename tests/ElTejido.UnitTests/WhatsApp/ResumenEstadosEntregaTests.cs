using ElTejido.Application.WhatsApp;
using ElTejido.Infrastructure.WhatsApp;
using FluentAssertions;

namespace ElTejido.UnitTests.WhatsApp;

public sealed class ResumenEstadosEntregaTests
{
    [Fact]
    public void Describir_EstadoFallido_DevuelveLineaDeFalloConCodigoYDetalle()
    {
        var payload = ConPayloadDeEstado(new WhatsAppStatus
        {
            Id = "wamid.OUT",
            Status = "failed",
            Errors = new[]
            {
                new WhatsAppStatusError
                {
                    Code = 131030,
                    Title = "Recipient phone number not in allowed list",
                    ErrorData = new WhatsAppStatusErrorData { Details = "Recipient not in allowed list" },
                },
            },
        });

        var lineas = ResumenEstadosEntrega.Describir(payload);

        lineas.Should().HaveCount(1);
        lineas[0].EsFallo.Should().BeTrue();
        lineas[0].Texto.Should().Contain("code=131030");
        lineas[0].Texto.Should().Contain("wamid.OUT");
        lineas[0].Texto.Should().Contain("Recipient not in allowed list");
    }

    [Fact]
    public void Describir_EstadoEntregadoSinError_DevuelveLineaInformativa()
    {
        var payload = ConPayloadDeEstado(new WhatsAppStatus { Id = "wamid.OUT", Status = "delivered" });

        var lineas = ResumenEstadosEntrega.Describir(payload);

        lineas.Should().HaveCount(1);
        lineas[0].EsFallo.Should().BeFalse();
        lineas[0].Texto.Should().Contain("delivered");
    }

    [Fact]
    public void Describir_PayloadSinEstados_DevuelveVacio()
    {
        ResumenEstadosEntrega.Describir(new WhatsAppWebhookPayload()).Should().BeEmpty();
    }

    private static WhatsAppWebhookPayload ConPayloadDeEstado(WhatsAppStatus estado)
        => new()
        {
            Entry = new[]
            {
                new WhatsAppEntry
                {
                    Changes = new[]
                    {
                        new WhatsAppChange { Value = new WhatsAppChangeValue { Statuses = new[] { estado } } },
                    },
                },
            },
        };
}
