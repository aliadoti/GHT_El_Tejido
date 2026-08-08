using ElTejido.Application.Conversacion;
using ElTejido.Application.Seguridad;
using ElTejido.Domain.Seguridad;
using ElTejido.Infrastructure.Configuracion;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace ElTejido.UnitTests.Configuracion;

public sealed class ServicioAuditoriaFrasesFinalizacionTests
{
    [Fact]
    public async Task StartAsync_ListaInvalida_RegistraSoloElMotivoSeguroYUsaElDefault()
    {
        var logs = Substitute.For<IRepositorioLogSeguridad>();
        var servicio = new ServicioAuditoriaFrasesFinalizacion(
            new OpcionesConversacion
            {
                FrasesFinalizarIdea = new List<string> { "dejar esta idea", "DEJAR, ESTA IDEA" },
            },
            logs,
            TimeProvider.System,
            NullLogger<ServicioAuditoriaFrasesFinalizacion>.Instance);

        await servicio.StartAsync(CancellationToken.None);

        await logs.Received(1).RegistrarAsync(
            Arg.Is<LogSeguridad>(log =>
                log.TipoEvento == TipoEventoSeguridad.ConfiguracionFrasesFinalizacion
                && log.Resultado == "descartada"
                && log.Detalle == "lista=finalizarIdea;motivo=duplicado"
                && !log.Detalle.Contains("dejar", StringComparison.OrdinalIgnoreCase)),
            Arg.Any<CancellationToken>());
        await logs.Received(1).RegistrarAsync(
            Arg.Is<LogSeguridad>(log =>
                log.TipoEvento == TipoEventoSeguridad.ConfiguracionFrasesFinalizacion
                && log.Resultado == "default"
                && log.Detalle == "lista=finalizarParticipacion;version=compilada"),
            Arg.Any<CancellationToken>());
    }
}
