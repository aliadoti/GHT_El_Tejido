using ElTejido.Application.Conversacion;
using ElTejido.Application.Diagnostico;
using ElTejido.Infrastructure.Diagnostico;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace ElTejido.UnitTests.Diagnostico;

public sealed class ComprobacionUmbralResumenConsolidacionTests
{
    [Fact]
    public async Task ResumenApagado_SeReportaComoNoAplica()
    {
        var resultado = await Comprobar(new OpcionesConversacion()).ComprobarAsync(CancellationToken.None);

        resultado.Should().ContainSingle()
            .Which.Estado.Should().Be(EstadoPreparacion.NoAplica);
    }

    [Fact]
    public async Task UmbralResumenIgualAlBase_AdvierteSinRomperElArranque()
    {
        var resultado = await Comprobar(new OpcionesConversacion
        {
            ResumenConsolidacionHabilitado = true,
            UmbralResumenConsolidacion = 0.6,
            UmbralCierreAnticipado = 0.6,
        }).ComprobarAsync(CancellationToken.None);

        resultado.Should().ContainSingle()
            .Which.Estado.Should().Be(EstadoPreparacion.Faltante);
    }

    [Fact]
    public async Task UmbralResumenMenorAlBase_SeReportaComoCompatible()
    {
        var resultado = await Comprobar(new OpcionesConversacion
        {
            ResumenConsolidacionHabilitado = true,
            UmbralResumenConsolidacion = 0.4,
            UmbralCierreAnticipado = 0.6,
        }).ComprobarAsync(CancellationToken.None);

        resultado.Should().ContainSingle()
            .Which.Estado.Should().Be(EstadoPreparacion.Ok);
    }

    private static ComprobacionUmbralResumenConsolidacion Comprobar(OpcionesConversacion opciones)
        => new(Options.Create(opciones));
}
