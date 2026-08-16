using ElTejido.Domain.Configuracion;
using ElTejido.Infrastructure.Configuracion;
using FluentAssertions;
using Newtonsoft.Json;

namespace ElTejido.UnitTests.Configuracion;

/// <summary>
/// DT-RUB-01 corte 1: los campos de estructura son <b>aditivos</b> (03 §3.11). Un documento nuevo
/// conserva id, descripcion y orden en el round-trip; uno historico sin esos campos se sigue leyendo
/// derivando el id del nombre y el orden de la posicion, <b>sin mutar el documento</b>.
/// </summary>
public sealed class RubricaCosmosMappingTests
{
    private static readonly DateTimeOffset Epoca = DateTimeOffset.UnixEpoch;

    [Fact]
    public void RoundTrip_DocumentoNuevo_ConservaEstructuraCompletaEIntegridad()
    {
        var original = Rubrica.Crear(
            "r_qa",
            "Rubrica QA",
            "Rubrica de prueba",
            new EscalaRubrica(1, 5),
            [
                CriterioRubrica.Crear("claridad", "Claridad", "Que tan concreta es.", 0.3m, 1),
                CriterioRubrica.Crear("viabilidad", "Viabilidad", "Que tan realizable es.", 0.5m, 2),
                CriterioRubrica.Crear("alcance", "Alcance", "A cuanta gente llega.", 0.2m, 3),
            ],
            2,
            EstadoRubrica.Activa,
            Epoca,
            Epoca,
            "Evalua con evidencia del aporte.");

        var documento = ConfigCosmosDocument.FromRubrica(original);
        var json = JsonConvert.SerializeObject(documento);
        var releida = JsonConvert.DeserializeObject<ConfigCosmosDocument>(json)!.ToRubrica();

        releida.Id.Should().Be("r_qa");
        releida.Version.Should().Be(2);
        releida.InstruccionesGenerales.Should().Be("Evalua con evidencia del aporte.");
        releida.Criterios.Select(c => c.Id).Should().Equal("claridad", "viabilidad", "alcance");
        releida.Criterios.Select(c => c.Orden).Should().Equal(1, 2, 3);
        releida.Criterios.Select(c => c.Descripcion).Should().NotContain(string.Empty);
        releida.Criterios.Select(c => c.Peso).Should().Equal(0.3m, 0.5m, 0.2m);
        releida.ContenidoMarkdown.Should().Be(original.ContenidoMarkdown);
        releida.HashEstructura.Should().Be(original.HashEstructura);
        releida.IntegridadEstructural.Should().Be(EstadoIntegridadRubrica.Valida);
    }

    [Fact]
    public void Lectura_DocumentoLegacySinIdNiOrden_DerivaLaClaveYConservaSuMarkdown()
    {
        // Forma exacta de un documento anterior a DT-RUB-01: criterios con nombre y peso solamente.
        const string JsonLegacy = """
            {
              "id": "r_general_v3",
              "type": "Rubrica",
              "pk": "Rubrica",
              "familiaId": "r_general",
              "nombre": "Rubrica general",
              "descripcion": "Evalua ideas",
              "contenidoMarkdown": "# Rubrica\nCinco ejes: claridad, impacto, viabilidad, novedad y alcance.",
              "escala": { "min": 1, "max": 5 },
              "criterios": [ { "nombre": "Impacto", "peso": 1.0 } ],
              "version": 3,
              "estado": "activa",
              "creadoEn": "1970-01-01T00:00:00Z",
              "actualizadoEn": "1970-01-01T00:00:00Z"
            }
            """;

        var rubrica = JsonConvert.DeserializeObject<ConfigCosmosDocument>(JsonLegacy)!.ToRubrica();

        rubrica.Id.Should().Be("r_general");
        rubrica.Criterios.Should().ContainSingle();
        rubrica.Criterios[0].Id.Should().Be("impacto");
        rubrica.Criterios[0].Nombre.Should().Be("Impacto");
        rubrica.Criterios[0].Orden.Should().Be(1);
        rubrica.InstruccionesGenerales.Should().BeEmpty();

        // El Markdown historico se conserva tal cual: la lectura no reescribe lo que ya recibia el LLM.
        rubrica.ContenidoMarkdown.Should().Contain("Cinco ejes");

        // Y la contradiccion queda marcada: se lee, pero no habilita una asignacion o activacion nueva.
        rubrica.IntegridadEstructural.Should().Be(EstadoIntegridadRubrica.LegacyNoVerificada);
        rubrica.HabilitadaParaAsignacionNueva.Should().BeFalse();
    }

    [Fact]
    public void Escritura_DocumentoNuevo_PersisteIdOrdenEIntegridad()
    {
        var rubrica = Rubrica.Crear(
            "r_qa",
            "Rubrica QA",
            "desc",
            new EscalaRubrica(1, 4),
            [CriterioRubrica.Crear("claridad", "Claridad", string.Empty, 1m, 1)],
            1,
            EstadoRubrica.Borrador,
            Epoca,
            Epoca);

        var json = JsonConvert.SerializeObject(ConfigCosmosDocument.FromRubrica(rubrica));

        json.Should().Contain("\"integridadEstructural\":\"valida\"");
        json.Should().Contain("\"hashEstructura\":\"sha256:");
        json.Should().Contain("\"id\":\"claridad\"");
        json.Should().Contain("\"orden\":1");
    }
}
