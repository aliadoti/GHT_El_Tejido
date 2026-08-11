using ElTejido.Application.Configuracion;
using ElTejido.Application.Conversacion;
using ElTejido.Application.Seguridad;
using ElTejido.Domain.Seguridad;
using FluentAssertions;
using NSubstitute;

namespace ElTejido.UnitTests.Configuracion;

public sealed class ProveedorTextosConversacionTests
{
    [Fact]
    public async Task Runtime_GateApagado_ConservaLegacySinConsultarRepositorio()
    {
        var repositorio = Substitute.For<IRepositorioCatalogosTextos>();
        var proveedor = CrearProveedor(repositorio, habilitado: false);

        var resultado = await proveedor.ObtenerParaRuntimeAsync("es", CancellationToken.None);

        resultado.Origen.Should().Be(OrigenTextosConversacion.Legacy);
        resultado.Version.Should().BeNull();
        await repositorio.DidNotReceive().ObtenerActivoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Preview_CatalogoValido_UsaCacheHastaQueExpire()
    {
        var activo = CatalogosTextosSemilla.CrearVersionEmergencia("es");
        var repositorio = Substitute.For<IRepositorioCatalogosTextos>();
        repositorio.ObtenerActivoAsync("es", Arg.Any<CancellationToken>()).Returns(activo);
        var proveedor = CrearProveedor(repositorio, habilitado: false);

        var primera = await proveedor.PrevisualizarAsync("es", CancellationToken.None);
        var segunda = await proveedor.PrevisualizarAsync("es", CancellationToken.None);

        primera.Origen.Should().Be(OrigenTextosConversacion.Catalogo);
        segunda.Origen.Should().Be(OrigenTextosConversacion.Cache);
        segunda.Version.Should().BeSameAs(activo);
        await repositorio.Received(1).ObtenerActivoAsync("es", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Refresh_FallaDespuesDeUnaLecturaValida_UsaUltimaVersionValida()
    {
        var activo = CatalogosTextosSemilla.CrearVersionEmergencia("es");
        var repositorio = Substitute.For<IRepositorioCatalogosTextos>();
        repositorio.ObtenerActivoAsync("es", Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult<VersionCatalogoTextos?>(activo),
                Task.FromException<VersionCatalogoTextos?>(new InvalidOperationException("Cosmos no disponible")));
        var reloj = new RelojMutable(DateTimeOffset.Parse("2026-08-10T12:00:00Z"));
        var logs = Substitute.For<IRepositorioLogSeguridad>();
        var proveedor = CrearProveedor(repositorio, habilitado: true, reloj, logs, cacheSegundos: 60);
        await proveedor.ObtenerParaRuntimeAsync("es", CancellationToken.None);
        reloj.Avanzar(TimeSpan.FromSeconds(61));

        var resultado = await proveedor.ObtenerParaRuntimeAsync("es", CancellationToken.None);

        resultado.Origen.Should().Be(OrigenTextosConversacion.UltimaVersionValida);
        resultado.Version.Should().BeSameAs(activo);
        await logs.Received().RegistrarAsync(
            Arg.Is<LogSeguridad>(x =>
                x.Resultado == "fallbackRuntime"
                && x.Detalle == "idioma=es;origen=ultimaVersionValida;motivo=lecturaOValidacion"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SinActivoNiLkg_UsaEmergenciaDelMismoIdioma()
    {
        var repositorio = Substitute.For<IRepositorioCatalogosTextos>();
        repositorio.ObtenerActivoAsync("en", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<VersionCatalogoTextos?>(null));
        var proveedor = CrearProveedor(repositorio, habilitado: true);

        var resultado = await proveedor.ObtenerParaRuntimeAsync("en", CancellationToken.None);

        resultado.Origen.Should().Be(OrigenTextosConversacion.Emergencia);
        resultado.Version!.Catalogo.Idioma.Should().Be("en");
        resultado.Version.Catalogo.Mensajes["saludoPrimerContacto"].Should().StartWith("Hello!");
    }

    [Theory]
    [InlineData("es")]
    [InlineData("en")]
    public void Semilla_EsCompletaYValida(string idioma)
    {
        var semilla = CatalogosTextosSemilla.CrearSolicitud(idioma);

        semilla.Mensajes.Keys.Should().BeEquivalentTo(ValidadorCatalogoTextosConversacion.ClavesMensajes);
        semilla.Frases.Keys.Should().BeEquivalentTo(ValidadorCatalogoTextosConversacion.ClavesFrases);
        ValidadorCatalogoTextosConversacion.ValidarYCalcularHuella(semilla.Mensajes, semilla.Frases)
            .Should().HaveLength(64);
    }

    [Fact]
    public void SemillaEspanol_FotografiaLosValoresEfectivosConfigurados()
    {
        var opciones = new OpcionesConversacion
        {
            FrasesContinuar = new List<string> { "adelante con la siguiente" },
            Mensajes = new OpcionesMensajesConversacion
            {
                SaludoPrimerContacto = "Saludo efectivo del ambiente",
            },
        };

        var semilla = CatalogosTextosSemilla.CrearSolicitud("es", opciones);

        semilla.Mensajes["saludoPrimerContacto"].Should().Be("Saludo efectivo del ambiente");
        semilla.Frases["continuar"].Should().Equal("adelante con la siguiente");
    }

    private static ProveedorTextosConversacion CrearProveedor(
        IRepositorioCatalogosTextos repositorio,
        bool habilitado,
        TimeProvider? reloj = null,
        IRepositorioLogSeguridad? logs = null,
        int cacheSegundos = 60)
        => new(
            repositorio,
            logs ?? Substitute.For<IRepositorioLogSeguridad>(),
            new OpcionesCatalogoTextos { Habilitado = habilitado, CacheSegundos = cacheSegundos },
            reloj ?? TimeProvider.System);

    private sealed class RelojMutable : TimeProvider
    {
        private DateTimeOffset _ahora;

        public RelojMutable(DateTimeOffset ahora)
        {
            _ahora = ahora;
        }

        public override DateTimeOffset GetUtcNow() => _ahora;

        public void Avanzar(TimeSpan intervalo) => _ahora = _ahora.Add(intervalo);
    }
}
