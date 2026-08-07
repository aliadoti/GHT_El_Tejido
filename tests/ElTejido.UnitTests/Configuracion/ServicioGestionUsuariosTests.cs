using ElTejido.Application.Common;
using ElTejido.Application.Configuracion;
using ElTejido.Application.Usuarios;
using ElTejido.Domain.Common;
using ElTejido.Domain.Identidad;
using ElTejido.Domain.Usuarios;
using FluentAssertions;
using NSubstitute;

namespace ElTejido.UnitTests.Configuracion;

public sealed class ServicioGestionUsuariosTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 6, 13, 19, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CrearUsuario_NormalizaNumeroYGuardaUsuarioActivo()
    {
        var repositorio = Substitute.For<IRepositorioUsuarios>();
        Usuario? guardado = null;
        repositorio
            .GuardarUsuarioAsync(Arg.Do<Usuario>(u => guardado = u), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        repositorio
            .ReservarCodigosUsuarioAsync(1, Arg.Any<CancellationToken>())
            .Returns(131);
        var servicio = CrearServicio(repositorio);

        var usuario = await servicio.CrearUsuarioAsync(
            new SolicitudCrearUsuario(
                "Ana Perez",
                "+57 300 111 2233",
                RolUsuario.Participante,
                EstadoRegistro.Activo,
                "Operaciones",
                "GHT",
                ["t_area_oper"],
                new Dictionary<string, object?> { ["cargo"] = "Coordinadora" }),
            CancellationToken.None);

        usuario.Should().BeSameAs(guardado);
        usuario.Id.Should().StartWith("u_");
        usuario.WhatsappNormalizado.Valor.Should().Be("573001112233");
        usuario.Estado.Should().Be(EstadoRegistro.Activo);
        usuario.CreadoEn.Should().Be(Ahora);
        // El codigo legible lo asigna la secuencia del maestro, no el cliente (03 §3.1.1).
        usuario.CodigoUsuario.Should().Be(131);
        usuario.CodigoUsuarioLegible.Should().Be("U-000131");
        usuario.Idioma.Should().Be("es");
        await repositorio.Received(1).ReservarCodigosUsuarioAsync(1, Arg.Any<CancellationToken>());
        await repositorio.Received(1).GuardarUsuarioAsync(usuario, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ActualizarUsuario_ConservaCodigoUsuarioYCamposDelMaestro()
    {
        var existente = Usuario.Crear(
            "u_1",
            77,
            "Usuario",
            NumeroWhatsApp.FromNormalized("573001112233"),
            RolUsuario.Participante,
            EstadoRegistro.Activo,
            "Operaciones",
            "GHT",
            null,
            null,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch,
            usuarioWhatsapp: "ana.perez",
            empresaId: "AL",
            sede: "FF - ADM",
            cargo: "Gerente",
            email: "ana@ght.com",
            antiguedadAnios: 16.391666m,
            idioma: "en");
        var repositorio = Substitute.For<IRepositorioUsuarios>();
        repositorio.ObtenerUsuarioPorIdAsync("u_1", Arg.Any<CancellationToken>()).Returns(existente);
        var servicio = CrearServicio(repositorio);

        var actualizado = await servicio.ActualizarUsuarioAsync(
            "u_1",
            new SolicitudActualizarUsuario("Ana Nueva", null, null, null, null, null, null, null),
            CancellationToken.None);

        actualizado.Nombre.Should().Be("Ana Nueva");
        actualizado.CodigoUsuario.Should().Be(77);
        actualizado.UsuarioWhatsapp.Should().Be("ana.perez");
        actualizado.EmpresaId.Should().Be("AL");
        actualizado.Sede.Should().Be("FF - ADM");
        actualizado.Cargo.Should().Be("Gerente");
        actualizado.Email.Should().Be("ana@ght.com");
        actualizado.AntiguedadAnios.Should().Be(16.391666m);
        actualizado.Idioma.Should().Be("en");
        await repositorio.DidNotReceive().ReservarCodigosUsuarioAsync(
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CrearUsuario_NumeroDuplicado_LanzaConflicto()
    {
        var repositorio = Substitute.For<IRepositorioUsuarios>();
        repositorio
            .ObtenerUsuarioPorNumeroAsync(Arg.Any<NumeroWhatsApp>(), Arg.Any<CancellationToken>())
            .Returns(CrearUsuario("u_existente", "573001112233"));
        var servicio = CrearServicio(repositorio);

        var act = () => servicio.CrearUsuarioAsync(
            new SolicitudCrearUsuario(
                "Ana",
                "573001112233",
                RolUsuario.Participante,
                EstadoRegistro.Activo,
                "Operaciones",
                "GHT",
                null,
                null),
            CancellationToken.None);

        await act.Should().ThrowAsync<ErrorConflicto>();
        await repositorio.DidNotReceive().GuardarUsuarioAsync(Arg.Any<Usuario>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ActualizarUsuario_NumeroDeOtroUsuario_LanzaConflicto()
    {
        var usuario = CrearUsuario("u_1", "573001112233");
        var otro = CrearUsuario("u_2", "573009998888");
        var repositorio = Substitute.For<IRepositorioUsuarios>();
        repositorio.ObtenerUsuarioPorIdAsync("u_1", Arg.Any<CancellationToken>()).Returns(usuario);
        repositorio
            .ObtenerUsuarioPorNumeroAsync(NumeroWhatsApp.FromNormalized("573009998888"), Arg.Any<CancellationToken>())
            .Returns(otro);
        var servicio = CrearServicio(repositorio);

        var act = () => servicio.ActualizarUsuarioAsync(
            "u_1",
            new SolicitudActualizarUsuario(null, "573009998888", null, null, null, null, null, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<ErrorConflicto>();
        await repositorio.DidNotReceive().GuardarUsuarioAsync(Arg.Any<Usuario>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CambiarEstadoTag_InactivaPreservandoIdentidad()
    {
        var tag = Tag.Crear("t_area_oper", "Operaciones", "area", "Area", EstadoRegistro.Activo, DateTimeOffset.UnixEpoch);
        var repositorio = Substitute.For<IRepositorioUsuarios>();
        Tag? guardado = null;
        repositorio.ObtenerTagPorIdAsync("t_area_oper", Arg.Any<CancellationToken>()).Returns(tag);
        repositorio
            .GuardarTagAsync(Arg.Do<Tag>(t => guardado = t), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var servicio = CrearServicio(repositorio);

        var actualizado = await servicio.CambiarEstadoTagAsync(
            "t_area_oper",
            EstadoRegistro.Inactivo,
            CancellationToken.None);

        actualizado.Should().BeSameAs(guardado);
        actualizado.Id.Should().Be("t_area_oper");
        actualizado.Estado.Should().Be(EstadoRegistro.Inactivo);
        actualizado.CreadoEn.Should().Be(DateTimeOffset.UnixEpoch);
    }

    private static ServicioGestionUsuarios CrearServicio(IRepositorioUsuarios repositorio)
        => new(repositorio, new NormalizadorNumero(), new TimeProviderFijo(Ahora));

    private static Usuario CrearUsuario(string id, string numero)
        => Usuario.Crear(
            id,
            1,
            "Usuario",
            NumeroWhatsApp.FromNormalized(numero),
            RolUsuario.Participante,
            EstadoRegistro.Activo,
            "Operaciones",
            "GHT",
            null,
            null,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch);

    private sealed class TimeProviderFijo : TimeProvider
    {
        private readonly DateTimeOffset _ahora;

        public TimeProviderFijo(DateTimeOffset ahora) => _ahora = ahora;

        public override DateTimeOffset GetUtcNow() => _ahora;
    }
}
