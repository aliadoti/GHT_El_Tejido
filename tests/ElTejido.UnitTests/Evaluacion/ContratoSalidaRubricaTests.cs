using ElTejido.Application.Evaluacion;
using ElTejido.Domain.Configuracion;
using ElTejido.Domain.Evaluacion;
using FluentAssertions;

namespace ElTejido.UnitTests.Evaluacion;

/// <summary>
/// DT-RUB-01 corte 2 (08 §4.1): la salida valida contiene <b>exactamente</b> los ids de la version
/// efectiva y el total de negocio lo calcula el servidor con los pesos configurados.
/// </summary>
public sealed class ContratoSalidaRubricaTests
{
    private static readonly Rubrica RubricaTres = Rubrica.Crear(
        "r_qa",
        "Rubrica QA",
        "desc",
        new EscalaRubrica(1, 5),
        [
            CriterioRubrica.Crear("claridad", "Claridad", "Que tan concreta es.", 0.30m, 1),
            CriterioRubrica.Crear("viabilidad", "Viabilidad", "Que tan realizable es.", 0.50m, 2),
            CriterioRubrica.Crear("alcance", "Alcance", "A cuanta gente llega.", 0.20m, 3),
        ],
        1,
        EstadoRubrica.Activa,
        DateTimeOffset.UnixEpoch,
        DateTimeOffset.UnixEpoch);

    [Fact]
    public void Emparejar_ConjuntoExacto_EsValidoYUsaElNombreDelSnapshot()
    {
        var resultado = ContratoSalidaRubrica.Emparejar(
            [Salida("alcance", 4), Salida("claridad", 5), Salida("viabilidad", 3)],
            RubricaTres);

        resultado.Valido.Should().BeTrue();

        // El orden de salida es el de la rubrica, no el que devolvio el modelo.
        resultado.Calificaciones.Select(c => c.CriterioId).Should().Equal("claridad", "viabilidad", "alcance");

        // El nombre visible lo pone el servidor desde la version efectiva, no el modelo.
        resultado.Calificaciones.Select(c => c.Criterio).Should().Equal("Claridad", "Viabilidad", "Alcance");
    }

    [Fact]
    public void Emparejar_CriterioFaltante_Rechaza()
    {
        var resultado = ContratoSalidaRubrica.Emparejar([Salida("claridad", 4), Salida("viabilidad", 4)], RubricaTres);

        resultado.Valido.Should().BeFalse();
        resultado.Motivo.Should().Be("criterio_faltante");
        resultado.Calificaciones.Should().BeEmpty();
    }

    [Fact]
    public void Emparejar_CriterioAdicional_Rechaza()
    {
        var resultado = ContratoSalidaRubrica.Emparejar(
            [Salida("claridad", 4), Salida("viabilidad", 4), Salida("alcance", 4), Salida("impacto", 4)],
            RubricaTres);

        resultado.Valido.Should().BeFalse();
        resultado.Motivo.Should().Be("criterio_extra");
    }

    [Fact]
    public void Emparejar_CriterioDuplicado_Rechaza()
    {
        var resultado = ContratoSalidaRubrica.Emparejar(
            [Salida("claridad", 4), Salida("claridad", 2), Salida("viabilidad", 4), Salida("alcance", 4)],
            RubricaTres);

        resultado.Valido.Should().BeFalse();
        resultado.Motivo.Should().Be("criterio_duplicado");
    }

    [Fact]
    public void Emparejar_PuntajeFueraDeEscala_Rechaza()
    {
        var resultado = ContratoSalidaRubrica.Emparejar(
            [Salida("claridad", 9), Salida("viabilidad", 4), Salida("alcance", 4)],
            RubricaTres);

        resultado.Valido.Should().BeFalse();
        resultado.Motivo.Should().Be("puntaje_fuera_escala");
    }

    [Fact]
    public void Emparejar_JustificacionVacia_Rechaza()
    {
        var resultado = ContratoSalidaRubrica.Emparejar(
            [
                new SalidaCalificacionCriterio { CriterioId = "claridad", Puntaje = 4, Justificacion = "   " },
                Salida("viabilidad", 4),
                Salida("alcance", 4),
            ],
            RubricaTres);

        resultado.Valido.Should().BeFalse();
        resultado.Motivo.Should().Be("justificacion_vacia");
    }

    [Fact]
    public void Emparejar_SinCalificaciones_RechazaPorFaltante()
    {
        ContratoSalidaRubrica.Emparejar(null, RubricaTres).Motivo.Should().Be("criterio_faltante");
    }

    [Fact]
    public void Emparejar_IdCorrectoConNombreVisibleAjeno_SeAceptaPorqueElNombreLoPoneElServidor()
    {
        // El modelo no decide el nombre: aunque devolviera texto arbitrario, el emparejamiento es por
        // criterio_id y la etiqueta persistida sale del snapshot.
        var resultado = ContratoSalidaRubrica.Emparejar(
            [Salida("claridad", 4), Salida("viabilidad", 4), Salida("alcance", 4)],
            RubricaTres);

        resultado.Valido.Should().BeTrue();
        resultado.Calificaciones.Should().OnlyContain(c => c.Criterio != c.CriterioId);
    }

    [Fact]
    public void CalcularTotalPonderado_UsaLosPesosConfigurados()
    {
        // QAS/24 prueba 6: (5 x 0.30) + (3 x 0.50) + (4 x 0.20) = 3.80
        var calificaciones = ContratoSalidaRubrica
            .Emparejar([Salida("claridad", 5), Salida("viabilidad", 3), Salida("alcance", 4)], RubricaTres)
            .Calificaciones;

        var total = ContratoSalidaRubrica.CalcularTotalPonderado(calificaciones, RubricaTres.Criterios);

        total.Should().Be(3.80m);
    }

    [Fact]
    public void CalcularTotalPonderado_NoRedondeaAntesDeDevolver()
    {
        var rubrica = Rubrica.Crear(
            "r_tercios",
            "Tercios",
            "desc",
            new EscalaRubrica(1, 5),
            [
                CriterioRubrica.Crear("uno", "Uno", string.Empty, 0.33m, 1),
                CriterioRubrica.Crear("dos", "Dos", string.Empty, 0.33m, 2),
                CriterioRubrica.Crear("tres", "Tres", string.Empty, 0.34m, 3),
            ],
            1,
            EstadoRubrica.Activa,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch);

        var calificaciones = ContratoSalidaRubrica
            .Emparejar([Salida("uno", 5), Salida("dos", 4), Salida("tres", 2)], rubrica)
            .Calificaciones;

        var total = ContratoSalidaRubrica.CalcularTotalPonderado(calificaciones, rubrica.Criterios);

        // (5x0.33 + 4x0.33 + 2x0.34) / 1.00 = 3.65 exacto, sin redondeo intermedio.
        total.Should().Be(3.65m);
    }

    [Fact]
    public void CalcularTotalPonderado_PesosNoUniformes_PesaMasElCriterioDominante()
    {
        var rubrica = Rubrica.Crear(
            "r_dominante",
            "Dominante",
            "desc",
            new EscalaRubrica(1, 5),
            [
                CriterioRubrica.Crear("menor", "Menor", string.Empty, 0.1m, 1),
                CriterioRubrica.Crear("mayor", "Mayor", string.Empty, 0.9m, 2),
            ],
            1,
            EstadoRubrica.Activa,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch);

        var calificaciones = ContratoSalidaRubrica
            .Emparejar([Salida("menor", 5), Salida("mayor", 1)], rubrica)
            .Calificaciones;

        ContratoSalidaRubrica.CalcularTotalPonderado(calificaciones, rubrica.Criterios).Should().Be(1.4m);
    }

    private static SalidaCalificacionCriterio Salida(string criterioId, decimal puntaje)
        => new() { CriterioId = criterioId, Puntaje = puntaje, Justificacion = "justificacion" };
}
