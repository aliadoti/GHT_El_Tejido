using ElTejido.Application.Evaluacion;
using ElTejido.Domain.Configuracion;
using ElTejido.Domain.Evaluacion;
using FluentAssertions;

namespace ElTejido.UnitTests.Evaluacion;

public sealed class CalculadorEjeDebilTests
{
    private static readonly CriterioRubrica[] Criterios =
    [
        CriterioRubrica.Crear("claridad", 0.5m),
        CriterioRubrica.Crear("impacto", 0.3m),
        CriterioRubrica.Crear("viabilidad", 0.2m),
    ];

    [Fact]
    public void Determinar_EligeElCriterioDeMenorPuntaje()
    {
        var calificaciones = new[]
        {
            CalificacionCriterio.Crear("claridad", 4m, "clara"),
            CalificacionCriterio.Crear("impacto", 2m, "bajo impacto"),
            CalificacionCriterio.Crear("viabilidad", 3m, "viable"),
        };

        var eje = CalculadorEjeDebil.Determinar(calificaciones, Criterios);

        eje.Should().NotBeNull();
        eje!.Nombre.Should().Be("impacto");
    }

    [Fact]
    public void Determinar_EmpateEnPuntaje_DesempataPorMenorPeso()
    {
        var calificaciones = new[]
        {
            CalificacionCriterio.Crear("claridad", 2m, "empate"),
            CalificacionCriterio.Crear("impacto", 2m, "empate"),
            CalificacionCriterio.Crear("viabilidad", 4m, "ok"),
        };

        var eje = CalculadorEjeDebil.Determinar(calificaciones, Criterios);

        // claridad (peso 0.5) e impacto (peso 0.3) empatan en puntaje; gana el de menor peso.
        eje.Should().NotBeNull();
        eje!.Nombre.Should().Be("impacto");
    }

    [Fact]
    public void Determinar_EmparejaPorIdCanonicoAunqueElNombreVisibleCambie()
    {
        // DT-RUB-01 §8: renombrar la etiqueta visible no puede romper el calculo.
        var criterios = new[]
        {
            CriterioRubrica.Crear("claridad", "Claridad de la propuesta", string.Empty, 0.5m, 1),
            CriterioRubrica.Crear("impacto", "Impacto esperado", string.Empty, 0.5m, 2),
        };
        var calificaciones = new[]
        {
            CalificacionCriterio.Crear("claridad", "Otro nombre cualquiera", 4m, "ok"),
            CalificacionCriterio.Crear("impacto", "Etiqueta distinta", 2m, "bajo"),
        };

        CalculadorEjeDebil.Determinar(calificaciones, criterios)!.Id.Should().Be("impacto");
    }

    [Fact]
    public void Determinar_EmpateEnPuntajeYPeso_DesempataPorOrdenAntesQuePorId()
    {
        // DT-RUB-01 §8: el desempate es menor peso -> orden -> id ordinal. Aqui "zeta" va primero en
        // el orden de la rubrica, asi que gana pese a ser posterior alfabeticamente.
        var criterios = new[]
        {
            CriterioRubrica.Crear("zeta", "Zeta", string.Empty, 0.5m, 1),
            CriterioRubrica.Crear("alfa", "Alfa", string.Empty, 0.5m, 2),
        };
        var calificaciones = new[]
        {
            CalificacionCriterio.Crear("zeta", "Zeta", 1m, "empate"),
            CalificacionCriterio.Crear("alfa", "Alfa", 1m, "empate"),
        };

        CalculadorEjeDebil.Determinar(calificaciones, criterios)!.Id.Should().Be("zeta");
    }

    [Fact]
    public void Determinar_CalificacionHistoricaSinId_CaeAlNombreSnapshot()
    {
        // 03 §3.9: un documento anterior a DT-RUB-01 no tiene criterioId; lo unico que guardo es el
        // nombre visible, y con eso debe seguir resolviendose.
        var calificaciones = new[]
        {
            CalificacionCriterio.CrearHistorico("claridad", 4m, "clara"),
            CalificacionCriterio.CrearHistorico("impacto", 2m, "bajo"),
        };

        CalculadorEjeDebil.Determinar(calificaciones, Criterios)!.Nombre.Should().Be("impacto");
    }

    [Fact]
    public void Determinar_EmpateEnPuntajeYPeso_DesempataAlfabeticamente()
    {
        var criterios = new[]
        {
            CriterioRubrica.Crear("zeta", 0.5m),
            CriterioRubrica.Crear("alfa", 0.5m),
        };
        var calificaciones = new[]
        {
            CalificacionCriterio.Crear("zeta", 1m, "empate"),
            CalificacionCriterio.Crear("alfa", 1m, "empate"),
        };

        var eje = CalculadorEjeDebil.Determinar(calificaciones, criterios);

        eje.Should().NotBeNull();
        eje!.Nombre.Should().Be("alfa");
    }

    [Fact]
    public void Determinar_SinCalificaciones_DevuelveNull()
    {
        var eje = CalculadorEjeDebil.Determinar(Array.Empty<CalificacionCriterio>(), Criterios);

        eje.Should().BeNull();
    }

    [Fact]
    public void Determinar_CriterioSinCoincidenciaEnRubrica_SeIgnora()
    {
        var calificaciones = new[]
        {
            CalificacionCriterio.Crear("criterio_desconocido", 1m, "no existe en la rubrica"),
            CalificacionCriterio.Crear("impacto", 3m, "ok"),
        };

        var eje = CalculadorEjeDebil.Determinar(calificaciones, Criterios);

        eje.Should().NotBeNull();
        eje!.Nombre.Should().Be("impacto");
    }
}
