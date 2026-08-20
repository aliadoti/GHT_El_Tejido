using ElTejido.Application.Common;
using ElTejido.Application.Configuracion;
using ElTejido.Application.Conversacion;
using FluentAssertions;

namespace ElTejido.UnitTests.Configuracion;

/// <summary>
/// DT-P32-02 corte 1/3: la base curada deja de depender de App Settings, la fotografia legacy se
/// prevalida aparte sin truncar y el limite de frases por grupo pasa a ser operativo con techo duro.
/// </summary>
public sealed class CatalogosTextosSemillaBaseYLimitesTests
{
    [Theory]
    [InlineData("es")]
    [InlineData("en")]
    public void Base_TieneTodasLasClavesYSiempreValida(string idioma)
    {
        var semilla = CatalogosTextosSemilla.CrearBase(idioma);

        semilla.Idioma.Should().Be(idioma);
        semilla.Mensajes.Keys.Should().BeEquivalentTo(ValidadorCatalogoTextosConversacion.ClavesMensajes);
        semilla.Frases.Keys.Should().BeEquivalentTo(ValidadorCatalogoTextosConversacion.ClavesFrases);
        var acto = () => ValidadorCatalogoTextosConversacion.ValidarYCalcularHuella(
            semilla.Mensajes,
            semilla.Frases);
        acto.Should().NotThrow();
    }

    [Theory]
    [InlineData("no is all right for me")]
    [InlineData("it's all right for me")]
    [InlineData("it is all right for me")]
    [InlineData("no, it's all right for me")]
    [InlineData("no, it is all right for me")]
    [InlineData("I'm fine with it as is")]
    [InlineData("I am fine with it as is")]
    public void BaseIngles_ConformidadTrasConsulta_CubreVariantesDeterministas(string frase)
    {
        var semilla = CatalogosTextosSemilla.CrearBase("en");

        semilla.Frases["continuar"].Should().Contain(frase);
        semilla.Frases["confirmar"].Should().Contain(frase);
        semilla.Frases["acuseConsultaIdea"].Should().Contain(frase);
    }

    [Fact]
    public void Base_IgnoraLaConfiguracionLegacyAunqueSeaInvalida()
    {
        var opciones = OpcionesConLegacyInvalido();

        var basePura = CatalogosTextosSemilla.CrearBase("es");

        // La lista legacy invalida no llega a la base ni impide validarla (spec §10.1).
        basePura.Frases["despertarProactivo"].Should()
            .HaveCountLessOrEqualTo(PoliticaLimitesCatalogoTextos.MaxFrasesPorGrupoDefault)
            .And.NotContain("frase legacy 0");
        basePura.Mensajes["saludoPrimerContacto"].Should()
            .Be(OpcionesMensajesConversacion.SaludoPrimerContactoDefault)
            .And.NotBe(opciones.Mensajes.SaludoPrimerContacto);
        var acto = () => ValidadorCatalogoTextosConversacion.ValidarYCalcularHuella(
            basePura.Mensajes,
            basePura.Frases);
        acto.Should().NotThrow();
    }

    [Fact]
    public void Legacy_ConservaTodasLasEntradasSinTruncarNiMezclarConDefaults()
    {
        var opciones = OpcionesConLegacyInvalido();

        var legacy = CatalogosTextosSemilla.CrearDesdeLegacy("es", opciones);

        legacy.Frases["despertarProactivo"].Should()
            .HaveCount(31)
            .And.BeEquivalentTo(opciones.FrasesDespertarProactivo)
            .And.NotContain(DetectorEntradaProactiva.FrasesPorDefecto.First());
        legacy.Mensajes["saludoPrimerContacto"].Should().Be(opciones.Mensajes.SaludoPrimerContacto);
    }

    [Fact]
    public void Prevalidar_LegacyPorEncimaDelLimite_ReportaGrupoYNoLanza()
    {
        var legacy = CatalogosTextosSemilla.CrearDesdeLegacy("es", OpcionesConLegacyInvalido());
        var limiteP32 = PoliticaLimitesCatalogoTextos.Crear(
            30,
            PoliticaLimitesCatalogoTextos.MaxBytesImportacionJsonDefault);

        var resultado = ValidadorCatalogoTextosConversacion.Prevalidar(
            legacy.FamiliaId,
            legacy.Idioma,
            legacy.Mensajes,
            legacy.Frases,
            limiteP32);

        resultado.Valido.Should().BeFalse();
        resultado.Errores.Should().ContainSingle()
            .Which.Should().Be(new DetalleError("frases.despertarProactivo", "debe_tener_entre_1_y_30_elementos"));
        resultado.Conteos.Mensajes.Should().Be(ValidadorCatalogoTextosConversacion.ClavesMensajes.Count);
        resultado.Conteos.GruposFrases.Should().Be(ValidadorCatalogoTextosConversacion.ClavesFrases.Count);
        resultado.Conteos.Frases.Should().BeGreaterThan(31);
    }

    [Fact]
    public void Prevalidar_LegacyPorEncimaDelLimite_EsValidoConElLimiteOperativoAmpliado()
    {
        var legacy = CatalogosTextosSemilla.CrearDesdeLegacy("es", OpcionesConLegacyInvalido());

        var resultado = ValidadorCatalogoTextosConversacion.Prevalidar(
            legacy.FamiliaId,
            legacy.Idioma,
            legacy.Mensajes,
            legacy.Frases);

        // El default operativo pasa de 30 a 100 sin recompilar nada (spec §2.4).
        resultado.Valido.Should().BeTrue();
        resultado.Errores.Should().BeEmpty();
    }

    [Fact]
    public void Validar_GrupoPorEncimaDelLimiteConfigurado_RechazaYNoRecorta()
    {
        var semilla = CatalogosTextosSemilla.CrearBase("es");
        var frases = semilla.Frases.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
        var excedido = Enumerable.Range(0, PoliticaLimitesCatalogoTextos.MaxFrasesPorGrupoDefault + 1)
            .Select(indice => $"frase {indice}")
            .ToArray();
        frases["continuar"] = excedido;

        var acto = () => ValidadorCatalogoTextosConversacion.ValidarYCalcularHuella(semilla.Mensajes, frases);

        acto.Should().Throw<ErrorValidacion>()
            .Which.Detalles.Should().Contain(x =>
                x.Campo == "frases.continuar" && x.Problema == "debe_tener_entre_1_y_100_elementos");
        frases["continuar"].Should().HaveCount(excedido.Length);
    }

    [Theory]
    [InlineData(0, PoliticaLimitesCatalogoTextos.MinFrasesPorGrupo)]
    [InlineData(30, 30)]
    [InlineData(200, 200)]
    [InlineData(5000, PoliticaLimitesCatalogoTextos.TechoFrasesPorGrupo)]
    public void Opciones_AjustanElLimiteDeFrasesAlTechoCompilado(int configurado, int esperado)
    {
        var opciones = new OpcionesCatalogoTextos { MaxFrasesPorGrupo = configurado };

        opciones.MaxFrasesPorGrupo.Should().Be(esperado);
        opciones.Limites.MaxFrasesPorGrupo.Should().Be(esperado);
    }

    [Theory]
    [InlineData(0, PoliticaLimitesCatalogoTextos.MinBytesImportacionJson)]
    [InlineData(524288, 524288)]
    [InlineData(99999999, PoliticaLimitesCatalogoTextos.TechoBytesImportacionJson)]
    public void Opciones_AjustanElLimiteDeImportacionAlTechoCompilado(int configurado, int esperado)
    {
        var opciones = new OpcionesCatalogoTextos { MaxBytesImportacionJson = configurado };

        opciones.MaxBytesImportacionJson.Should().Be(esperado);
        opciones.Limites.MaxBytesImportacionJson.Should().Be(esperado);
    }

    [Fact]
    public void Opciones_SinConfiguracion_UsanLosDefaultsDeLaSpec()
    {
        var opciones = new OpcionesCatalogoTextos();

        opciones.MaxFrasesPorGrupo.Should().Be(100);
        opciones.MaxBytesImportacionJson.Should().Be(262144);
    }

    /// <summary>Reproduce la corrida del 2026-08-13: 31 frases heredadas invalidaban todo el catalogo.</summary>
    private static OpcionesConversacion OpcionesConLegacyInvalido()
    {
        var opciones = new OpcionesConversacion();
        opciones.Mensajes.SaludoPrimerContacto = "Saludo heredado del ambiente.";
        for (var indice = 0; indice < 31; indice++)
        {
            opciones.FrasesDespertarProactivo.Add($"frase legacy {indice}");
        }

        return opciones;
    }
}
