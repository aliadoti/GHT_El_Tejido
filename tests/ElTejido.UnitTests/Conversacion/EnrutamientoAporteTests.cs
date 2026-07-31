using ElTejido.Domain.Common;
using ElTejido.Domain.Conversaciones;
using FluentAssertions;

namespace ElTejido.UnitTests.Conversacion;

/// <summary>
/// P-26 corte 1 (03 §3.6.1): identidad determinista, particion routing reservada y defaults del
/// aporte conservado antes de elegir campania/pregunta.
/// </summary>
public sealed class EnrutamientoAporteTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 7, 29, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Crear_GeneraIdDeterministaYParticionRoutingPorUsuario()
    {
        var enrutamiento = CrearEnrutamiento();
        var reintento = CrearEnrutamiento();

        enrutamiento.Id.Should().Be("route_u_8f3c_wamid.abc");
        enrutamiento.Id.Should().Be(reintento.Id, "un reintento de Meta no puede crear otro enrutamiento");
        enrutamiento.ParticionRouting.Should().Be("routing:u_8f3c");
        EnrutamientoAporte.GenerarId("u_8f3c", "wamid.abc").Should().Be(enrutamiento.Id);
        EnrutamientoAporte.ParticionRoutingDe("u_8f3c").Should().Be("routing:u_8f3c");
    }

    [Fact]
    public void Crear_SinFechasOpcionales_VenceALas24HorasYActualizadoIgualACreado()
    {
        var enrutamiento = CrearEnrutamiento();

        enrutamiento.VenceEn.Should().Be(Ahora.AddHours(24));
        enrutamiento.ActualizadoEn.Should().Be(Ahora);
        enrutamiento.ProcesadoEn.Should().BeNull();
        enrutamiento.ConversacionId.Should().BeNull();
        enrutamiento.CampaniasOfrecidas.Should().BeEmpty();
        enrutamiento.IntentosSeleccion.Should().BeEmpty();
    }

    [Theory]
    [InlineData("", "wamid.abc", "texto")]
    [InlineData("u_1", "", "texto")]
    [InlineData("u_1", "wamid.abc", " ")]
    public void Crear_SinUsuarioMensajeOTexto_Lanza(string usuarioId, string whatsappMessageId, string texto)
    {
        var acto = () => EnrutamientoAporte.Crear(
            usuarioId,
            whatsappMessageId,
            texto,
            EstadoEnrutamientoAporte.SeleccionCampania,
            Ahora);

        acto.Should().Throw<DomainValidationException>();
    }

    private static EnrutamientoAporte CrearEnrutamiento()
        => EnrutamientoAporte.Crear(
            "u_8f3c",
            "wamid.abc",
            "Se me ocurrio crear...",
            EstadoEnrutamientoAporte.SeleccionCampania,
            Ahora);
}
