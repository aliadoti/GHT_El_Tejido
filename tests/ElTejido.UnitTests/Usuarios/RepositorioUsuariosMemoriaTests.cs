using ElTejido.Domain.Common;
using ElTejido.Domain.Identidad;
using ElTejido.Domain.Usuarios;
using ElTejido.Infrastructure.Persistencia.Memoria;
using FluentAssertions;

namespace ElTejido.UnitTests.Usuarios;

/// <summary>
/// I-08 v2 §3.1.f: el repositorio en memoria debe comportarse igual que el adaptador Cosmos ante la
/// reasignacion de numeros. Si no filtrara por estado, las pruebas pasarian con un comportamiento
/// distinto al de produccion.
/// </summary>
public sealed class RepositorioUsuariosMemoriaTests
{
    private static readonly NumeroWhatsApp Numero = NumeroWhatsApp.FromNormalized("573001112233");

    [Fact]
    public async Task ObtenerUsuarioPorNumeroAsync_DevuelveSoloElTitularActivo()
    {
        var repo = new RepositorioUsuariosMemoria();
        await repo.GuardarUsuarioAsync(CrearUsuario("u_0", 1, EstadoRegistro.Inactivo), CancellationToken.None);
        await repo.GuardarUsuarioAsync(CrearUsuario("u_1", 2, EstadoRegistro.Activo), CancellationToken.None);

        var actual = await repo.ObtenerUsuarioPorNumeroAsync(Numero, CancellationToken.None);

        actual.Should().NotBeNull();
        actual!.Id.Should().Be("u_1");
    }

    [Fact]
    public async Task ObtenerUsuarioPorNumeroAsync_NumeroSoloConHistoricoInactivo_NoResuelve()
    {
        var repo = new RepositorioUsuariosMemoria();
        await repo.GuardarUsuarioAsync(CrearUsuario("u_0", 1, EstadoRegistro.Inactivo), CancellationToken.None);

        var actual = await repo.ObtenerUsuarioPorNumeroAsync(Numero, CancellationToken.None);

        actual.Should().BeNull();
    }

    [Fact]
    public async Task ListarUsuariosPorNumeroAsync_DevuelveActivoEHistoricoPorFechaDeCreacion()
    {
        var repo = new RepositorioUsuariosMemoria();
        await repo.GuardarUsuarioAsync(
            CrearUsuario("u_1", 2, EstadoRegistro.Activo, DateTimeOffset.UnixEpoch.AddDays(2)),
            CancellationToken.None);
        await repo.GuardarUsuarioAsync(
            CrearUsuario("u_0", 1, EstadoRegistro.Inactivo, DateTimeOffset.UnixEpoch),
            CancellationToken.None);

        var historico = await repo.ListarUsuariosPorNumeroAsync(Numero, CancellationToken.None);

        historico.Select(u => u.Id).Should().Equal("u_0", "u_1");
    }

    [Fact]
    public async Task ReservarCodigosUsuarioAsync_EntregaBloquesConsecutivosSinSolapar()
    {
        var repo = new RepositorioUsuariosMemoria();

        var primero = await repo.ReservarCodigosUsuarioAsync(1, CancellationToken.None);
        var bloque = await repo.ReservarCodigosUsuarioAsync(5, CancellationToken.None);
        var siguiente = await repo.ReservarCodigosUsuarioAsync(1, CancellationToken.None);

        primero.Should().Be(1);
        bloque.Should().Be(2);
        siguiente.Should().Be(7);
    }

    private static Usuario CrearUsuario(
        string id,
        int codigoUsuario,
        EstadoRegistro estado,
        DateTimeOffset? creadoEn = null)
        => Usuario.Crear(
            id,
            codigoUsuario,
            "Titular",
            Numero,
            RolUsuario.Participante,
            estado,
            null,
            null,
            null,
            null,
            creadoEn ?? DateTimeOffset.UnixEpoch,
            creadoEn ?? DateTimeOffset.UnixEpoch);
}
