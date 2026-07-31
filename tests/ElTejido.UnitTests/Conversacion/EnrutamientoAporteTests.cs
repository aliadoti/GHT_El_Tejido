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

    [Fact]
    public void Transiciones_SeleccionCampaniaPreguntaYProcesado_SiguenElOrdenDelContrato()
    {
        var resuelto = CrearEnrutamiento()
            .SeleccionarCampania("c_1", Ahora.AddMinutes(1))
            .OfrecerPreguntas([new OpcionPreguntaOfrecida("p_1", "¿Cómo mejorar?", 1)], Ahora.AddMinutes(2))
            .SeleccionarPregunta("p_1", Ahora.AddMinutes(3));

        resuelto.Estado.Should().Be(EstadoEnrutamientoAporte.Listo);
        resuelto.CampaniaSeleccionadaId.Should().Be("c_1");
        resuelto.PreguntaSeleccionadaId.Should().Be("p_1");

        var enIdea = resuelto.MarcarEnIdea("conv_1", Ahora.AddMinutes(4));
        enIdea.Estado.Should().Be(EstadoEnrutamientoAporte.EnIdea);
        enIdea.ConversacionId.Should().Be("conv_1");
        enIdea.ProcesadoEn.Should().Be(Ahora.AddMinutes(4));

        enIdea.Completar(Ahora.AddMinutes(5)).Estado.Should().Be(EstadoEnrutamientoAporte.Completado);
    }

    [Fact]
    public void EstablecerAfinidad_DesdeListo_ApuntaALaConversacionSinFijarProcesadoEn()
    {
        var listo = CrearEnrutamiento()
            .SeleccionarCampania("c_2", Ahora.AddMinutes(1))
            .SeleccionarPregunta("p_1", Ahora.AddMinutes(2));

        var afinidad = listo.EstablecerAfinidad(null, Ahora.AddMinutes(3));

        afinidad.Estado.Should().Be(EstadoEnrutamientoAporte.EnIdea);
        afinidad.ConversacionId.Should().BeNull("aún no hay conversación en la campaña nueva");
        afinidad.ProcesadoEn.Should().BeNull("su aporte original ya fue procesado antes del cambio");
    }

    [Theory]
    [InlineData(EstadoEnrutamientoAporte.SeleccionCampania)]
    [InlineData(EstadoEnrutamientoAporte.EnIdea)]
    [InlineData(EstadoEnrutamientoAporte.Completado)]
    public void MarcarEnIdea_FueraDeListo_Lanza(EstadoEnrutamientoAporte estado)
    {
        var enrutamiento = EnrutamientoAporte.Crear(
            "u_1", "wamid.abc", "texto", estado, Ahora);

        var acto = () => enrutamiento.MarcarEnIdea("conv_1", Ahora);

        acto.Should().Throw<DomainValidationException>();
    }

    [Fact]
    public void SeleccionVencida_SoloAplicaAEstadosDeSeleccion()
    {
        var enSeleccion = CrearEnrutamiento();
        var listo = enSeleccion.SeleccionarCampania("c_1", Ahora).SeleccionarPregunta("p_1", Ahora);

        enSeleccion.SeleccionVencida(Ahora.AddHours(25)).Should().BeTrue();
        enSeleccion.SeleccionVencida(Ahora.AddHours(23)).Should().BeFalse();
        listo.SeleccionVencida(Ahora.AddHours(25)).Should().BeFalse("un enrutamiento resuelto no expira por este camino");
    }

    private static EnrutamientoAporte CrearEnrutamiento()
        => EnrutamientoAporte.Crear(
            "u_8f3c",
            "wamid.abc",
            "Se me ocurrio crear...",
            EstadoEnrutamientoAporte.SeleccionCampania,
            Ahora);
}
