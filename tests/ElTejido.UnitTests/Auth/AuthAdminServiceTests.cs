using ElTejido.Application.Auth;
using ElTejido.Application.Common;
using ElTejido.Application.Seguridad;
using ElTejido.Application.Usuarios;
using ElTejido.Domain.Identidad;
using ElTejido.Domain.Seguridad;
using ElTejido.Domain.Usuarios;
using ElTejido.UnitTests.Soporte;
using FluentAssertions;
using NSubstitute;

namespace ElTejido.UnitTests.Auth;

public sealed class AuthAdminServiceTests
{
    private const string NumeroCrudo = "+57 300 111 9999";
    private const string NumeroNormalizado = "573001119999";

    [Fact]
    public async Task SolicitarCodigo_AdminActivo_GeneraGuardaNotificaYRegistraEnviado()
    {
        var ctx = new Contexto();
        ctx.Usuarios.ObtenerUsuarioPorNumeroAsync(Arg.Any<NumeroWhatsApp>(), Arg.Any<CancellationToken>())
            .Returns(FabricasDominio.CrearUsuario("u_admin1", NumeroNormalizado, RolUsuario.Admin));
        var servicio = ctx.Construir();

        await servicio.SolicitarCodigoAsync(NumeroCrudo, CancellationToken.None);

        await ctx.Codigos.Received(1).GuardarAsync(Arg.Any<CodigoAuthAdmin>(), Arg.Any<CancellationToken>());
        await ctx.Notificador.Received(1).EnviarCodigoAsync(Arg.Any<NumeroWhatsApp>(), "482913", Arg.Any<CancellationToken>());
        await RecibioLog(ctx, TipoEventoSeguridad.SolicitudOtp, "enviado");
    }

    [Fact]
    public async Task SolicitarCodigo_NumeroDesconocido_NoGeneraNiNotifica()
    {
        var ctx = new Contexto();
        ctx.Usuarios.ObtenerUsuarioPorNumeroAsync(Arg.Any<NumeroWhatsApp>(), Arg.Any<CancellationToken>())
            .Returns((Usuario?)null);
        var servicio = ctx.Construir();

        await servicio.SolicitarCodigoAsync(NumeroCrudo, CancellationToken.None);

        await ctx.Codigos.DidNotReceive().GuardarAsync(Arg.Any<CodigoAuthAdmin>(), Arg.Any<CancellationToken>());
        await ctx.Notificador.DidNotReceive().EnviarCodigoAsync(Arg.Any<NumeroWhatsApp>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await RecibioLog(ctx, TipoEventoSeguridad.SolicitudOtp, "ignorado");
    }

    [Fact]
    public async Task SolicitarCodigo_UsuarioNoAdministrativo_Ignora()
    {
        var ctx = new Contexto();
        ctx.Usuarios.ObtenerUsuarioPorNumeroAsync(Arg.Any<NumeroWhatsApp>(), Arg.Any<CancellationToken>())
            .Returns(FabricasDominio.CrearUsuario("u_part1", NumeroNormalizado, RolUsuario.Participante));
        var servicio = ctx.Construir();

        await servicio.SolicitarCodigoAsync(NumeroCrudo, CancellationToken.None);

        await ctx.Codigos.DidNotReceive().GuardarAsync(Arg.Any<CodigoAuthAdmin>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SolicitarCodigo_LimiteExcedido_RegistraRateLimitYNoGenera()
    {
        var ctx = new Contexto();
        ctx.Usuarios.ObtenerUsuarioPorNumeroAsync(Arg.Any<NumeroWhatsApp>(), Arg.Any<CancellationToken>())
            .Returns(FabricasDominio.CrearUsuario("u_admin1", NumeroNormalizado, RolUsuario.Admin));
        ctx.Limitador.RegistrarYPermitirAsync(Arg.Any<NumeroWhatsApp>(), Arg.Any<CancellationToken>()).Returns(false);
        var servicio = ctx.Construir();

        await servicio.SolicitarCodigoAsync(NumeroCrudo, CancellationToken.None);

        await ctx.Codigos.DidNotReceive().GuardarAsync(Arg.Any<CodigoAuthAdmin>(), Arg.Any<CancellationToken>());
        await RecibioLog(ctx, TipoEventoSeguridad.RateLimit, "limitado");
    }

    [Fact]
    public async Task VerificarCodigo_SinCodigoVigente_DevuelveNullYRegistraFallido()
    {
        var ctx = new Contexto();
        ctx.Codigos.ObtenerVigenteMasRecienteAsync(Arg.Any<NumeroWhatsApp>(), Arg.Any<CancellationToken>())
            .Returns((CodigoAuthAdmin?)null);
        var servicio = ctx.Construir();

        var sesion = await servicio.VerificarCodigoAsync(NumeroCrudo, "482913", CancellationToken.None);

        sesion.Should().BeNull();
        await RecibioLog(ctx, TipoEventoSeguridad.LoginFallido, "fallido");
    }

    [Fact]
    public async Task VerificarCodigo_CodigoIncorrecto_ConsumeIntentoYDevuelveNull()
    {
        var ctx = new Contexto();
        ctx.Codigos.ObtenerVigenteMasRecienteAsync(Arg.Any<NumeroWhatsApp>(), Arg.Any<CancellationToken>())
            .Returns(ctx.CrearCodigoVigente("u_admin1"));
        ctx.Hasher.Verificar(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(false);
        var servicio = ctx.Construir();

        var sesion = await servicio.VerificarCodigoAsync(NumeroCrudo, "000000", CancellationToken.None);

        sesion.Should().BeNull();
        await ctx.Codigos.Received(1).GuardarAsync(
            Arg.Is<CodigoAuthAdmin>(c => c.IntentosRestantes == 4 && !c.Usado),
            Arg.Any<CancellationToken>());
        await RecibioLog(ctx, TipoEventoSeguridad.LoginFallido, "fallido");
    }

    [Fact]
    public async Task VerificarCodigo_Correcto_MarcaUsadoEmiteSesionYRegistraExito()
    {
        var ctx = new Contexto();
        ctx.Codigos.ObtenerVigenteMasRecienteAsync(Arg.Any<NumeroWhatsApp>(), Arg.Any<CancellationToken>())
            .Returns(ctx.CrearCodigoVigente("u_admin1"));
        ctx.Hasher.Verificar(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        ctx.Usuarios.ObtenerUsuarioPorIdAsync("u_admin1", Arg.Any<CancellationToken>())
            .Returns(FabricasDominio.CrearUsuario("u_admin1", NumeroNormalizado, RolUsuario.Admin, nombre: "Admin"));
        var esperada = new SesionEmitida("token", "csrf", ctx.Reloj.GetUtcNow().AddMinutes(60), new UsuarioSesion("u_admin1", "Admin", "admin"));
        ctx.Sesion.EmitirAsync(Arg.Any<Usuario>(), Arg.Any<CancellationToken>()).Returns(esperada);
        var servicio = ctx.Construir();

        var sesion = await servicio.VerificarCodigoAsync(NumeroCrudo, "482913", CancellationToken.None);

        sesion.Should().BeSameAs(esperada);
        await ctx.Codigos.Received(1).GuardarAsync(Arg.Is<CodigoAuthAdmin>(c => c.Usado), Arg.Any<CancellationToken>());
        await ctx.Sesion.Received(1).EmitirAsync(Arg.Any<Usuario>(), Arg.Any<CancellationToken>());
        await RecibioLog(ctx, TipoEventoSeguridad.LoginExitoso, "exitoso");
    }

    private static Task RecibioLog(Contexto ctx, TipoEventoSeguridad tipo, string resultado)
        => ctx.Logs.Received(1).RegistrarAsync(
            Arg.Is<LogSeguridad>(l => l.TipoEvento == tipo && l.Resultado == resultado),
            Arg.Any<CancellationToken>());

    private sealed class Contexto
    {
        public INormalizadorNumero Normalizador { get; } = new NormalizadorNumero();

        public IRepositorioUsuarios Usuarios { get; } = Substitute.For<IRepositorioUsuarios>();

        public IRepositorioCodigosAuth Codigos { get; } = Substitute.For<IRepositorioCodigosAuth>();

        public IRepositorioLogSeguridad Logs { get; } = Substitute.For<IRepositorioLogSeguridad>();

        public ISecretProvider Secretos { get; } = Substitute.For<ISecretProvider>();

        public IHasherOtp Hasher { get; } = Substitute.For<IHasherOtp>();

        public IGeneradorCodigoOtp Generador { get; } = Substitute.For<IGeneradorCodigoOtp>();

        public INotificadorOtp Notificador { get; } = Substitute.For<INotificadorOtp>();

        public ILimitadorOtp Limitador { get; } = Substitute.For<ILimitadorOtp>();

        public IServicioSesion Sesion { get; } = Substitute.For<IServicioSesion>();

        public IProveedorCorrelacion Correlacion { get; } = Substitute.For<IProveedorCorrelacion>();

        public OpcionesAuth Opciones { get; } = new();

        public RelojFijo Reloj { get; } = new(new DateTimeOffset(2026, 6, 13, 0, 0, 0, TimeSpan.Zero));

        public Contexto()
        {
            Generador.Generar(Arg.Any<int>()).Returns("482913");
            Secretos.ObtenerSecretoAsync(NombresSecretos.OtpSalt, Arg.Any<CancellationToken>()).Returns("pepper");
            Hasher.Hashear(Arg.Any<string>(), Arg.Any<string>()).Returns("$hash$");
            Limitador.RegistrarYPermitirAsync(Arg.Any<NumeroWhatsApp>(), Arg.Any<CancellationToken>()).Returns(true);
        }

        public CodigoAuthAdmin CrearCodigoVigente(string usuarioId)
        {
            var ahora = Reloj.GetUtcNow();
            return CodigoAuthAdmin.Crear(
                "cod_1",
                usuarioId,
                NumeroWhatsApp.FromNormalized(NumeroNormalizado),
                "$hash$",
                ahora.AddMinutes(5),
                intentosRestantes: 5,
                usado: false,
                ahora,
                ttl: 300);
        }

        public AuthAdminService Construir() => new(
            Normalizador,
            Usuarios,
            Codigos,
            Logs,
            Secretos,
            Hasher,
            Generador,
            Notificador,
            Limitador,
            Sesion,
            Correlacion,
            Opciones,
            Reloj);
    }
}
