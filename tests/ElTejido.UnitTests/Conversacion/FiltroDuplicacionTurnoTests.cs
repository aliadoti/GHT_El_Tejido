using ElTejido.Application.Conversacion;
using FluentAssertions;

namespace ElTejido.UnitTests.Conversacion;

/// <summary>
/// DT-I20-01 §4.2/§6: la guarda de no duplicación es determinista y conservadora. Ante duda conserva
/// el cuerpo validado y solo descarta el adorno redundante; nunca reescribe el contenido aprobado.
/// </summary>
public sealed class FiltroDuplicacionTurnoTests
{
    [Fact]
    public void PuenteIgualAUnaOracionDelCuerpo_SeEnviaUnaSolaVez()
    {
        var composicion = FiltroDuplicacionTurno.Componer(
            "Ya queda claro el avance.",
            "Ya queda claro el avance. Definiste responsables e indicadores.",
            "¿Cómo lo conectarías con los ingresos?",
            preguntaExigida: true);

        composicion.PuenteOmitido.Should().BeTrue();
        composicion.RequiereRespaldo.Should().BeFalse();
        composicion.Texto.Should().Be(
            "Ya queda claro el avance. Definiste responsables e indicadores.\n\n¿Cómo lo conectarías con los ingresos?");
        composicion.Motivo.Should().Be(FiltroDuplicacionTurno.MotivoPuenteOmitido);
    }

    [Theory]
    // Prefijo del cuerpo, cuerpo prefijo del puente y primera oración equivalente (§4.2 reglas 1 y 2).
    [InlineData("Ya queda claro.", "Ya queda claro. Y sumaste el indicador.")]
    [InlineData("Ya queda claro el avance y sumaste el indicador.", "Ya queda claro el avance")]
    [InlineData("¡Ya queda claro! Sigamos con el detalle.", "Ya queda claro. Definiste responsables.")]
    public void PuenteRedundante_ConservaElCuerpoValidado(string puente, string cuerpo)
    {
        var composicion = FiltroDuplicacionTurno.Componer(puente, cuerpo, pregunta: null, preguntaExigida: false);

        composicion.PuenteOmitido.Should().BeTrue();
        composicion.Texto.Should().Be(cuerpo.Trim());
    }

    [Fact]
    public void PuenteDistinto_PreservaElOrdenPuenteCuerpoPregunta()
    {
        var composicion = FiltroDuplicacionTurno.Componer(
            "Ya definiste responsables e indicadores.",
            "Tu propuesta conecta el seguimiento con la operación.",
            "¿Qué acción aumentaría los ingresos?",
            preguntaExigida: true);

        composicion.PuenteOmitido.Should().BeFalse();
        composicion.PreguntaOmitida.Should().BeFalse();
        composicion.Motivo.Should().BeNull();
        composicion.Texto.Should().Be(
            "Ya definiste responsables e indicadores.\n\n"
            + "Tu propuesta conecta el seguimiento con la operación.\n\n"
            + "¿Qué acción aumentaría los ingresos?");
    }

    [Fact]
    public void PreguntaDuplicadaEnActoQueLaExige_PideElRespaldoSeguro()
    {
        var composicion = FiltroDuplicacionTurno.Componer(
            "Recojo tu avance.",
            "Tu idea ya tiene responsables. ¿Cómo la conectarías con los ingresos?",
            "¿Cómo la conectarías con los ingresos?",
            preguntaExigida: true);

        composicion.RequiereRespaldo.Should().BeTrue();
        composicion.Texto.Should().BeEmpty();
        composicion.Motivo.Should().Contain(FiltroDuplicacionTurno.MotivoRespaldoPorDuplicacion);
    }

    [Fact]
    public void PreguntaDuplicadaEnActoOpcional_SeOmiteSinPerderElCuerpo()
    {
        const string cuerpo = "Retomamos tu idea. ¿Quieres completarla?";

        var composicion = FiltroDuplicacionTurno.Componer(
            "Volvemos a lo que dejaste abierto.", cuerpo, "¿Quieres completarla?", preguntaExigida: false);

        composicion.PreguntaOmitida.Should().BeTrue();
        composicion.RequiereRespaldo.Should().BeFalse();
        composicion.Texto.Should().Be("Volvemos a lo que dejaste abierto.\n\n" + cuerpo);
        composicion.Motivo.Should().Be(FiltroDuplicacionTurno.MotivoPreguntaOmitida);
    }

    [Fact]
    public void ActoSinCuerpo_NoInventaDuplicacionNiPierdeElPuente()
    {
        var composicion = FiltroDuplicacionTurno.Componer(
            "Quedamos en pausa; retómalo cuando quieras.", cuerpo: null, pregunta: null, preguntaExigida: false);

        composicion.PuenteOmitido.Should().BeFalse();
        composicion.RequiereRespaldo.Should().BeFalse();
        composicion.Texto.Should().Be("Quedamos en pausa; retómalo cuando quieras.");
    }

    [Fact]
    public void PalabrasCompartidasSinOracionRepetida_NoDescartanNada()
    {
        var composicion = FiltroDuplicacionTurno.Componer(
            "Gracias por el detalle del indicador.",
            "El indicador que propones permite seguir el avance mes a mes.",
            pregunta: null,
            preguntaExigida: false);

        composicion.PuenteOmitido.Should().BeFalse();
        composicion.Texto.Should().StartWith("Gracias por el detalle del indicador.");
    }
}
