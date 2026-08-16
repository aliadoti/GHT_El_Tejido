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
        EscalaRubrica.Crear(1, 5),
        new[] { CriterioRubrica.Crear("claridad", 0.5m), CriterioRubrica.Crear("impacto", 0.5m) },
        1,
        EstadoRubrica.Activa,
        DateTimeOffset.UnixEpoch,
        DateTimeOffset.UnixEpoch);

    [Fact]
    public void ContieneFuga_RubricaDeOchoCriterios_RevisaTodosLosNombres()
    {
        // DT-RUB-01 §8: la lista negra se deriva de la lista canonica completa, sin importar cuantos
        // criterios tenga la version. Agregar criterios en una version nueva cambia la politica sin
        // tocar codigo.
        var nombres = new[]
        {
            "Claridad", "Viabilidad", "Alcance", "Novedad",
            "Impacto", "Coherencia", "Evidencia", "Sostenibilidad",
        };
        var rubrica = Rubrica.Crear(
            "r_8",
            "Rubrica de ocho",
            "desc",
            EscalaRubrica.Crear(1, 5),
            nombres.Select((n, i) => CriterioRubrica.Crear(
                NormalizacionRubrica.NormalizarId(n), n, string.Empty, i == 7 ? 0.125m : 0.125m, i + 1)),
            1,
            EstadoRubrica.Activa,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch);

        foreach (var nombre in nombres)
        {
            FiltroSalidaRubrica.ContieneFuga($"Podrias mejorar la {nombre.ToLowerInvariant()} de tu propuesta.", rubrica)
                .Should().BeTrue($"'{nombre}' es un criterio de la version efectiva");
        }

        FiltroSalidaRubrica.ContieneFuga("Cuentame como llevarias esto a la practica manana.", rubrica)
            .Should().BeFalse();
    }

    [Fact]
    public void ContieneFuga_NombreQueSoloExisteEnUnaVersionNueva_NoAfectaALaVersionAnterior()
    {
        // La politica depende de la version efectiva, no de un listado global.
        FiltroSalidaRubrica.ContieneFuga("Piensa en la sostenibilidad de la idea.", RubricaDePrueba)
            .Should().BeFalse();
    }

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
