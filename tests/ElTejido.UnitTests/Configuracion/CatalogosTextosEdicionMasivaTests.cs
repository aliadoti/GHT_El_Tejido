using ElTejido.Application.Campanas;
using ElTejido.Application.Common;
using ElTejido.Application.Configuracion;
using ElTejido.Application.Conversacion;
using ElTejido.Application.Seguridad;
using ElTejido.Application.WhatsApp;
using ElTejido.Domain.Campanas;
using ElTejido.Domain.Common;
using ElTejido.Domain.Configuracion;
using ElTejido.Domain.Seguridad;
using ElTejido.Infrastructure.Persistencia.Memoria;
using FluentAssertions;
using NSubstitute;

namespace ElTejido.UnitTests.Configuracion;

/// <summary>
/// DT-P32-02 corte 2/3: la edicion masiva crea siempre una version nueva en borrador, la
/// prevalidacion no escribe y el readiness distingue gate, activo, borrador y bloqueos.
/// </summary>
public sealed class CatalogosTextosEdicionMasivaTests
{
    private readonly RepositorioCatalogosTextosMemoria _repositorio = new();
    private readonly IRepositorioLogSeguridad _logs = Substitute.For<IRepositorioLogSeguridad>();

    [Fact]
    public async Task ImportarMasivo_ArchivoValido_CreaVersionSiguienteEnBorradorEIgnoraSusMetadatos()
    {
        var servicio = Servicio();
        await servicio.ImportarMasivoAsync(Edicion(), "admin", CancellationToken.None);

        var segunda = await servicio.ImportarMasivoAsync(
            Edicion(sufijo: "editado"),
            "admin",
            CancellationToken.None);

        segunda.Catalogo.Version.Should().Be(2);
        segunda.Catalogo.Estado.Should().Be(EstadoCatalogoTextos.Borrador);
        segunda.Catalogo.Mensajes["acuseContinuar"].Should().Contain("editado");
        (await _repositorio.ObtenerActivoAsync("es", CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task ImportarMasivo_ContenidoInvalido_DevuelveTodosLosErroresYNoEscribe()
    {
        var servicio = Servicio();
        var contenido = ContenidoValido();
        var mensajes = contenido.Mensajes.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
        mensajes["acuseContinuar"] = " ";
        mensajes["saludoReactivacion"] = "Hola {{secreto}}";
        var frases = contenido.Frases.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
        frases["continuar"] = new[] { "listo", "  LISTO " };

        var accion = () => servicio.ImportarMasivoAsync(
            new SolicitudEdicionMasivaCatalogoTextos(
                new SolicitudGuardarCatalogoTextos(Familia, "es", mensajes, frases),
                [new DetalleError("formato", "no_soportado")],
                TamanoBytes: 4096),
            "admin",
            CancellationToken.None);

        var error = (await accion.Should().ThrowAsync<ErrorValidacion>()).Which;
        error.Detalles.Should().Contain(x => x.Problema == "no_soportado");
        error.Detalles.Should().Contain(x => x.Campo == "mensajes.acuseContinuar" && x.Problema == "vacio");
        error.Detalles.Should().Contain(x => x.Problema == "placeholder_no_permitido:secreto");
        error.Detalles.Should().Contain(x => x.Problema == "frase_duplicada");
        (await _repositorio.BuscarAsync("es", null, CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task ImportarMasivo_IdiomaDistintoAlSeleccionado_SeRechazaSinCorregirlo()
    {
        var servicio = Servicio();
        var solicitud = Edicion(idioma: "en") with { IdiomaEsperado = "es" };

        var accion = () => servicio.ImportarMasivoAsync(solicitud, "admin", CancellationToken.None);

        (await accion.Should().ThrowAsync<ErrorValidacion>()).Which.Detalles
            .Should().Contain(x => x.Campo == "idioma" && x.Problema == "no_coincide_con_seleccion");
        (await _repositorio.BuscarAsync("en", null, CancellationToken.None)).Should().BeEmpty();
        (await _repositorio.BuscarAsync("es", null, CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task ImportarMasivo_FamiliaDistintaALaSeleccionada_SeRechaza()
    {
        var servicio = Servicio();
        var solicitud = Edicion() with { FamiliaIdEsperada = "otra_familia" };

        var accion = () => servicio.ImportarMasivoAsync(solicitud, "admin", CancellationToken.None);

        (await accion.Should().ThrowAsync<ErrorValidacion>()).Which.Detalles
            .Should().Contain(x => x.Campo == "familiaId" && x.Problema == "no_coincide_con_seleccion");
    }

    [Fact]
    public async Task Prevalidar_NoEscribeYAuditaConteosSinContenido()
    {
        var servicio = Servicio();

        var resultado = await servicio.PrevalidarImportacionAsync(
            Edicion(sufijo: "texto-control-no-auditar"),
            "admin",
            CancellationToken.None);

        resultado.Valido.Should().BeTrue();
        resultado.Conteos.Mensajes.Should().Be(ValidadorCatalogoTextosConversacion.ClavesMensajes.Count);
        resultado.Conteos.GruposFrases.Should().Be(ValidadorCatalogoTextosConversacion.ClavesFrases.Count);
        (await _repositorio.BuscarAsync("es", null, CancellationToken.None)).Should().BeEmpty();
        await _logs.Received().RegistrarAsync(
            Arg.Is<LogSeguridad>(log =>
                log.Detalle != null
                && log.Detalle.Contains("accion=prevalidarImportacion", StringComparison.Ordinal)
                && log.Detalle.Contains("bytes=", StringComparison.Ordinal)
                && !log.Detalle.Contains("texto-control-no-auditar", StringComparison.Ordinal)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Prevalidar_EImportar_CoincidenEnElVeredicto()
    {
        var servicio = Servicio();
        var solicitud = Edicion();

        var prevalidacion = await servicio.PrevalidarImportacionAsync(solicitud, "admin", CancellationToken.None);
        var importada = await servicio.ImportarMasivoAsync(solicitud, "admin", CancellationToken.None);

        prevalidacion.Valido.Should().BeTrue();
        importada.Catalogo.Version.Should().Be(1);
    }

    // --- Readiness (§4.1) ---

    [Fact]
    public async Task Readiness_SinCatalogos_ReportaGateRealYCampaniasBloqueadas()
    {
        var campanias = new RepositorioCampaniasMemoria();
        await campanias.GuardarCampaniaAsync(CampaniaBilingue(), CancellationToken.None);
        var readiness = Readiness(campanias, gate: false);

        var resultado = await readiness.ObtenerAsync(null, CancellationToken.None);

        resultado.GateHabilitado.Should().BeFalse();
        resultado.MaxFrasesPorGrupo.Should().Be(PoliticaLimitesCatalogoTextos.MaxFrasesPorGrupoDefault);
        resultado.Idiomas.Should().HaveCount(2);
        var ingles = resultado.Idiomas.Single(x => x.Idioma == "en");
        ingles.Listo.Should().BeFalse();
        ingles.TieneActivo.Should().BeFalse();
        ingles.TieneBorrador.Should().BeFalse();
        // La base curada siempre puede generarse, aunque el ambiente no tenga catalogo.
        ingles.SemillaBaseDisponible.Should().BeTrue();
        ingles.CampaniasBloqueadas.Should().ContainSingle()
            .Which.Motivo.Should().Be("catalogo_activo_faltante");
    }

    [Fact]
    public async Task Readiness_ConCatalogoActivo_QuedaListoYSinBloqueos()
    {
        var campanias = new RepositorioCampaniasMemoria();
        await campanias.GuardarCampaniaAsync(CampaniaBilingue(), CancellationToken.None);
        var servicio = Servicio();
        var creado = await servicio.ImportarMasivoAsync(Edicion(idioma: "en"), "admin", CancellationToken.None);
        await servicio.ActivarAsync(Familia, "en", creado.Catalogo.Version, creado.Etag, "admin", CancellationToken.None);

        var resultado = await Readiness(campanias, gate: true).ObtenerAsync("en", CancellationToken.None);

        resultado.GateHabilitado.Should().BeTrue();
        var ingles = resultado.Idiomas.Should().ContainSingle().Which;
        ingles.Listo.Should().BeTrue();
        ingles.VersionActiva.Should().Be(1);
        ingles.ActivaValida.Should().BeTrue();
        ingles.HuellaActiva.Should().NotBeNullOrEmpty();
        ingles.CampaniasBloqueadas.Should().BeEmpty();
    }

    /// <summary>
    /// DT-P32-03 §3.2: `idiomas[].listo` sigue hablando solo del catalogo; la senal que decide si
    /// tiene sentido encender el gate es `listoParaGateOn`, que ademas exige los mapeos Meta.
    /// </summary>
    [Fact]
    public async Task Readiness_ConCatalogoListoPeroSinMapeoMeta_NoQuedaListoParaGateOn()
    {
        var campanias = new RepositorioCampaniasMemoria();
        await campanias.GuardarCampaniaAsync(CampaniaConPlantilla("inicio_campania"), CancellationToken.None);
        await ActivarCatalogoIngles();

        var resultado = await Readiness(campanias, gate: true, new OpcionesPlantillaEnvioInicial())
            .ObtenerAsync("en", CancellationToken.None);

        resultado.Idiomas.Should().ContainSingle().Which.Listo.Should().BeTrue();
        resultado.ListoParaGateOn.Should().BeFalse();
        var mapeo = resultado.MapeosMeta.Should().ContainSingle().Subject;
        mapeo.PlantillaRef.Should().Be("inicio_campania");
        mapeo.Idioma.Should().Be("en");
        mapeo.Configurado.Should().BeFalse();
        // DT-P32-03-01 §7.1: lo pide una campania activa, asi que frena el gate.
        mapeo.BloqueaGateOn.Should().BeTrue();
        mapeo.Campanias.Should().ContainSingle().Which.CampaniaId.Should().Be("c_plantilla");
    }

    /// <summary>
    /// DT-P32-03-01 §7.2 y §7.4: un borrador a medio construir se sigue diagnosticando, pero no puede
    /// mantener `listoParaGateOn` en `false` para las campanias que ya estan activas.
    /// </summary>
    [Fact]
    public async Task Readiness_ConMapeoFaltanteSoloEnBorrador_QuedaListoParaGateOn()
    {
        var campanias = new RepositorioCampaniasMemoria();
        await campanias.GuardarCampaniaAsync(
            CampaniaConPlantilla("inicio_borrador", id: "c_borrador", estado: EstadoCampania.Borrador),
            CancellationToken.None);
        await ActivarCatalogoIngles();

        var resultado = await Readiness(campanias, gate: true, new OpcionesPlantillaEnvioInicial())
            .ObtenerAsync("en", CancellationToken.None);

        resultado.ListoParaGateOn.Should().BeTrue();
        var mapeo = resultado.MapeosMeta.Should().ContainSingle().Subject;
        mapeo.Listo.Should().BeFalse();
        mapeo.BloqueaGateOn.Should().BeFalse();
        mapeo.Problemas.Should().NotBeEmpty();
        mapeo.Campanias.Should().ContainSingle().Which.Estado.Should().Be("borrador");
    }

    /// <summary>
    /// DT-P32-03-01 §7.4: con los pares de las campanias activas configurados, un borrador incompleto
    /// aparte no cambia la senal agregada.
    /// </summary>
    [Fact]
    public async Task Readiness_ConActivaCompletaYBorradorIncompleto_ConservaLaSenalEnVerde()
    {
        var campanias = new RepositorioCampaniasMemoria();
        await campanias.GuardarCampaniaAsync(CampaniaConPlantilla("inicio_campania"), CancellationToken.None);
        await campanias.GuardarCampaniaAsync(
            CampaniaConPlantilla("inicio_borrador", id: "c_borrador", estado: EstadoCampania.Borrador),
            CancellationToken.None);
        await ActivarCatalogoIngles();

        var resultado = await Readiness(campanias, gate: true, PlantillasIngles("inicio_campania"))
            .ObtenerAsync("en", CancellationToken.None);

        resultado.ListoParaGateOn.Should().BeTrue();
        resultado.MapeosMeta.Should().HaveCount(2);
        resultado.MapeosMeta.Single(x => x.PlantillaRef == "inicio_campania").BloqueaGateOn.Should().BeTrue();
        resultado.MapeosMeta.Single(x => x.PlantillaRef == "inicio_borrador").BloqueaGateOn.Should().BeFalse();
    }

    [Fact]
    public async Task Readiness_ConCatalogoYMapeoCompletos_QuedaListoParaGateOn()
    {
        var campanias = new RepositorioCampaniasMemoria();
        await campanias.GuardarCampaniaAsync(CampaniaConPlantilla("inicio_campania"), CancellationToken.None);
        await ActivarCatalogoIngles();
        var plantillas = new OpcionesPlantillaEnvioInicial
        {
            Mapeos =
            {
                ["inicio_campania"] = new Dictionary<string, PlantillaEnvioInicialConfigurada>(
                    StringComparer.OrdinalIgnoreCase)
                {
                    ["en"] = new() { Nombre = "el_tejido_inicio", Idioma = "en_US", Componentes = ["nombre"] },
                },
            },
        };

        var resultado = await Readiness(campanias, gate: true, plantillas)
            .ObtenerAsync("en", CancellationToken.None);

        resultado.ListoParaGateOn.Should().BeTrue();
        resultado.MapeosMeta.Should().ContainSingle().Which.Listo.Should().BeTrue();
    }

    [Fact]
    public async Task Readiness_ConMensajeInicialSinAlias_ReportaPlantillaRefFaltante()
    {
        var campanias = new RepositorioCampaniasMemoria();
        await campanias.GuardarCampaniaAsync(CampaniaConPlantilla(plantillaRef: null), CancellationToken.None);
        await ActivarCatalogoIngles();

        var resultado = await Readiness(campanias, gate: true, new OpcionesPlantillaEnvioInicial())
            .ObtenerAsync("en", CancellationToken.None);

        resultado.ListoParaGateOn.Should().BeFalse();
        var mapeo = resultado.MapeosMeta.Should().ContainSingle().Subject;
        mapeo.PlantillaRef.Should().BeNull();
        mapeo.Problemas.Should().Equal(ValidadorMapeosPlantillaMeta.PlantillaRefFaltante);
        mapeo.Campanias.Should().ContainSingle().Which.MensajeInicialId.Should().Be("mi_1");
    }

    [Fact]
    public async Task Readiness_ConLegacyExcedido_LoReportaSinImpedirLaSemillaBase()
    {
        var opcionesLegacy = new OpcionesConversacion();
        for (var indice = 0; indice < 31; indice++)
        {
            opcionesLegacy.FrasesDespertarProactivo.Add($"frase legacy {indice}");
        }

        var readiness = new ServicioReadinessCatalogosTextos(
            _repositorio,
            new RepositorioCampaniasMemoria(),
            new OpcionesCatalogoTextos { MaxFrasesPorGrupo = 30 },
            opcionesLegacy);

        var espanol = (await readiness.ObtenerAsync("es", CancellationToken.None)).Idiomas.Single();

        espanol.LegacyValido.Should().BeFalse();
        espanol.ProblemasLegacy.Should().ContainSingle()
            .Which.Campo.Should().Be("frases.despertarProactivo");
        espanol.ConteosLegacy.Frases.Should().BeGreaterThan(31);
        espanol.SemillaBaseDisponible.Should().BeTrue();
    }

    [Fact]
    public async Task Readiness_IdiomaInvalido_DevuelveValidacion()
    {
        var accion = () => Readiness(new RepositorioCampaniasMemoria(), gate: false)
            .ObtenerAsync("fr", CancellationToken.None);

        await accion.Should().ThrowAsync<ErrorValidacion>();
    }

    private const string Familia = CatalogosTextosSemilla.FamiliaId;

    private ServicioGestionCatalogosTextos Servicio()
        => new(_repositorio, _logs, TimeProvider.System);

    private ServicioReadinessCatalogosTextos Readiness(
        IRepositorioCampanias campanias,
        bool gate,
        OpcionesPlantillaEnvioInicial? plantillas = null)
        => new(
            _repositorio,
            campanias,
            new OpcionesCatalogoTextos { Habilitado = gate },
            new OpcionesConversacion(),
            plantillas);

    private async Task ActivarCatalogoIngles()
    {
        var servicio = Servicio();
        var creado = await servicio.ImportarMasivoAsync(Edicion(idioma: "en"), "admin", CancellationToken.None);
        await servicio.ActivarAsync(
            Familia,
            "en",
            creado.Catalogo.Version,
            creado.Etag,
            "admin",
            CancellationToken.None);
    }

    private static SolicitudEdicionMasivaCatalogoTextos Edicion(
        string idioma = "es",
        string sufijo = "base")
    {
        var contenido = ContenidoValido(sufijo);
        return new SolicitudEdicionMasivaCatalogoTextos(
            new SolicitudGuardarCatalogoTextos(Familia, idioma, contenido.Mensajes, contenido.Frases),
            [],
            TamanoBytes: 2048);
    }

    private static SolicitudContenidoCatalogoTextos ContenidoValido(string sufijo = "base")
    {
        var mensajes = ValidadorCatalogoTextosConversacion.ClavesMensajes
            .ToDictionary(x => x, x => $"{x} {sufijo}", StringComparer.Ordinal);
        var frases = ValidadorCatalogoTextosConversacion.ClavesFrases
            .ToDictionary(
                x => x,
                x => (IReadOnlyCollection<string>)new[] { $"{x} {sufijo}" },
                StringComparer.Ordinal);
        return new SolicitudContenidoCatalogoTextos(mensajes, frases);
    }

    private static Campania CampaniaBilingue()
        => Campania.Crear(
            "c_bilingue",
            "Campania",
            "Descripcion",
            "Objetivo",
            EstadoCampania.Activa,
            null,
            null,
            "rub_1",
            null,
            "llm_1",
            ConfigMarkdown.Crear(TipoArtefactoMarkdown.Respuesta),
            ConfigConversacional.Crear(1, "Gracias."),
            LimitesSeguridad.Crear(1500, 10, 2),
            null,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch,
            idiomasHabilitados: ["es", "en"],
            localizaciones: new Dictionary<string, LocalizacionCampania>(StringComparer.Ordinal)
            {
                ["en"] = LocalizacionCampania.Crear(
                    "en",
                    "Campaign",
                    "Description",
                    "Objective",
                    "Thanks.",
                    mensajesIniciales: null,
                    preguntas: null),
            });

    /// <summary>Mapeo Meta completo en ingles para el alias dado.</summary>
    private static OpcionesPlantillaEnvioInicial PlantillasIngles(string plantillaRef)
        => new()
        {
            Mapeos =
            {
                [plantillaRef] = new Dictionary<string, PlantillaEnvioInicialConfigurada>(
                    StringComparer.OrdinalIgnoreCase)
                {
                    ["en"] = new() { Nombre = "el_tejido_inicio", Idioma = "en_US", Componentes = ["nombre"] },
                },
            },
        };

    /// <summary>Campania inglesa con un mensaje inicial activo que exige (o no) un alias Meta.</summary>
    private static Campania CampaniaConPlantilla(
        string? plantillaRef,
        string id = "c_plantilla",
        EstadoCampania estado = EstadoCampania.Activa)
        => Campania.Crear(
            id,
            "Campania",
            "Descripcion",
            "Objetivo",
            estado,
            [
                MensajeInicial.Crear(
                    "mi_1",
                    "saludo",
                    "Hola {{nombre}}.",
                    1,
                    ["nombre"],
                    EstadoRegistro.Activo,
                    PlantillaWhatsApp.Crear("legacy_saludo", "es", ["nombre"])),
            ],
            null,
            "rub_1",
            null,
            "llm_1",
            ConfigMarkdown.Crear(TipoArtefactoMarkdown.Respuesta),
            ConfigConversacional.Crear(1, "Gracias."),
            LimitesSeguridad.Crear(1500, 10, 2),
            null,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch,
            idiomasHabilitados: ["en"],
            localizaciones: new Dictionary<string, LocalizacionCampania>(StringComparer.Ordinal)
            {
                ["en"] = LocalizacionCampania.Crear(
                    "en",
                    "Campaign",
                    "Description",
                    "Goal",
                    "Thanks.",
                    new Dictionary<string, LocalizacionMensajeInicial>(StringComparer.Ordinal)
                    {
                        ["mi_1"] = new("Hi {{nombre}}.", plantillaRef),
                    },
                    null),
            });
}
