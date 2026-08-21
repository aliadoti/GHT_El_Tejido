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
            idioma: "en",
            nombreSaludo: "Anita");
        var repositorio = Substitute.For<IRepositorioUsuarios>();
        repositorio.ObtenerUsuarioPorIdAsync("u_1", Arg.Any<CancellationToken>()).Returns(existente);
        var servicio = CrearServicio(repositorio);

        var actualizado = await servicio.ActualizarUsuarioAsync(
            "u_1",
            new SolicitudActualizarUsuario("Ana Nueva", null, null, null, null, null, null, null),
            CancellationToken.None);

        actualizado.Nombre.Should().Be("Ana Nueva");
        actualizado.NombreSaludo.Should().Be("Anita");
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
    public async Task CrearYActualizarUsuario_PermiteCorregirNombreSaludo()
    {
        var repositorio = Substitute.For<IRepositorioUsuarios>();
        repositorio.ReservarCodigosUsuarioAsync(1, Arg.Any<CancellationToken>()).Returns(1);
        var servicio = CrearServicio(repositorio);
        var creado = await servicio.CrearUsuarioAsync(
            new SolicitudCrearUsuario(
                "ARENAS CHAVES JUAN PABLO",
                "573001112233",
                RolUsuario.Participante,
                EstadoRegistro.Activo,
                null,
                null,
                null,
                null),
            CancellationToken.None);
        repositorio.ObtenerUsuarioPorIdAsync(creado.Id, Arg.Any<CancellationToken>()).Returns(creado);

        var corregido = await servicio.ActualizarUsuarioAsync(
            creado.Id,
            new SolicitudActualizarUsuario(
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                NombreSaludo: "Juan"),
            CancellationToken.None);

        creado.Nombre.Should().Be("ARENAS CHAVES JUAN PABLO");
        creado.NombreSaludo.Should().Be("Juan Pablo");
        corregido.Nombre.Should().Be("ARENAS CHAVES JUAN PABLO");
        corregido.NombreSaludo.Should().Be("Juan");
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

    // --- I-08 v2: campos del maestro, unicidad de email y reasignacion manual (04 §5.1) ---

    [Fact]
    public async Task CrearUsuario_GuardaLosCamposDeLaPlantillaOficial()
    {
        var repositorio = Substitute.For<IRepositorioUsuarios>();
        repositorio.ReservarCodigosUsuarioAsync(1, Arg.Any<CancellationToken>()).Returns(5);
        var servicio = CrearServicio(repositorio);

        var usuario = await servicio.CrearUsuarioAsync(
            new SolicitudCrearUsuario(
                "ANA PEREZ",
                "573001112233",
                RolUsuario.Participante,
                EstadoRegistro.Activo,
                Area: null,
                Empresa: "Flores El Aljibe",
                null,
                null,
                Email: "Ana.Perez@GHT.com",
                EmpresaId: "AL",
                Sede: "FF - ADM",
                Cargo: "Coordinadora",
                AntiguedadAnios: 16.391666m,
                Idioma: "en",
                UsuarioWhatsapp: "ana.perez"),
            CancellationToken.None);

        usuario.Area.Should().BeNull(); // area y empresa ya no son obligatorios (I-08 §3.1.h).
        usuario.Empresa.Should().Be("Flores El Aljibe");
        usuario.EmpresaId.Should().Be("AL");
        usuario.Sede.Should().Be("FF - ADM");
        usuario.Cargo.Should().Be("Coordinadora");
        usuario.Email.Should().Be("ana.perez@ght.com");
        usuario.AntiguedadAnios.Should().Be(16.391666m);
        usuario.Idioma.Should().Be("en");
        usuario.UsuarioWhatsapp.Should().Be("ana.perez");
    }

    [Fact]
    public async Task CrearUsuario_EmailDeOtroActivo_LanzaConflicto()
    {
        var repositorio = Substitute.For<IRepositorioUsuarios>();
        repositorio
            .BuscarUsuariosAsync(Arg.Any<FiltroUsuarios>(), Arg.Any<CancellationToken>())
            .Returns(new[] { CrearUsuario("u_otro", "573009998877", email: "ana@ght.com") });
        var servicio = CrearServicio(repositorio);

        var act = () => servicio.CrearUsuarioAsync(
            new SolicitudCrearUsuario(
                "Ana", "573001112233", RolUsuario.Participante, EstadoRegistro.Activo,
                null, null, null, null, Email: "ANA@ght.com"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ErrorConflicto>();
        await repositorio.DidNotReceive().GuardarUsuarioAsync(Arg.Any<Usuario>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ActualizarUsuario_ConservaSuPropioEmailSinChocarConsigoMismo()
    {
        var existente = CrearUsuario("u_1", "573001112233", email: "ana@ght.com");
        var repositorio = Substitute.For<IRepositorioUsuarios>();
        repositorio.ObtenerUsuarioPorIdAsync("u_1", Arg.Any<CancellationToken>()).Returns(existente);
        repositorio
            .BuscarUsuariosAsync(Arg.Any<FiltroUsuarios>(), Arg.Any<CancellationToken>())
            .Returns(new[] { existente });
        var servicio = CrearServicio(repositorio);

        var actualizado = await servicio.ActualizarUsuarioAsync(
            "u_1",
            new SolicitudActualizarUsuario(null, null, null, null, null, null, null, null, Email: "ana@ght.com"),
            CancellationToken.None);

        actualizado.Email.Should().Be("ana@ght.com");
    }

    [Fact]
    public async Task ActualizarUsuario_ReactivarConTitularActivoEnEseNumero_LanzaConflicto()
    {
        // ADM-08c: reactivar a un inactivo cuyo numero ya tiene titular activo dejaria dos activos.
        var inactivo = CrearUsuario("u_viejo", "573001112233", EstadoRegistro.Inactivo);
        var repositorio = Substitute.For<IRepositorioUsuarios>();
        repositorio.ObtenerUsuarioPorIdAsync("u_viejo", Arg.Any<CancellationToken>()).Returns(inactivo);
        repositorio
            .ObtenerUsuarioPorNumeroAsync(Arg.Any<NumeroWhatsApp>(), Arg.Any<CancellationToken>())
            .Returns(CrearUsuario("u_nuevo", "573001112233"));
        var servicio = CrearServicio(repositorio);

        var act = () => servicio.CambiarEstadoUsuarioAsync(
            "u_viejo",
            EstadoRegistro.Activo,
            CancellationToken.None);

        await act.Should().ThrowAsync<ErrorConflicto>();
        await repositorio.DidNotReceive().GuardarUsuarioAsync(Arg.Any<Usuario>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ActualizarUsuario_ReactivarSinOtroTitular_Funciona()
    {
        var inactivo = CrearUsuario("u_viejo", "573001112233", EstadoRegistro.Inactivo);
        var repositorio = Substitute.For<IRepositorioUsuarios>();
        repositorio.ObtenerUsuarioPorIdAsync("u_viejo", Arg.Any<CancellationToken>()).Returns(inactivo);
        var servicio = CrearServicio(repositorio);

        var actualizado = await servicio.CambiarEstadoUsuarioAsync(
            "u_viejo",
            EstadoRegistro.Activo,
            CancellationToken.None);

        actualizado.Estado.Should().Be(EstadoRegistro.Activo);
    }

    [Fact]
    public async Task ReasignarNumero_InactivaAlTitularYCreaAlNuevoConservandoElNumero()
    {
        var anterior = CrearUsuario("u_1", "573001112233");
        var repositorio = Substitute.For<IRepositorioUsuarios>();
        repositorio.ObtenerUsuarioPorIdAsync("u_1", Arg.Any<CancellationToken>()).Returns(anterior);
        repositorio.ReservarCodigosUsuarioAsync(1, Arg.Any<CancellationToken>()).Returns(9);
        var guardados = new List<Usuario>();
        await repositorio.GuardarUsuarioAsync(Arg.Do<Usuario>(guardados.Add), Arg.Any<CancellationToken>());
        var servicio = CrearServicio(repositorio);

        var resultado = await servicio.ReasignarNumeroAsync(
            "u_1",
            new SolicitudReasignarNumero("CARLOS RODRIGUEZ", EmpresaId: "AL"),
            CancellationToken.None);

        // Orden obligatorio: primero inactivar, luego crear (03 §3.1).
        guardados.Should().HaveCount(2);
        guardados[0].Id.Should().Be("u_1");
        guardados[0].Estado.Should().Be(EstadoRegistro.Inactivo);
        guardados[0].WhatsappNormalizado.Valor.Should().Be("573001112233");
        guardados[1].Should().BeSameAs(resultado.Nuevo);

        resultado.Nuevo.Id.Should().NotBe("u_1");
        resultado.Nuevo.CodigoUsuario.Should().Be(9);
        resultado.Nuevo.Estado.Should().Be(EstadoRegistro.Activo);
        resultado.Nuevo.Rol.Should().Be(RolUsuario.Participante);
        resultado.Nuevo.Tags.Should().BeEmpty();
        resultado.UsuarioIdAnterior.Should().Be("u_1");
        resultado.CodigoUsuarioAnterior.Should().Be(1);
    }

    [Fact]
    public async Task ReasignarNumero_SiFallaElAlta_RevierteLaInactivacion()
    {
        var anterior = CrearUsuario("u_1", "573001112233");
        var repositorio = Substitute.For<IRepositorioUsuarios>();
        repositorio.ObtenerUsuarioPorIdAsync("u_1", Arg.Any<CancellationToken>()).Returns(anterior);
        repositorio.ReservarCodigosUsuarioAsync(1, Arg.Any<CancellationToken>()).Returns(9);
        var guardados = new List<Usuario>();
        repositorio
            .GuardarUsuarioAsync(Arg.Do<Usuario>(guardados.Add), Arg.Any<CancellationToken>())
            .Returns(_ => guardados.Count == 2
                ? Task.FromException(new ErrorConflicto("choque"))
                : Task.CompletedTask);
        var servicio = CrearServicio(repositorio);

        var act = () => servicio.ReasignarNumeroAsync(
            "u_1",
            new SolicitudReasignarNumero("CARLOS RODRIGUEZ"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ErrorConflicto>();
        // El numero nunca puede quedarse sin titular activo (I-08 §6).
        guardados.Last().Id.Should().Be("u_1");
        guardados.Last().Estado.Should().Be(EstadoRegistro.Activo);
    }

    [Fact]
    public async Task ReasignarNumero_SobreUnInactivo_LanzaConflicto()
    {
        var repositorio = Substitute.For<IRepositorioUsuarios>();
        repositorio
            .ObtenerUsuarioPorIdAsync("u_1", Arg.Any<CancellationToken>())
            .Returns(CrearUsuario("u_1", "573001112233", EstadoRegistro.Inactivo));
        var servicio = CrearServicio(repositorio);

        var act = () => servicio.ReasignarNumeroAsync(
            "u_1",
            new SolicitudReasignarNumero("CARLOS RODRIGUEZ"),
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

    private static Usuario CrearUsuario(
        string id,
        string numero,
        EstadoRegistro estado = EstadoRegistro.Activo,
        string? email = null)
        => Usuario.Crear(
            id,
            1,
            "Usuario",
            NumeroWhatsApp.FromNormalized(numero),
            RolUsuario.Participante,
            estado,
            "Operaciones",
            "GHT",
            null,
            null,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch,
            usuarioWhatsapp: null,
            empresaId: null,
            sede: null,
            cargo: null,
            email);

    private sealed class TimeProviderFijo : TimeProvider
    {
        private readonly DateTimeOffset _ahora;

        public TimeProviderFijo(DateTimeOffset ahora) => _ahora = ahora;

        public override DateTimeOffset GetUtcNow() => _ahora;
    }
}
