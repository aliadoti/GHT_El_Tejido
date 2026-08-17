using ElTejido.Application.Campanas;
using ElTejido.Domain.Campanas;
using ElTejido.Domain.Common;
using ElTejido.Domain.Localizacion;
using FluentAssertions;

namespace ElTejido.UnitTests.Campanas;

public sealed class ResolutorContenidoCampaniaTests
{
    private readonly ResolutorContenidoCampania _resolutor = new();

    [Fact]
    public void GateApagado_ConservaTodoElContenidoLegacyAunqueElHiloSeaIngles()
    {
        var resultado = _resolutor.Resolver(new ContextoLocalizacion(
            CrearCampania(LocalizacionInglesCompleta()),
            IdiomaConversacion.Ingles,
            CatalogoTextosHabilitado: false));

        var contenido = resultado.Should().BeOfType<ResultadoContenidoCampania.Disponible>()
            .Which.Contenido;
        contenido.Idioma.Should().Be(IdiomaConversacion.Espanol);
        contenido.Origen.Should().Be(OrigenContenidoCampania.Legacy);
        contenido.Nombre.Should().Be("Campania legacy");
        contenido.MensajeCierre.Should().Be("Cierre legacy");
        contenido.MensajesIniciales["mi_1"].Should().BeEquivalentTo(
            new ContenidoMensajeInicialEfectivo("Saludo legacy", null));
        contenido.Preguntas["p_1"].Should().BeEquivalentTo(
            new ContenidoPreguntaEfectiva("Pregunta legacy", "Instruccion legacy"));
    }

    [Fact]
    public void GateEncendido_LocalizacionCompletaDevuelveUnaUnicaFuenteEditorial()
    {
        var resultado = _resolutor.Resolver(new ContextoLocalizacion(
            CrearCampania(LocalizacionInglesCompleta()),
            IdiomaConversacion.Ingles,
            CatalogoTextosHabilitado: true)
        {
            PreguntaId = "p_1",
            MensajeInicialId = "mi_1",
            CorrelationId = "corr-1",
        });

        var contenido = resultado.Should().BeOfType<ResultadoContenidoCampania.Disponible>()
            .Which.Contenido;
        contenido.Idioma.Should().Be(IdiomaConversacion.Ingles);
        contenido.Origen.Should().Be(OrigenContenidoCampania.Localizacion);
        contenido.Nombre.Should().Be("English campaign");
        contenido.Descripcion.Should().Be("English description");
        contenido.Objetivo.Should().Be("English objective");
        contenido.MensajeCierre.Should().Be("English close");
        contenido.MensajesIniciales["mi_1"].Should().BeEquivalentTo(
            new ContenidoMensajeInicialEfectivo("English greeting", "welcome_en"));
        contenido.Preguntas["p_1"].Should().BeEquivalentTo(
            new ContenidoPreguntaEfectiva("English question", "English instruction"));
    }

    [Fact]
    public void GateEncendido_LocalizacionIncompletaNoEntregaContenidoParcial()
    {
        var incompleta = LocalizacionCampania.Crear(
            "en", "English campaign", null, "English objective", "English close",
            new Dictionary<string, LocalizacionMensajeInicial>(StringComparer.Ordinal)
            {
                ["mi_1"] = new("English greeting", null),
            },
            new Dictionary<string, LocalizacionPregunta>(StringComparer.Ordinal)
            {
                ["p_1"] = new("English question", null),
            });

        var resultado = _resolutor.Resolver(new ContextoLocalizacion(
            CrearCampania(incompleta),
            IdiomaConversacion.Ingles,
            CatalogoTextosHabilitado: true));

        var noDisponible = resultado.Should().BeOfType<ResultadoContenidoCampania.NoDisponible>().Subject;
        noDisponible.Problemas.Should().OnlyContain(
            problema => problema.Codigo == ResolutorContenidoCampania.CodigoLocalizacionIncompleta);
        noDisponible.Problemas.Select(problema => problema.Ruta).Should().BeEquivalentTo(
            "localizaciones.en.descripcion",
            "localizaciones.en.mensajesIniciales.mi_1.plantillaRef",
            "localizaciones.en.preguntas.p_1.instruccion");
    }

    [Fact]
    public void GateEncendido_IdiomaNoHabilitadoFallaTipificado()
    {
        var resultado = _resolutor.Resolver(new ContextoLocalizacion(
            CrearCampania(localizacionIngles: null, bilingue: false),
            IdiomaConversacion.Ingles,
            CatalogoTextosHabilitado: true));

        var noDisponible = resultado.Should().BeOfType<ResultadoContenidoCampania.NoDisponible>().Subject;
        noDisponible.CodigoPrincipal.Should().Be(ResolutorContenidoCampania.CodigoIdiomaNoHabilitado);
        noDisponible.Problemas.Should().ContainSingle()
            .Which.Ruta.Should().Be("localizaciones.en");
    }

    private static LocalizacionCampania LocalizacionInglesCompleta()
        => LocalizacionCampania.Crear(
            "en", "English campaign", "English description", "English objective", "English close",
            new Dictionary<string, LocalizacionMensajeInicial>(StringComparer.Ordinal)
            {
                ["mi_1"] = new("English greeting", "welcome_en"),
            },
            new Dictionary<string, LocalizacionPregunta>(StringComparer.Ordinal)
            {
                ["p_1"] = new("English question", "English instruction"),
            });

    private static Campania CrearCampania(LocalizacionCampania? localizacionIngles, bool bilingue = true)
    {
        var mensaje = MensajeInicial.Crear(
            "mi_1", "saludo", "Saludo legacy", 1, null, EstadoRegistro.Activo,
            PlantillaWhatsApp.Crear("welcome_es", "es", null));
        var pregunta = Pregunta.Crear(
            "p_1", "Pregunta legacy", "Instruccion legacy", "categoria", 1, EstadoRegistro.Activo,
            rubricaRef: null, versionRubrica: null, promptRefs: null, 1,
            LimitesSeguridad.ParaPregunta(1500, 2),
            ConfigMarkdown.Crear(TipoArtefactoMarkdown.Respuesta));
        var localizaciones = localizacionIngles is null
            ? null
            : new Dictionary<string, LocalizacionCampania>(StringComparer.Ordinal)
            {
                ["en"] = localizacionIngles,
            };

        return Campania.Crear(
            "c_1", "Campania legacy", "Descripcion legacy", "Objetivo legacy", EstadoCampania.Activa,
            new[] { mensaje }, new[] { pregunta }, "rub_1", null, "llm_1",
            ConfigMarkdown.Crear(TipoArtefactoMarkdown.Respuesta),
            ConfigConversacional.Crear(1, "Cierre legacy"),
            LimitesSeguridad.Crear(1500, 10, 2), null,
            DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch,
            bilingue ? new[] { "es", "en" } : null,
            localizaciones);
    }
}
