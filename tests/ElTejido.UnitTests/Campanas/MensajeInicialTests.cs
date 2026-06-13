using ElTejido.Domain.Campanas;
using ElTejido.Domain.Common;
using FluentAssertions;

namespace ElTejido.UnitTests.Campanas;

public sealed class MensajeInicialTests
{
    [Fact]
    public void Crear_NormalizesInitialMessageFields()
    {
        var plantilla = PlantillaWhatsApp.Crear(
            " el_tejido_saludo ",
            " es ",
            ["nombre", " campania ", "nombre", " "]);

        var mensaje = MensajeInicial.Crear(
            " mi_1 ",
            " saludo ",
            " Hola {{nombre}}. ",
            1,
            ["nombre", " empresa ", "nombre", " "],
            EstadoRegistro.Activo,
            plantilla);

        mensaje.Id.Should().Be("mi_1");
        mensaje.NombreInterno.Should().Be("saludo");
        mensaje.Texto.Should().Be("Hola {{nombre}}.");
        mensaje.Orden.Should().Be(1);
        mensaje.VariablesDinamicas.Should().Equal("nombre", "empresa");
        mensaje.Estado.Should().Be(EstadoRegistro.Activo);
        mensaje.PlantillaWhatsApp.Should().BeSameAs(plantilla);
        plantilla.Nombre.Should().Be("el_tejido_saludo");
        plantilla.Idioma.Should().Be("es");
        plantilla.Componentes.Should().Equal("nombre", "campania");
    }

    [Fact]
    public void Crear_RejectsNonPositiveOrder()
    {
        var act = () => MensajeInicial.Crear(
            "mi_1",
            "saludo",
            "Hola.",
            0,
            [],
            EstadoRegistro.Activo,
            null);

        act.Should()
            .Throw<DomainValidationException>()
            .Where(exception => exception.Code == "ORDEN_INVALIDO");
    }
}
