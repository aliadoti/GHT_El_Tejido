using ElTejido.Application.Campanas;
using ElTejido.Application.Common;
using ElTejido.Application.Identidad;
using ElTejido.Application.Participantes;
using ElTejido.Application.Seguridad;
using ElTejido.Application.Usuarios;
using ElTejido.Domain.Campanas;
using ElTejido.Domain.Common;
using ElTejido.Domain.Identidad;
using ElTejido.Domain.Participantes;
using ElTejido.Domain.Seguridad;
using ElTejido.Domain.Usuarios;
using ElTejido.UnitTests.Soporte;
using FluentAssertions;
using NSubstitute;

namespace ElTejido.UnitTests.Identidad;

public sealed class ResolutorParticipanteTests
{
    private const string Numero = "573001119999";

    [Fact]
    public async Task Resolver_NumeroInvalido_NoMatriculadoYRegistra()
    {
        var ctx = new Contexto();
        var resolutor = ctx.Construir();

        var resultado = await resolutor.ResolverAsync("abc", CancellationToken.None);

        DebeRechazar(resultado, MotivoRechazo.NoMatriculado);
        await ctx.DebeHaberRegistradoRechazo();
    }

    [Fact]
    public async Task Resolver_UsuarioDesconocido_NoMatriculado()
    {
        var ctx = new Contexto();
        ctx.Usuarios.ObtenerUsuarioPorNumeroAsync(Arg.Any<NumeroWhatsApp>(), Arg.Any<CancellationToken>())
            .Returns((Usuario?)null);
        var resolutor = ctx.Construir();

        var resultado = await resolutor.ResolverAsync(Numero, CancellationToken.None);

        DebeRechazar(resultado, MotivoRechazo.NoMatriculado);
    }

    [Fact]
    public async Task Resolver_UsuarioInactivo_Inactivo()
    {
        var ctx = new Contexto();
        ctx.Usuarios.ObtenerUsuarioPorNumeroAsync(Arg.Any<NumeroWhatsApp>(), Arg.Any<CancellationToken>())
            .Returns(FabricasDominio.CrearUsuario("u_1", Numero, RolUsuario.Participante, EstadoRegistro.Inactivo));
        var resolutor = ctx.Construir();

        var resultado = await resolutor.ResolverAsync(Numero, CancellationToken.None);

        DebeRechazar(resultado, MotivoRechazo.Inactivo);
    }

    [Fact]
    public async Task Resolver_UsuarioNoParticipante_NoEsParticipante()
    {
        var ctx = new Contexto();
        ctx.Usuarios.ObtenerUsuarioPorNumeroAsync(Arg.Any<NumeroWhatsApp>(), Arg.Any<CancellationToken>())
            .Returns(FabricasDominio.CrearUsuario("u_1", Numero, RolUsuario.Admin));
        var resolutor = ctx.Construir();

        var resultado = await resolutor.ResolverAsync(Numero, CancellationToken.None);

        DebeRechazar(resultado, MotivoRechazo.NoEsParticipante);
    }

    [Fact]
    public async Task Resolver_SinCampaniaActiva_SinCampaniaActiva()
    {
        var ctx = new Contexto();
        ctx.Usuarios.ObtenerUsuarioPorNumeroAsync(Arg.Any<NumeroWhatsApp>(), Arg.Any<CancellationToken>())
            .Returns(FabricasDominio.CrearUsuario("u_1", Numero, RolUsuario.Participante));
        ctx.Participantes.BuscarParticipantesPorNumeroAsync(Arg.Any<NumeroWhatsApp>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ParticipanteCampania>());
        var resolutor = ctx.Construir();

        var resultado = await resolutor.ResolverAsync(Numero, CancellationToken.None);

        DebeRechazar(resultado, MotivoRechazo.SinCampaniaActiva);
    }

    [Fact]
    public async Task Resolver_ParticipanteActivoConCampaniaActiva_AutorizadoConPrimeraPreguntaVigente()
    {
        var ctx = new Contexto();
        ctx.Usuarios.ObtenerUsuarioPorNumeroAsync(Arg.Any<NumeroWhatsApp>(), Arg.Any<CancellationToken>())
            .Returns(FabricasDominio.CrearUsuario("u_1", Numero, RolUsuario.Participante));
        ctx.Participantes.BuscarParticipantesPorNumeroAsync(Arg.Any<NumeroWhatsApp>(), Arg.Any<CancellationToken>())
            .Returns(new[] { FabricasDominio.CrearParticipante("pc_1", "c_1", "u_1", Numero) });
        var campania = FabricasDominio.CrearCampania(
            "c_1",
            EstadoCampania.Activa,
            new[]
            {
                FabricasDominio.CrearPregunta("p_2", 2),
                FabricasDominio.CrearPregunta("p_1", 1),
            });
        ctx.Campanias.ObtenerCampaniaPorIdAsync("c_1", Arg.Any<CancellationToken>()).Returns(campania);
        var resolutor = ctx.Construir();

        var resultado = await resolutor.ResolverAsync(Numero, CancellationToken.None);

        var autorizado = resultado.Should().BeOfType<ResultadoResolucion.Autorizado>().Subject;
        autorizado.Participante.Usuario.Id.Should().Be("u_1");
        autorizado.Participante.Campania.Id.Should().Be("c_1");
        autorizado.Participante.PreguntaVigente.Id.Should().Be("p_1");
    }

    private static void DebeRechazar(ResultadoResolucion resultado, MotivoRechazo motivo)
        => resultado.Should().BeOfType<ResultadoResolucion.NoAutorizado>()
            .Which.Motivo.Should().Be(motivo);

    private sealed class Contexto
    {
        public INormalizadorNumero Normalizador { get; } = new NormalizadorNumero();

        public IRepositorioUsuarios Usuarios { get; } = Substitute.For<IRepositorioUsuarios>();

        public IRepositorioParticipantes Participantes { get; } = Substitute.For<IRepositorioParticipantes>();

        public IRepositorioCampanias Campanias { get; } = Substitute.For<IRepositorioCampanias>();

        public IRepositorioLogSeguridad Logs { get; } = Substitute.For<IRepositorioLogSeguridad>();

        public IProveedorCorrelacion Correlacion { get; } = Substitute.For<IProveedorCorrelacion>();

        public TimeProvider Reloj { get; } = new RelojFijo(new DateTimeOffset(2026, 6, 13, 0, 0, 0, TimeSpan.Zero));

        public ResolutorParticipante Construir() => new(
            Normalizador,
            Usuarios,
            Participantes,
            Campanias,
            Logs,
            Correlacion,
            Reloj);

        public Task DebeHaberRegistradoRechazo()
            => Logs.Received(1).RegistrarAsync(
                Arg.Is<LogSeguridad>(l => l.TipoEvento == TipoEventoSeguridad.RechazoParticipacion && l.Resultado == "rechazado"),
                Arg.Any<CancellationToken>());
    }
}
