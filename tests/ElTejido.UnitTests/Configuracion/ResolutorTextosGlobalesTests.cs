using ElTejido.Application.Configuracion;
using ElTejido.Domain.Localizacion;
using FluentAssertions;
using NSubstitute;

namespace ElTejido.UnitTests.Configuracion;

public sealed class ResolutorTextosGlobalesTests
{
    [Fact]
    public async Task Runtime_DelegaEnElResolutorExistenteSinReimplementarCacheLkg()
    {
        var runtime = Substitute.For<IResolutorTextosConversacion>();
        var disponibilidad = Substitute.For<IDisponibilidadCatalogoTextos>();
        var textos = new TextosConversacionResueltos(
            "en",
            new Dictionary<string, string>(),
            new Dictionary<string, IReadOnlyCollection<string>>(),
            OrigenTextosConversacion.Cache,
            3,
            "huella");
        runtime.ResolverParaIdiomaAsync("en", Arg.Any<CancellationToken>()).Returns(textos);
        var resolutor = new ResolutorTextosGlobales(runtime, disponibilidad);

        var resultado = await resolutor.ResolverAsync(
            IdiomaConversacion.Ingles,
            ModoResolucionTextosGlobales.Runtime,
            CancellationToken.None);

        resultado.Should().BeOfType<ResultadoTextosGlobales.Disponible>()
            .Which.Textos.Should().BeSameAs(textos);
        await disponibilidad.DidNotReceiveWithAnyArgs()
            .ObtenerIdiomasSinCatalogoActivoAsync(default!, default);
    }

    [Fact]
    public async Task Diagnostico_ConsultaDisponibilidadSinCargarContenidoRuntime()
    {
        var runtime = Substitute.For<IResolutorTextosConversacion>();
        var disponibilidad = Substitute.For<IDisponibilidadCatalogoTextos>();
        disponibilidad.ObtenerIdiomasSinCatalogoActivoAsync(
                Arg.Any<IReadOnlyCollection<string>>(),
                Arg.Any<CancellationToken>())
            .Returns(new[] { "en" });
        var resolutor = new ResolutorTextosGlobales(runtime, disponibilidad);

        var resultado = await resolutor.ResolverAsync(
            IdiomaConversacion.Ingles,
            ModoResolucionTextosGlobales.Diagnostico,
            CancellationToken.None);

        resultado.Should().BeOfType<ResultadoTextosGlobales.NoDisponible>()
            .Which.Codigo.Should().Be("catalogo_activo_faltante");
        await runtime.DidNotReceiveWithAnyArgs().ResolverParaIdiomaAsync(default!, default);
    }
}
