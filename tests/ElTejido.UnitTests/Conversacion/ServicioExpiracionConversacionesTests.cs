using ElTejido.Application.Conversacion;
using ElTejido.Domain.Conversaciones;
using ElTejido.UnitTests.Soporte;
using FluentAssertions;
using NSubstitute;
using DominioConversacion = ElTejido.Domain.Conversaciones.Conversacion;

namespace ElTejido.UnitTests.Conversacion;

public sealed class ServicioExpiracionConversacionesTests
{
    private static readonly DateTimeOffset Ahora = DateTimeOffset.UnixEpoch.AddDays(10);

    private readonly IRepositorioConversaciones _conversaciones = Substitute.For<IRepositorioConversaciones>();

    [Fact]
    public async Task CerrarExpiradas_Habilitada_CierraLosHilosAbiertos()
    {
        var conv1 = DominioConversacion.Iniciar("conv_1", "c_1", "u_1", "p_1", "whatsapp", null, DateTimeOffset.UnixEpoch);
        var conv2 = DominioConversacion.Iniciar("conv_2", "c_1", "u_2", "p_1", "whatsapp", null, DateTimeOffset.UnixEpoch);
        _conversaciones.ListarAbiertasInactivasAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(new[] { conv1, conv2 });

        var cerradas = await Construir(horas: 48).CerrarExpiradasAsync(CancellationToken.None);

        cerradas.Should().Be(2);
        await _conversaciones.Received(2).GuardarConversacionAsync(
            Arg.Is<DominioConversacion>(c => c.Estado == EstadoConversacion.Cerrada), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CerrarExpiradas_UsaElPlazoConfigurado_ParaElLimite()
    {
        _conversaciones.ListarAbiertasInactivasAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<DominioConversacion>());

        await Construir(horas: 48).CerrarExpiradasAsync(CancellationToken.None);

        // El limite debe ser "ahora - 48h".
        await _conversaciones.Received(1).ListarAbiertasInactivasAsync(
            Ahora.AddHours(-48), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CerrarExpiradas_Desactivada_NoConsultaNiCierra()
    {
        var servicio = Construir(horas: 0);

        servicio.Habilitada.Should().BeFalse();
        (await servicio.CerrarExpiradasAsync(CancellationToken.None)).Should().Be(0);

        await _conversaciones.DidNotReceiveWithAnyArgs().ListarAbiertasInactivasAsync(default, default);
        await _conversaciones.DidNotReceiveWithAnyArgs().GuardarConversacionAsync(default!, default);
    }

    private ServicioExpiracionConversaciones Construir(int horas)
        => new(
            _conversaciones,
            new OpcionesConversacion { HorasExpiracionSinRespuesta = horas },
            new RelojFijo(Ahora));
}
