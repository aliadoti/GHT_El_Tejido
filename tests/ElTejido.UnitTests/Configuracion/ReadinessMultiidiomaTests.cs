using ElTejido.Application.Campanas;
using ElTejido.Application.Configuracion;
using ElTejido.Application.Conversacion;
using ElTejido.Application.Evaluacion;
using ElTejido.Application.WhatsApp;
using ElTejido.Domain.Campanas;
using ElTejido.Domain.Localizacion;
using ElTejido.Infrastructure.Persistencia.Memoria;
using FluentAssertions;
using NSubstitute;

namespace ElTejido.UnitTests.Configuracion;

public sealed class ReadinessMultiidiomaTests
{
    [Fact]
    public async Task Obtener_UsaResolutoresRuntimeYNoDeclaraListaUnaCampaniaActivaRechazada()
    {
        var catalogos = new RepositorioCatalogosTextosMemoria();
        await catalogos.CrearAsync(CatalogosTextosSemilla.CrearVersionEmergencia("en").Catalogo, CancellationToken.None);
        var campanias = new RepositorioCampaniasMemoria();
        await campanias.GuardarCampaniaAsync(CampaniaInglesaIncompleta(), CancellationToken.None);
        var textos = Substitute.For<IResolutorTextosGlobales>();
        textos.ResolverAsync(
                IdiomaConversacion.Ingles,
                ModoResolucionTextosGlobales.Diagnostico,
                Arg.Any<CancellationToken>())
            .Returns(new ResultadoTextosGlobales.Disponible(IdiomaConversacion.Ingles, Textos: null));
        var contenido = Substitute.For<IResolutorContenidoCampania>();
        contenido.Resolver(Arg.Any<ContextoLocalizacion>()).Returns(
            new ResultadoContenidoCampania.NoDisponible(
                IdiomaConversacion.Ingles,
                [new ProblemaContenidoCampania(
                    ResolutorContenidoCampania.CodigoLocalizacionIncompleta,
                    "localizaciones.en")]));
        var politica = Substitute.For<IPoliticaIdiomaLlm>();
        politica.Resolver("en", TipoDirectivaIdiomaLlm.SalidaObligatoria).Returns(
            new ResultadoDirectivaIdiomaLlm.Disponible(
                IdiomaConversacion.Ingles,
                "IDIOMA_DE_SALIDA_OBLIGATORIO: en"));
        var servicio = new ServicioReadinessCatalogosTextos(
            catalogos,
            campanias,
            new OpcionesCatalogoTextos { Habilitado = true },
            new OpcionesConversacion(),
            new OpcionesPlantillaEnvioInicial(),
            textos,
            contenido,
            new ResolverPlantillaCanal(new OpcionesPlantillaEnvioInicial()),
            politica);

        var resultado = await servicio.ObtenerAsync("en", CancellationToken.None);

        var ingles = resultado.Idiomas.Should().ContainSingle().Subject;
        ingles.ActivaValida.Should().BeTrue();
        ingles.Listo.Should().BeFalse();
        ingles.CampaniasBloqueadas.Should().ContainSingle()
            .Which.Motivo.Should().Be("localizacion_campania_incompleta");
        await textos.Received(1).ResolverAsync(
            IdiomaConversacion.Ingles,
            ModoResolucionTextosGlobales.Diagnostico,
            Arg.Any<CancellationToken>());
        contenido.Received().Resolver(Arg.Is<ContextoLocalizacion>(contexto =>
            contexto.CatalogoTextosHabilitado && contexto.Idioma == IdiomaConversacion.Ingles));
        politica.Received(1).Resolver("en", TipoDirectivaIdiomaLlm.SalidaObligatoria);
    }

    private static Campania CampaniaInglesaIncompleta()
        => Campania.Crear(
            "c_1",
            "Campania",
            "Descripcion",
            "Objetivo",
            EstadoCampania.Activa,
            mensajesIniciales: null,
            preguntas: null,
            "rub_1",
            promptRefs: null,
            "llm_1",
            ConfigMarkdown.Crear(TipoArtefactoMarkdown.Respuesta),
            ConfigConversacional.Crear(1, "Gracias."),
            LimitesSeguridad.Crear(1500, 10, 2),
            usuariosHabilitados: null,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch,
            idiomasHabilitados: ["en"],
            localizaciones: null);
}
