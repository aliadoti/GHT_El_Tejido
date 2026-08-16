using ElTejido.Domain.Common;
using ElTejido.Domain.Configuracion;
using FluentAssertions;

namespace ElTejido.UnitTests.Configuracion;

/// <summary>
/// DT-RUB-01 corte 1: la estructura versionada es la fuente unica. Cubre cantidades variables de
/// criterios (no hay un numero funcional fijo), cada motivo tipificado de 04 §5.5 y el determinismo
/// de la proyeccion Markdown y de la huella.
/// </summary>
public sealed class RubricaEstructuradaTests
{
    private static readonly DateTimeOffset Epoca = DateTimeOffset.UnixEpoch;

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(8)]
    public void Crear_AceptaCualquierCantidadDeCriteriosConPesosValidos(int cantidad)
    {
        var rubrica = CrearRubrica(CriteriosUniformes(cantidad));

        rubrica.Criterios.Should().HaveCount(cantidad);
        rubrica.Criterios.Select(c => c.Orden).Should().BeInAscendingOrder();
        rubrica.Criterios.Select(c => c.Orden).Should().Equal(Enumerable.Range(1, cantidad));
        rubrica.IntegridadEstructural.Should().Be(EstadoIntegridadRubrica.Valida);
        rubrica.HabilitadaParaAsignacionNueva.Should().BeTrue();
    }

    [Fact]
    public void Crear_SinCriterios_Rechaza()
    {
        var accion = () => CrearRubrica(Array.Empty<CriterioRubrica>());

        accion.Should().Throw<DomainValidationException>();
        Validar(Array.Empty<CriterioRubrica>())
            .Should().ContainSingle(e => e.Campo == "criterios" && e.Motivo == "requerido");
    }

    [Fact]
    public void Validar_MasCriteriosQueElTechoTecnico_ReportaLimiteExcedido()
    {
        var criterios = CriteriosUniformes(ValidadorRubricaEstructurada.MaxCriterios + 1);

        Validar(criterios).Should().ContainSingle(e => e.Campo == "criterios" && e.Motivo == "limite_excedido");
    }

    [Fact]
    public void Validar_IdDuplicado_ReportaElSegundoCriterio()
    {
        var criterios = new[]
        {
            CriterioRubrica.Crear("claridad", "Claridad", string.Empty, 0.5m, 1),
            CriterioRubrica.Crear("claridad", "Otra cosa", string.Empty, 0.5m, 2),
        };

        Validar(criterios).Should().Contain(e => e.Campo == "criterios.1.id" && e.Motivo == "duplicado");
    }

    [Fact]
    public void Validar_NombreDuplicadoSoloPorTildesYMayusculas_LoDetecta()
    {
        var criterios = new[]
        {
            CriterioRubrica.Crear("innovacion", "Innovación", string.Empty, 0.5m, 1),
            CriterioRubrica.Crear("innovacion_2", "INNOVACION", string.Empty, 0.5m, 2),
        };

        Validar(criterios).Should().Contain(e => e.Campo == "criterios.1.nombre" && e.Motivo == "duplicado");
    }

    [Fact]
    public void Validar_IdNoCanonico_ReportaFormatoInvalido()
    {
        var criterios = new[] { CriterioRubrica.Crear("Claridad Total", "Claridad", string.Empty, 1m, 1) };

        Validar(criterios).Should().Contain(e => e.Campo == "criterios.0.id" && e.Motivo == "formato_invalido");
    }

    [Theory]
    [InlineData(0.0001)]
    [InlineData(1.5)]
    public void Validar_PesoFueraDeRango_LoReporta(double peso)
    {
        var criterios = new[] { CriterioRubrica.Crear("claridad", "Claridad", string.Empty, (decimal)peso, 1) };

        Validar(criterios).Should().Contain(e => e.Campo == "criterios.0.peso" && e.Motivo == "fuera_de_rango"
            || e.Campo == "criterios.pesos" && e.Motivo == "suma_invalida");
    }

    [Fact]
    public void Validar_PesosQueNoSumanUno_ReportaSumaInvalida()
    {
        var criterios = new[]
        {
            CriterioRubrica.Crear("claridad", "Claridad", string.Empty, 0.5m, 1),
            CriterioRubrica.Crear("impacto", "Impacto", string.Empty, 0.2m, 2),
        };

        Validar(criterios).Should().ContainSingle(e => e.Campo == "criterios.pesos" && e.Motivo == "suma_invalida");
    }

    [Fact]
    public void Validar_PesosConRepartoDeTresTercios_EsValido()
    {
        var criterios = new[]
        {
            CriterioRubrica.Crear("uno", "Uno", string.Empty, 0.33m, 1),
            CriterioRubrica.Crear("dos", "Dos", string.Empty, 0.33m, 2),
            CriterioRubrica.Crear("tres", "Tres", string.Empty, 0.34m, 3),
        };

        Validar(criterios).Should().BeEmpty();
    }

    [Fact]
    public void Validar_OrdenDuplicado_LoReporta()
    {
        var criterios = new[]
        {
            CriterioRubrica.Crear("claridad", "Claridad", string.Empty, 0.5m, 1),
            CriterioRubrica.Crear("impacto", "Impacto", string.Empty, 0.5m, 1),
        };

        Validar(criterios).Should().Contain(e => e.Campo == "criterios.1.orden" && e.Motivo == "duplicado");
    }

    [Fact]
    public void Validar_OrdenConHueco_ReportaNoConsecutivo()
    {
        var criterios = new[]
        {
            CriterioRubrica.Crear("claridad", "Claridad", string.Empty, 0.5m, 1),
            CriterioRubrica.Crear("impacto", "Impacto", string.Empty, 0.5m, 7),
        };

        Validar(criterios).Should().Contain(e => e.Motivo == "no_consecutivo");
    }

    [Fact]
    public void Validar_EscalaInvertida_ReportaEscalaInvalida()
    {
        var errores = ValidadorRubricaEstructurada.Validar(
            new EscalaRubrica(5, 5),
            ValidadorRubricaEstructurada.NormalizarOrden(CriteriosUniformes(2))).Errores;

        errores.Should().ContainSingle(e => e.Campo == "escala" && e.Motivo == "invalida");
    }

    [Fact]
    public void Validar_DevuelveTodosLosMotivos_NoSoloElPrimero()
    {
        var criterios = new[]
        {
            CriterioRubrica.Crear("claridad", "Claridad", string.Empty, 0.5m, 1),
            CriterioRubrica.Crear("claridad", "Claridad", string.Empty, 0.9m, 1),
        };

        var errores = Validar(criterios);

        errores.Select(e => e.Motivo).Should().Contain(["duplicado", "suma_invalida"]);
        errores.Should().HaveCountGreaterThan(2);
    }

    [Fact]
    public void NormalizarOrden_SinOrdenExplicito_UsaLaPosicionDelArreglo()
    {
        var criterios = new[]
        {
            CriterioRubrica.Crear("claridad", 0.5m),
            CriterioRubrica.Crear("impacto", 0.5m),
        };

        ValidadorRubricaEstructurada.NormalizarOrden(criterios).Select(c => c.Orden).Should().Equal(1, 2);
    }

    [Fact]
    public void NormalizarOrden_OrdenParcial_NoInventaElFaltante()
    {
        var criterios = new[]
        {
            CriterioRubrica.Crear("claridad", "Claridad", string.Empty, 0.5m, 2),
            new CriterioRubrica("impacto", "Impacto", string.Empty, 0.5m, 0),
        };

        var normalizados = ValidadorRubricaEstructurada.NormalizarOrden(criterios);

        normalizados.Select(c => c.Orden).Should().Equal(2, 0);
        ValidadorRubricaEstructurada.Validar(new EscalaRubrica(1, 5), normalizados).Errores
            .Should().Contain(e => e.Campo == "criterios.1.orden" && e.Motivo == "no_consecutivo");
    }

    [Fact]
    public void Crear_MarkdownEsDerivadoYNoPuedeContradecirLaEstructura()
    {
        var rubrica = CrearRubrica(
            [
                CriterioRubrica.Crear("claridad", "Claridad", "Que tan concreta es.", 0.3m, 1),
                CriterioRubrica.Crear("viabilidad", "Viabilidad", "Que tan realizable es.", 0.7m, 2),
            ]);

        rubrica.ContenidoMarkdown.Should().Contain("claridad").And.Contain("Viabilidad");
        rubrica.ContenidoMarkdown.Should().NotContain("Impacto");
        rubrica.ContenidoMarkdown.Should().Contain("entre 1 y 5");
    }

    [Fact]
    public void Crear_DosVecesLaMismaEstructura_ProduceElMismoMarkdownYLaMismaHuella()
    {
        var primera = CrearRubrica(CriteriosUniformes(3));
        var segunda = CrearRubrica(CriteriosUniformes(3));

        segunda.ContenidoMarkdown.Should().Be(primera.ContenidoMarkdown);
        segunda.HashEstructura.Should().Be(primera.HashEstructura);
    }

    [Fact]
    public void Crear_PesoConDistintaEscalaDecimal_ConservaLaMismaHuella()
    {
        var conUnDecimal = CrearRubrica([CriterioRubrica.Crear("claridad", "Claridad", string.Empty, 1.0m, 1)]);
        var conCuatroDecimales = CrearRubrica([CriterioRubrica.Crear("claridad", "Claridad", string.Empty, 1.0000m, 1)]);

        conCuatroDecimales.HashEstructura.Should().Be(conUnDecimal.HashEstructura);
    }

    [Fact]
    public void Crear_EstructuraDistinta_CambiaLaHuella()
    {
        var tres = CrearRubrica(CriteriosUniformes(3));
        var cuatro = CrearRubrica(CriteriosUniformes(4));

        cuatro.HashEstructura.Should().NotBe(tres.HashEstructura);
    }

    [Fact]
    public void Rehidratar_DocumentoHistoricoSinIdNiOrden_SeLeeYQuedaNoVerificado()
    {
        var rubrica = Rubrica.Rehidratar(
            "r_legacy",
            "Rubrica legacy",
            "desc",
            instruccionesGenerales: null,
            contenidoMarkdownPersistido: "# Rubrica\n## Ejes\nClaridad, impacto, viabilidad, novedad y alcance.",
            new EscalaRubrica(1, 5),
            [CriterioRubrica.Crear("Impacto", 1m)],
            1,
            EstadoRubrica.Activa,
            Epoca,
            Epoca);

        // Se lee sin excepcion y conserva su Markdown original: no se muta el documento historico.
        rubrica.Criterios.Should().ContainSingle();
        rubrica.Criterios[0].Id.Should().Be("impacto");
        rubrica.Criterios[0].Orden.Should().Be(1);
        rubrica.ContenidoMarkdown.Should().Contain("## Ejes");

        // Pero la contradiccion entre estructura y Markdown la deja fuera de una asignacion nueva.
        rubrica.IntegridadEstructural.Should().Be(EstadoIntegridadRubrica.LegacyNoVerificada);
        rubrica.HabilitadaParaAsignacionNueva.Should().BeFalse();
    }

    [Fact]
    public void Rehidratar_EstructuraQueRompeLasReglas_QuedaInvalidaYSeSigueLeyendo()
    {
        var rubrica = Rubrica.Rehidratar(
            "r_rota",
            "Rubrica rota",
            "desc",
            null,
            "# Rubrica",
            new EscalaRubrica(1, 5),
            [CriterioRubrica.Crear("claridad", 0.2m), CriterioRubrica.Crear("impacto", 0.2m)],
            1,
            EstadoRubrica.Activa,
            Epoca,
            Epoca);

        rubrica.IntegridadEstructural.Should().Be(EstadoIntegridadRubrica.Invalida);
        rubrica.HabilitadaParaAsignacionNueva.Should().BeFalse();
    }

    [Fact]
    public void Rehidratar_MarkdownCompiladoPorElServidor_ConservaIntegridadValida()
    {
        var original = CrearRubrica(CriteriosUniformes(3));

        var releida = Rubrica.Rehidratar(
            original.Id,
            original.Nombre,
            original.Descripcion,
            original.InstruccionesGenerales,
            original.ContenidoMarkdown,
            original.Escala,
            original.Criterios,
            original.Version,
            original.Estado,
            original.CreadoEn,
            original.ActualizadoEn);

        releida.IntegridadEstructural.Should().Be(EstadoIntegridadRubrica.Valida);
        releida.ContenidoMarkdown.Should().Be(original.ContenidoMarkdown);
        releida.HashEstructura.Should().Be(original.HashEstructura);
    }

    private static IReadOnlyList<ErrorRubrica> Validar(IReadOnlyList<CriterioRubrica> criterios)
        => ValidadorRubricaEstructurada.Validar(
            new EscalaRubrica(1, 5),
            ValidadorRubricaEstructurada.NormalizarOrden(criterios)).Errores;

    private static Rubrica CrearRubrica(IReadOnlyList<CriterioRubrica> criterios)
        => Rubrica.Crear(
            "r_qa",
            "Rubrica QA",
            "Rubrica de prueba",
            new EscalaRubrica(1, 5),
            criterios,
            1,
            EstadoRubrica.Borrador,
            Epoca,
            Epoca,
            "Evalua con evidencia del aporte.");

    /// <summary>Criterios con pesos que suman exactamente 1 para cualquier cantidad.</summary>
    private static CriterioRubrica[] CriteriosUniformes(int cantidad)
    {
        var peso = decimal.Round(1m / cantidad, 6);
        var criterios = new CriterioRubrica[cantidad];
        for (var i = 0; i < cantidad; i++)
        {
            var pesoCriterio = i == cantidad - 1 ? 1m - (peso * (cantidad - 1)) : peso;
            criterios[i] = CriterioRubrica.Crear($"criterio_{i + 1}", $"Criterio {i + 1}", $"Descripcion {i + 1}", pesoCriterio, i + 1);
        }

        return criterios;
    }
}
