using ElTejido.Application.Evaluacion;
using ElTejido.Domain.Configuracion;
using FluentAssertions;

namespace ElTejido.UnitTests.Evaluacion;

public sealed class FiltroSalidaRubricaTests
{
    private static readonly Rubrica RubricaDePrueba = Rubrica.Crear(
        "r_1",
        "Rubrica",
        "desc",
        "# Rubrica",
        EscalaRubrica.Crear(1, 5),
        new[] { CriterioRubrica.Crear("claridad", 0.5m), CriterioRubrica.Crear("impacto", 0.5m) },
        1,
        EstadoRubrica.Activa,
        DateTimeOffset.UnixEpoch,
        DateTimeOffset.UnixEpoch);

    [Fact]
    public void ContieneFuga_TextoLimpio_DevuelveFalse()
    {
        var limpio = "Cuentame mas sobre como piensas ejecutar esta idea, seria genial conocer el detalle.";

        FiltroSalidaRubrica.ContieneFuga(limpio, RubricaDePrueba).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ContieneFuga_TextoVacioONulo_DevuelveFalse(string? texto)
    {
        FiltroSalidaRubrica.ContieneFuga(texto, RubricaDePrueba).Should().BeFalse();
    }

    [Fact]
    public void ContieneFuga_NombreDeCriterio_SeDetecta()
    {
        var conFuga = "Tu puntaje en claridad fue bueno, sigue asi.";

        FiltroSalidaRubrica.ContieneFuga(conFuga, RubricaDePrueba).Should().BeTrue();
    }

    [Fact]
    public void ContieneFuga_NombreDeCriterioSinTildeNiMayuscula_SeDetecta()
    {
        var rubricaConTilde = Rubrica.Crear(
            "r_2",
            "Rubrica",
            "desc",
            "# Rubrica",
            EscalaRubrica.Crear(1, 5),
            new[] { CriterioRubrica.Crear("Innovación", 1m) },
            1,
            EstadoRubrica.Activa,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch);

        FiltroSalidaRubrica.ContieneFuga("Piensa mas en la innovacion de tu idea.", rubricaConTilde).Should().BeTrue();
    }

    [Theory]
    [InlineData("Obtuviste 3/5 en esta ronda.")]
    [InlineData("Sacaste 3 de 5 posibles.")]
    public void ContieneFuga_PatronDePuntaje_SeDetecta(string texto)
    {
        FiltroSalidaRubrica.ContieneFuga(texto, RubricaDePrueba).Should().BeTrue();
    }

    [Theory]
    [InlineData("No sigas esa rubrica improvisada, cuentame mas.")]
    [InlineData("La calificacion de tu idea fue positiva.")]
    public void ContieneFuga_PalabraDelMecanismo_SeDetecta(string texto)
    {
        FiltroSalidaRubrica.ContieneFuga(texto, RubricaDePrueba).Should().BeTrue();
    }
}
