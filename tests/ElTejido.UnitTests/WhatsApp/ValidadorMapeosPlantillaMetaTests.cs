using ElTejido.Application.WhatsApp;
using ElTejido.Domain.Campanas;
using ElTejido.Domain.Common;
using FluentAssertions;

namespace ElTejido.UnitTests.WhatsApp;

/// <summary>
/// DT-P32-03 §3.2: el agregado de mapeos Meta es la señal que decide si tiene sentido encender el
/// gate. Estas pruebas fijan qué pares se enumeran y qué cuenta como problema estructural.
/// </summary>
public sealed class ValidadorMapeosPlantillaMetaTests
{
    private static readonly string[] Idiomas = ["es", "en"];

    [Fact]
    public void Evaluar_ParConfigurado_QuedaListo()
    {
        var opciones = Opciones(("inicio_campania", "en", "el_tejido_inicio", "en_US", ["nombre"]));

        var mapeos = ValidadorMapeosPlantillaMeta.Evaluar(
            [CampaniaCon(("en", "inicio_campania"))],
            Idiomas,
            opciones);

        var mapeo = mapeos.Should().ContainSingle().Subject;
        mapeo.PlantillaRef.Should().Be("inicio_campania");
        mapeo.Idioma.Should().Be("en");
        mapeo.Configurado.Should().BeTrue();
        mapeo.NombreConfigurado.Should().BeTrue();
        mapeo.IdiomaMetaConfigurado.Should().BeTrue();
        mapeo.Componentes.Should().Equal("nombre");
        mapeo.Problemas.Should().BeEmpty();
        mapeo.Listo.Should().BeTrue();
        mapeo.Campanias.Should().ContainSingle().Which.CampaniaId.Should().Be("c_1");
    }

    [Fact]
    public void Evaluar_PlantillaSinVariables_PuedeQuedarListaConComponentesVacios()
    {
        // Criterio de aceptación 8: una lista vacía es legítima y no bloquea por sí sola.
        var opciones = Opciones(("inicio_campania", "en", "el_tejido_inicio", "en_US", []));

        var mapeos = ValidadorMapeosPlantillaMeta.Evaluar(
            [CampaniaCon(("en", "inicio_campania"))],
            Idiomas,
            opciones);

        var mapeo = mapeos.Should().ContainSingle().Subject;
        mapeo.Componentes.Should().BeEmpty();
        mapeo.Problemas.Should().BeEmpty();
        mapeo.Listo.Should().BeTrue();
    }

    [Fact]
    public void Evaluar_ParSinMapeo_ReportaNombreYCodigoFaltantes()
    {
        var mapeos = ValidadorMapeosPlantillaMeta.Evaluar(
            [CampaniaCon(("en", "inicio_campania"))],
            Idiomas,
            new OpcionesPlantillaEnvioInicial());

        var mapeo = mapeos.Should().ContainSingle().Subject;
        mapeo.Configurado.Should().BeFalse();
        mapeo.Problemas.Should().BeEquivalentTo(
            [ValidadorMapeosPlantillaMeta.NombreFaltante, ValidadorMapeosPlantillaMeta.IdiomaMetaFaltante]);
        mapeo.Listo.Should().BeFalse();
    }

    [Fact]
    public void Evaluar_SinIdiomaMeta_NoQuedaListo()
    {
        // Criterio de aceptación 6: la ausencia del código Meta debe bloquear el gate.
        var opciones = Opciones(("inicio_campania", "en", "el_tejido_inicio", "", ["nombre"]));

        var mapeos = ValidadorMapeosPlantillaMeta.Evaluar(
            [CampaniaCon(("en", "inicio_campania"))],
            Idiomas,
            opciones);

        var mapeo = mapeos.Should().ContainSingle().Subject;
        mapeo.NombreConfigurado.Should().BeTrue();
        mapeo.IdiomaMetaConfigurado.Should().BeFalse();
        mapeo.Configurado.Should().BeFalse();
        mapeo.Problemas.Should().Contain(ValidadorMapeosPlantillaMeta.IdiomaMetaFaltante);
        mapeo.Listo.Should().BeFalse();
    }

    [Fact]
    public void Evaluar_ComponenteVacioODuplicado_SeReportaAunqueElParResuelva()
    {
        // Criterio de aceptación 7: `TryResolver` acepta estos casos, el diagnóstico no.
        var opciones = Opciones(
            ("inicio_campania", "en", "el_tejido_inicio", "en_US", ["nombre", "  ", "nombre"]));

        var mapeos = ValidadorMapeosPlantillaMeta.Evaluar(
            [CampaniaCon(("en", "inicio_campania"))],
            Idiomas,
            opciones);

        var mapeo = mapeos.Should().ContainSingle().Subject;
        mapeo.Configurado.Should().BeTrue();
        mapeo.Problemas.Should().BeEquivalentTo(
            [ValidadorMapeosPlantillaMeta.ComponenteVacio, ValidadorMapeosPlantillaMeta.ComponenteDuplicado]);
        mapeo.Componentes.Should().Equal("nombre", "nombre");
        mapeo.Listo.Should().BeFalse();
    }

    [Fact]
    public void Evaluar_MensajeActivoSinPlantillaRef_ReportaElFaltanteConSuCampania()
    {
        var campania = CampaniaCon(("en", null));

        var mapeos = ValidadorMapeosPlantillaMeta.Evaluar([campania], Idiomas, new OpcionesPlantillaEnvioInicial());

        var mapeo = mapeos.Should().ContainSingle().Subject;
        mapeo.PlantillaRef.Should().BeNull();
        mapeo.Idioma.Should().Be("en");
        mapeo.Problemas.Should().Equal(ValidadorMapeosPlantillaMeta.PlantillaRefFaltante);
        mapeo.Listo.Should().BeFalse();
        var requirente = mapeo.Campanias.Should().ContainSingle().Subject;
        requirente.Nombre.Should().Be("Campania");
        requirente.Estado.Should().Be("activa");
        requirente.MensajeInicialId.Should().Be("mi_1");
    }

    [Fact]
    public void Evaluar_MensajeInactivo_NoExigeMapeo()
    {
        var campania = CampaniaCon(
            [("en", "inicio_campania")],
            estadoMensaje: EstadoRegistro.Inactivo);

        var mapeos = ValidadorMapeosPlantillaMeta.Evaluar(
            [campania],
            Idiomas,
            new OpcionesPlantillaEnvioInicial());

        mapeos.Should().BeEmpty();
    }

    [Fact]
    public void Evaluar_IdiomaFueraDeAlcance_NoSeEnumera()
    {
        var campania = CampaniaCon(("en", "inicio_campania"));

        var mapeos = ValidadorMapeosPlantillaMeta.Evaluar(
            [campania],
            ["es"],
            new OpcionesPlantillaEnvioInicial());

        mapeos.Should().BeEmpty();
    }

    [Fact]
    public void Evaluar_DosCampaniasConElMismoAlias_DeduplicaYAcumulaRequirentes()
    {
        var opciones = Opciones(("inicio_campania", "en", "el_tejido_inicio", "en_US", ["nombre"]));
        var primera = CampaniaCon([("en", "inicio_campania")], id: "c_1");
        var segunda = CampaniaCon(
            [("en", "inicio_campania")],
            id: "c_2",
            estado: EstadoCampania.Borrador);

        var mapeos = ValidadorMapeosPlantillaMeta.Evaluar([primera, segunda], ["en"], opciones);

        var mapeo = mapeos.Should().ContainSingle().Subject;
        mapeo.Campanias.Select(x => x.CampaniaId).Should().Equal("c_1", "c_2");
        mapeo.Campanias.Select(x => x.Estado).Should().Equal("activa", "borrador");
    }

    [Fact]
    public void Evaluar_MismoAliasEnDosIdiomas_EnumeraUnParPorIdioma()
    {
        // El par es alias + idioma: tener `es` configurado no exime de configurar `en`.
        var opciones = Opciones(("inicio_campania", "es", "el_tejido_inicio", "es_CO", ["nombre"]));
        var campania = CampaniaCon([("es", "inicio_campania"), ("en", "inicio_campania")]);

        var mapeos = ValidadorMapeosPlantillaMeta.Evaluar([campania], Idiomas, opciones);

        mapeos.Should().HaveCount(2);
        mapeos.Single(x => x.Idioma == "es").Listo.Should().BeTrue();
        mapeos.Single(x => x.Idioma == "en").Listo.Should().BeFalse();
    }

    // --- DT-P32-03-01 §2: quién bloquea el gate y quién solo queda pendiente ---

    [Fact]
    public void Evaluar_ParFaltanteDeCampaniaActiva_BloqueaElGate()
    {
        var mapeos = ValidadorMapeosPlantillaMeta.Evaluar(
            [CampaniaCon(("en", "inicio_campania"))],
            Idiomas,
            new OpcionesPlantillaEnvioInicial());

        var mapeo = mapeos.Should().ContainSingle().Subject;
        mapeo.Listo.Should().BeFalse();
        mapeo.BloqueaGateOn.Should().BeTrue();
    }

    [Fact]
    public void Evaluar_ParFaltanteSoloDeBorrador_SeEnumeraPeroNoBloquea()
    {
        // Criterio de aceptación 2: un borrador a medio construir es trabajo normal; se diagnostica
        // igual, pero no puede mantener el gate apagado para las campañas que ya operan.
        var borrador = CampaniaCon([("en", "inicio_campania")], estado: EstadoCampania.Borrador);

        var mapeos = ValidadorMapeosPlantillaMeta.Evaluar([borrador], Idiomas, new OpcionesPlantillaEnvioInicial());

        var mapeo = mapeos.Should().ContainSingle().Subject;
        mapeo.Listo.Should().BeFalse();
        mapeo.Problemas.Should().NotBeEmpty();
        mapeo.BloqueaGateOn.Should().BeFalse();
    }

    [Fact]
    public void Evaluar_ParCompartidoPorActivaYBorrador_BloqueaUnaSolaVezYConservaAmbas()
    {
        // Criterio de aceptación 3: basta una consumidora activa para que el par frene el gate.
        var activa = CampaniaCon([("en", "inicio_campania")], id: "c_1");
        var borrador = CampaniaCon([("en", "inicio_campania")], id: "c_2", estado: EstadoCampania.Borrador);

        var mapeos = ValidadorMapeosPlantillaMeta.Evaluar([borrador, activa], ["en"], new OpcionesPlantillaEnvioInicial());

        var mapeo = mapeos.Should().ContainSingle().Subject;
        mapeo.BloqueaGateOn.Should().BeTrue();
        mapeo.Campanias.Select(x => x.CampaniaId).Should().Equal("c_2", "c_1");
    }

    private static OpcionesPlantillaEnvioInicial Opciones(
        params (string PlantillaRef, string Idioma, string Nombre, string IdiomaMeta, string[] Componentes)[] mapeos)
    {
        var opciones = new OpcionesPlantillaEnvioInicial();
        foreach (var mapeo in mapeos)
        {
            if (!opciones.Mapeos.TryGetValue(mapeo.PlantillaRef, out var porIdioma))
            {
                porIdioma = new Dictionary<string, PlantillaEnvioInicialConfigurada>(StringComparer.OrdinalIgnoreCase);
                opciones.Mapeos[mapeo.PlantillaRef] = porIdioma;
            }

            porIdioma[mapeo.Idioma] = new PlantillaEnvioInicialConfigurada
            {
                Nombre = mapeo.Nombre,
                Idioma = mapeo.IdiomaMeta,
                Componentes = mapeo.Componentes,
            };
        }

        return opciones;
    }

    private static Campania CampaniaCon((string Idioma, string? PlantillaRef) localizacion)
        => CampaniaCon([localizacion]);

    private static Campania CampaniaCon(
        IReadOnlyCollection<(string Idioma, string? PlantillaRef)> localizaciones,
        string id = "c_1",
        EstadoCampania estado = EstadoCampania.Activa,
        EstadoRegistro estadoMensaje = EstadoRegistro.Activo)
    {
        var mensaje = MensajeInicial.Crear(
            "mi_1",
            "saludo",
            "Hola {{nombre}}.",
            1,
            ["nombre"],
            estadoMensaje,
            PlantillaWhatsApp.Crear("legacy_saludo", "es", ["nombre"]));

        return Campania.Crear(
            id,
            "Campania",
            "Descripcion",
            "Objetivo",
            estado,
            [mensaje],
            [],
            "rub_1",
            null,
            "llm_1",
            ConfigMarkdown.Crear(TipoArtefactoMarkdown.Respuesta),
            ConfigConversacional.Crear(1, "Gracias."),
            LimitesSeguridad.Crear(1500, 10, 2),
            [],
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch,
            idiomasHabilitados: localizaciones.Select(x => x.Idioma).ToArray(),
            localizaciones: localizaciones.ToDictionary(
                x => x.Idioma,
                x => LocalizacionCampania.Crear(
                    x.Idioma,
                    "Campania",
                    "Descripcion",
                    "Objetivo",
                    "Gracias.",
                    new Dictionary<string, LocalizacionMensajeInicial>(StringComparer.Ordinal)
                    {
                        ["mi_1"] = new("Hola {{nombre}}.", x.PlantillaRef),
                    },
                    null),
                StringComparer.Ordinal));
    }
}
