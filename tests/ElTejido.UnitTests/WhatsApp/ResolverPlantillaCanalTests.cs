using ElTejido.Application.WhatsApp;
using ElTejido.Domain.Campanas;
using ElTejido.Domain.Localizacion;
using FluentAssertions;

namespace ElTejido.UnitTests.WhatsApp;

public sealed class ResolverPlantillaCanalTests
{
    [Fact]
    public void Resolver_ConvierteAliasEIdiomaInternoEnPlantillaMeta()
    {
        var resolutor = new ResolverPlantillaCanal(Opciones(
            new PlantillaEnvioInicialConfigurada
            {
                Nombre = "welcome_en",
                Idioma = "en_US",
                Componentes = ["nombre", "campania"],
            }));

        var resultado = resolutor.Resolver("inicio", IdiomaConversacion.Ingles);

        var disponible = resultado.Should().BeOfType<ResultadoPlantillaCanal.Disponible>().Subject;
        disponible.Plantilla.Should().BeEquivalentTo(new
        {
            Nombre = "welcome_en",
            Idioma = "en_US",
            Componentes = new[] { "nombre", "campania" },
        });
    }

    [Fact]
    public void Resolver_MapeoEstructuralmenteInvalidoNoEstaDisponibleParaRuntimeNiReadiness()
    {
        var resolutor = new ResolverPlantillaCanal(Opciones(
            new PlantillaEnvioInicialConfigurada
            {
                Nombre = "welcome_en",
                Idioma = "en_US",
                Componentes = ["nombre", " ", "nombre"],
            }));

        var resultado = resolutor.Resolver("inicio", IdiomaConversacion.Ingles);

        var noDisponible = resultado.Should().BeOfType<ResultadoPlantillaCanal.NoDisponible>().Subject;
        noDisponible.Problemas.Should().BeEquivalentTo(
            ProblemasPlantillaCanal.ComponenteVacio,
            ProblemasPlantillaCanal.ComponenteDuplicado);
    }

    [Fact]
    public void ResolverLegacy_ConservaComponentesDelMensajeCuandoLaConfiguracionNoLosReemplaza()
    {
        var opciones = new OpcionesPlantillaEnvioInicial
        {
            Nombre = "legacy_meta",
            Idioma = string.Empty,
        };
        var respaldo = PlantillaWhatsApp.Crear("ignorado", "es_CO", ["nombre"]);

        var resultado = new ResolverPlantillaCanal(opciones).ResolverLegacy(respaldo);

        resultado.Should().BeOfType<ResultadoPlantillaCanal.Disponible>()
            .Which.Plantilla.Should().BeEquivalentTo(new
            {
                Nombre = "legacy_meta",
                Idioma = "es_CO",
                Componentes = new[] { "nombre" },
            });
    }

    private static OpcionesPlantillaEnvioInicial Opciones(PlantillaEnvioInicialConfigurada configurada)
        => new()
        {
            Mapeos = new Dictionary<string, Dictionary<string, PlantillaEnvioInicialConfigurada>>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["inicio"] = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["en"] = configurada,
                },
            },
        };
}
