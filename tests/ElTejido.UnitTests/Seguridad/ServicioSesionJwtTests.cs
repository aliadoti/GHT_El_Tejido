using ElTejido.Application.Auth;
using ElTejido.Application.Seguridad;
using ElTejido.Domain.Usuarios;
using ElTejido.Infrastructure.Seguridad;
using ElTejido.UnitTests.Soporte;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace ElTejido.UnitTests.Seguridad;

public sealed class ServicioSesionJwtTests
{
    private const string ClaveFirma = "clave-de-firma-de-sesion-de-32+bytes!!";

    [Fact]
    public async Task EmitirYValidar_RoundtripDevuelveLaIdentidad()
    {
        var servicio = Crear(DateTimeOffset.UtcNow);
        var usuario = FabricasDominio.CrearUsuario("u_admin1", "573001119999", RolUsuario.Admin, nombre: "Admin");

        var sesion = await servicio.EmitirAsync(usuario, CancellationToken.None);
        var principal = await servicio.ValidarAsync(sesion.Token, CancellationToken.None);

        sesion.Usuario.Should().BeEquivalentTo(new UsuarioSesion("u_admin1", "Admin", "admin"));
        sesion.CsrfToken.Should().NotBeNullOrWhiteSpace();
        principal.Should().NotBeNull();
        principal!.UsuarioId.Should().Be("u_admin1");
        principal.Nombre.Should().Be("Admin");
        principal.Rol.Should().Be(RolUsuario.Admin);
        principal.CsrfToken.Should().Be(sesion.CsrfToken);
    }

    [Fact]
    public async Task ValidarAsync_TokenManipulado_DevuelveNull()
    {
        var servicio = Crear(DateTimeOffset.UtcNow);
        var usuario = FabricasDominio.CrearUsuario("u_admin1", "573001119999");
        var sesion = await servicio.EmitirAsync(usuario, CancellationToken.None);

        var manipulado = sesion.Token[..^2] + (sesion.Token[^1] == 'a' ? "bb" : "aa");
        var principal = await servicio.ValidarAsync(manipulado, CancellationToken.None);

        principal.Should().BeNull();
    }

    [Fact]
    public async Task ValidarAsync_TokenExpirado_DevuelveNull()
    {
        // Emite con un reloj en el pasado para que la expiracion ya haya ocurrido al validar.
        var servicio = Crear(DateTimeOffset.UtcNow.AddHours(-2));
        var usuario = FabricasDominio.CrearUsuario("u_admin1", "573001119999");
        var sesion = await servicio.EmitirAsync(usuario, CancellationToken.None);

        var principal = await servicio.ValidarAsync(sesion.Token, CancellationToken.None);

        principal.Should().BeNull();
    }

    [Fact]
    public async Task EmitirAsync_ClaveDeFirmaDemasiadoCorta_Lanza()
    {
        var secretos = Substitute.For<ISecretProvider>();
        secretos.ObtenerSecretoAsync(NombresSecretos.JwtSign, Arg.Any<CancellationToken>()).Returns("corta");
        var servicio = new ServicioSesionJwt(secretos, OpcionesPorDefecto(), new RelojFijo(DateTimeOffset.UtcNow));
        var usuario = FabricasDominio.CrearUsuario("u_admin1", "573001119999");

        var accion = async () => await servicio.EmitirAsync(usuario, CancellationToken.None);

        await accion.Should().ThrowAsync<InvalidOperationException>();
    }

    private static ServicioSesionJwt Crear(DateTimeOffset ahora)
    {
        var secretos = Substitute.For<ISecretProvider>();
        secretos.ObtenerSecretoAsync(NombresSecretos.JwtSign, Arg.Any<CancellationToken>()).Returns(ClaveFirma);
        return new ServicioSesionJwt(secretos, OpcionesPorDefecto(), new RelojFijo(ahora));
    }

    private static IOptions<OpcionesAuth> OpcionesPorDefecto()
        => Options.Create(new OpcionesAuth { SesionTtlMinutos = 60 });
}
