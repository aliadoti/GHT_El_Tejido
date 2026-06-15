using ElTejido.Application.Diagnostico;
using FluentAssertions;

namespace ElTejido.UnitTests.Diagnostico;

/// <summary>
/// Verifica la consolidacion del reporte de preparacion: Error domina sobre Faltante, NoAplica no
/// afecta, y una comprobacion que lanza pese al contrato se reporta como Error sin interrumpir.
/// </summary>
public sealed class ServicioPreparacionTests
{
    [Fact]
    public async Task TodoOk_AgregadoOk()
    {
        var servicio = new ServicioPreparacion(new[]
        {
            Comprobacion(("a", EstadoPreparacion.Ok), ("b", EstadoPreparacion.NoAplica)),
        });

        var reporte = await servicio.GenerarReporteAsync(CancellationToken.None);

        reporte.Estado.Should().Be(EstadoPreparacion.Ok);
        reporte.Componentes.Should().HaveCount(2);
    }

    [Fact]
    public async Task ConFaltante_AgregadoFaltante()
    {
        var servicio = new ServicioPreparacion(new[]
        {
            Comprobacion(("a", EstadoPreparacion.Ok), ("b", EstadoPreparacion.Faltante)),
        });

        var reporte = await servicio.GenerarReporteAsync(CancellationToken.None);

        reporte.Estado.Should().Be(EstadoPreparacion.Faltante);
    }

    [Fact]
    public async Task ErrorDominaSobreFaltante()
    {
        var servicio = new ServicioPreparacion(new[]
        {
            Comprobacion(("a", EstadoPreparacion.Faltante)),
            Comprobacion(("b", EstadoPreparacion.Error)),
        });

        var reporte = await servicio.GenerarReporteAsync(CancellationToken.None);

        reporte.Estado.Should().Be(EstadoPreparacion.Error);
    }

    [Fact]
    public async Task SoloNoAplica_AgregadoOk()
    {
        var servicio = new ServicioPreparacion(new[]
        {
            Comprobacion(("a", EstadoPreparacion.NoAplica)),
        });

        var reporte = await servicio.GenerarReporteAsync(CancellationToken.None);

        reporte.Estado.Should().Be(EstadoPreparacion.Ok);
    }

    [Fact]
    public async Task ComprobacionQueLanza_SeReportaComoError()
    {
        var servicio = new ServicioPreparacion(new IComprobacionPreparacion[]
        {
            Comprobacion(("a", EstadoPreparacion.Ok)),
            new ComprobacionQueLanza(),
        });

        var reporte = await servicio.GenerarReporteAsync(CancellationToken.None);

        reporte.Estado.Should().Be(EstadoPreparacion.Error);
        reporte.Componentes.Should().Contain(c => c.Estado == EstadoPreparacion.Error);
    }

    private static IComprobacionPreparacion Comprobacion(params (string Nombre, EstadoPreparacion Estado)[] resultados)
        => new ComprobacionFalsa(resultados
            .Select(r => new ResultadoComprobacion(r.Nombre, r.Estado, "prueba"))
            .ToArray());

    private sealed class ComprobacionFalsa : IComprobacionPreparacion
    {
        private readonly IReadOnlyList<ResultadoComprobacion> _resultados;

        public ComprobacionFalsa(IReadOnlyList<ResultadoComprobacion> resultados) => _resultados = resultados;

        public Task<IReadOnlyList<ResultadoComprobacion>> ComprobarAsync(CancellationToken cancellationToken)
            => Task.FromResult(_resultados);
    }

    private sealed class ComprobacionQueLanza : IComprobacionPreparacion
    {
        public Task<IReadOnlyList<ResultadoComprobacion>> ComprobarAsync(CancellationToken cancellationToken)
            => throw new InvalidOperationException("fallo inesperado");
    }
}
