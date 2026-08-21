using ElTejido.Application.Campanas;
using ElTejido.Domain.Campanas;
using ElTejido.Domain.Common;
using ElTejido.Domain.Identidad;
using ElTejido.Domain.Usuarios;
using ElTejido.UnitTests.Soporte;
using FluentAssertions;

namespace ElTejido.UnitTests.Campanas;

public sealed class RenderizadorMensajeTests
{
    [Fact]
    public void ConstruirVariables_UsaNombreSaludoYNoElNombreCompleto()
    {
        var usuario = Usuario.Crear(
            "u_1",
            1,
            "ARENAS CHAVES JUAN PABLO",
            NumeroWhatsApp.FromNormalized("573001112233"),
            RolUsuario.Participante,
            EstadoRegistro.Activo,
            null,
            null,
            null,
            null,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch);
        var campania = FabricasDominio.CrearCampania("c_1", EstadoCampania.Activa);

        var variables = RenderizadorMensaje.ConstruirVariables(usuario, campania);

        variables["nombre"].Should().Be("Juan Pablo");
        RenderizadorMensaje.Reemplazar("Hola {{nombre}}", variables).Should().Be("Hola Juan Pablo");
    }
}
