using ElTejido.Domain.Respuestas;
using FluentAssertions;

namespace ElTejido.UnitTests.Respuestas;

public sealed class RespuestaTests
{
    private static Respuesta Crear(NivelMadurez? nivel = null)
        => Respuesta.Crear(
            "resp_1",
            "camp_1",
            "u_1",
            "p_1",
            "conv_1",
            "Mi idea",
            "whatsapp",
            esRepregunta: false,
            EstadoRespuesta.Evaluada,
            DateTimeOffset.UtcNow,
            tagsSnapshot: null,
            nivelMadurez: nivel ?? NivelMadurez.Incubacion);

    [Fact]
    public void Crear_DefaultDeMadurezEsIncubacion()
    {
        var respuesta = Respuesta.Crear(
            "resp_1",
            "camp_1",
            "u_1",
            "p_1",
            "conv_1",
            "Mi idea",
            "whatsapp",
            esRepregunta: false,
            EstadoRespuesta.Evaluada,
            DateTimeOffset.UtcNow,
            tagsSnapshot: null);

        respuesta.NivelMadurez.Should().Be(NivelMadurez.Incubacion);
    }

    [Fact]
    public void Crear_SellaMadurezMaduroCuandoSeIndica()
    {
        var respuesta = Crear(NivelMadurez.Maduro);

        respuesta.NivelMadurez.Should().Be(NivelMadurez.Maduro);
    }

    [Fact]
    public void ReclasificarComoIncubacion_DegradaUnaRespuestaMadura()
    {
        var respuesta = Crear(NivelMadurez.Maduro);

        respuesta.ReclasificarComoIncubacion();

        respuesta.NivelMadurez.Should().Be(NivelMadurez.Incubacion);
    }

    [Fact]
    public void ReclasificarComoIncubacion_EsIdempotenteYNuncaPromueve()
    {
        var respuesta = Crear(NivelMadurez.Incubacion);

        respuesta.ReclasificarComoIncubacion();
        respuesta.ReclasificarComoIncubacion();

        respuesta.NivelMadurez.Should().Be(NivelMadurez.Incubacion);
    }
}
