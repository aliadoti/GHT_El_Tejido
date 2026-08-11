using ElTejido.Application.Configuracion;
using ElTejido.Application.Conversacion;
using FluentAssertions;
using NSubstitute;
using DominioConversacion = ElTejido.Domain.Conversaciones.Conversacion;

namespace ElTejido.UnitTests.Configuracion;

public sealed class ResolutorTextosConversacionTests
{
    [Fact]
    public async Task GateApagado_ConservaTextosHeredadosAunqueElHiloSeaIngles()
    {
        var proveedor = Substitute.For<IProveedorTextosConversacion>();
        proveedor.ObtenerParaRuntimeAsync("en", Arg.Any<CancellationToken>())
            .Returns(new ResultadoTextosConversacion(null, OrigenTextosConversacion.Legacy));
        var opciones = new OpcionesConversacion
        {
            Mensajes = new OpcionesMensajesConversacion { SaludoPrimerContacto = "Saludo vigente" },
        };
        var resolutor = new ResolutorTextosConversacion(proveedor, opciones);

        var resultado = await resolutor.ResolverAsync(Conversacion("en"), CancellationToken.None);

        resultado.Idioma.Should().Be("es");
        resultado.Origen.Should().Be(OrigenTextosConversacion.Legacy);
        resultado.VersionCatalogo.Should().BeNull();
        resultado.Mensajes["saludoPrimerContacto"].Should().Be("Saludo vigente");
        await proveedor.Received(1).ObtenerParaRuntimeAsync("en", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CatalogoActivo_UsaElIdiomaFijadoEnElHiloYExponeSuVersion()
    {
        var proveedor = Substitute.For<IProveedorTextosConversacion>();
        var version = CatalogosTextosSemilla.CrearVersionEmergencia("en");
        proveedor.ObtenerParaRuntimeAsync("en", Arg.Any<CancellationToken>())
            .Returns(new ResultadoTextosConversacion(version, OrigenTextosConversacion.Catalogo));
        var resolutor = new ResolutorTextosConversacion(proveedor, new OpcionesConversacion());

        var resultado = await resolutor.ResolverAsync(Conversacion("en"), CancellationToken.None);

        resultado.Idioma.Should().Be("en");
        resultado.VersionCatalogo.Should().Be(1);
        resultado.HuellaCatalogo.Should().Be(version.Catalogo.Huella);
        resultado.Mensajes["saludoPrimerContacto"].Should().StartWith("Hello!");
    }

    [Fact]
    public void Semillas_IncluyenConfirmacionYAclaracionesP27PorIdioma()
    {
        var espanol = CatalogosTextosSemilla.CrearSolicitud("es");
        var ingles = CatalogosTextosSemilla.CrearSolicitud("en");

        espanol.Frases["confirmar"].Should().Contain("confirmo");
        ingles.Frases["confirmar"].Should().Contain("yes");
        ingles.Mensajes["menuAclaracionSalida"].Should().StartWith("What would you prefer?");
        ingles.Mensajes["respaldoAclaracionSalida"].Should().StartWith("You can continue");
    }

    private static DominioConversacion Conversacion(string idioma)
        => DominioConversacion.Iniciar(
            "conv_1", "c_1", "u_1", "p_1", "whatsapp", null, DateTimeOffset.UnixEpoch, idioma: idioma);
}
