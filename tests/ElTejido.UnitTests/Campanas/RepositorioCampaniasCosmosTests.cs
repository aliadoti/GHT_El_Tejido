using ElTejido.Application.Campanas;
using ElTejido.Domain.Campanas;
using ElTejido.Domain.Common;
using ElTejido.Infrastructure.Campanas;
using FluentAssertions;

namespace ElTejido.UnitTests.Campanas;

public sealed class RepositorioCampaniasCosmosTests
{
    [Fact]
    public async Task GuardarCampaniaAsync_UpsertsCampaignUsingIdAsPartitionKey()
    {
        var container = new FakeCampaniasCosmosContainer();
        var repository = new RepositorioCampaniasCosmos(container);
        var campania = CrearCampania();

        await repository.GuardarCampaniaAsync(campania, CancellationToken.None);

        container.Upserts.Should().ContainSingle();
        var upsert = container.Upserts.Single();
        upsert.PartitionKey.Should().Be("c_2026conv");
        upsert.Document.Id.Should().Be("c_2026conv");
        upsert.Document.Type.Should().Be(CampaniaCosmosDocument.DocumentType);
        upsert.Document.Estado.Should().Be("activa");
        upsert.Document.ConfigLlmRef.Should().Be("llm_default");
        upsert.Document.MensajesIniciales.Should().ContainSingle();
        upsert.Document.Preguntas.Should().ContainSingle();
    }

    [Fact]
    public async Task ObtenerCampaniaPorIdAsync_MapsCosmosDocumentToDomain()
    {
        var container = new FakeCampaniasCosmosContainer
        {
            ReadResult = CampaniaCosmosDocument.FromDomain(CrearCampania()),
        };
        var repository = new RepositorioCampaniasCosmos(container);

        var result = await repository.ObtenerCampaniaPorIdAsync(" c_2026conv ", CancellationToken.None);

        container.LastReadId.Should().Be("c_2026conv");
        result.Should().NotBeNull();
        result!.Estado.Should().Be(EstadoCampania.Activa);
        result.MensajesIniciales.Should().ContainSingle().Which.PlantillaWhatsApp.Should().NotBeNull();
        result.Preguntas.Should().ContainSingle().Which.LimitesSeguridad.MaxLlamadasLlmPorUsuario.Should().Be(2);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConfigConversacional_TejidoColectivo_SobreviveElRoundTripCosmos(bool tejido)
    {
        // I-09 (aditivo, 03 §3.3): el flag por campaña debe conservarse al serializar/deserializar.
        var campania = CrearCampania(tejidoColectivo: tejido);

        var documento = CampaniaCosmosDocument.FromDomain(campania);
        var reconstruida = documento.ToDomain();

        documento.ConfigConversacional.TejidoColectivo.Should().Be(tejido);
        reconstruida.ConfigConversacional.TejidoColectivo.Should().Be(tejido);
    }

    [Fact]
    public void ConfigConversacional_TejidoColectivo_PorDefectoEsFalse()
    {
        // Documento viejo sin el campo = conversación autocontenida (comportamiento actual).
        var config = ConfigConversacional.Crear(1, "Gracias.");

        config.TejidoColectivo.Should().BeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConfigConversacional_Parafraseo_SobreviveRoundTripYDocumentoAnteriorQuedaApagado(bool parafraseo)
    {
        var campania = CrearCampania(parafraseo: parafraseo);

        var documento = CampaniaCosmosDocument.FromDomain(campania);
        var reconstruida = documento.ToDomain();

        documento.ConfigConversacional.Parafraseo.Should().Be(parafraseo);
        reconstruida.ConfigConversacional.Parafraseo.Should().Be(parafraseo);
        ConfigConversacional.Crear(1, "Gracias.").Parafraseo.Should().BeFalse();
    }

    [Fact]
    public async Task BuscarCampaniasAsync_UsesCosmosFilterAndMapsResults()
    {
        var container = new FakeCampaniasCosmosContainer
        {
            QueryResult = [CampaniaCosmosDocument.FromDomain(CrearCampania())],
        };
        var repository = new RepositorioCampaniasCosmos(container);

        var result = await repository.BuscarCampaniasAsync(
            new FiltroCampanias(EstadoCampania.Activa, " Convencion "),
            CancellationToken.None);

        container.LastFilter.Should().Be(new FiltroCampaniasCosmos("activa", "Convencion"));
        result.Should().ContainSingle().Which.Nombre.Should().Be("Convencion 2026 - Ideas");
    }

    private static Campania CrearCampania(bool tejidoColectivo = false, bool parafraseo = false)
    {
        return Campania.Crear(
            "c_2026conv",
            "Convencion 2026 - Ideas",
            "Captura de ideas",
            "Recolectar y evaluar ideas",
            EstadoCampania.Activa,
            [CrearMensaje()],
            [CrearPregunta()],
            "r_general",
            new Dictionary<string, string> { ["evaluar"] = "pr_eval", ["retro"] = "pr_retro" },
            "llm_default",
            ConfigMarkdown.Crear(TipoArtefactoMarkdown.Respuesta),
            ConfigConversacional.Crear(1, "Gracias. Tu aporte quedo registrado correctamente.", tejidoColectivo: tejidoColectivo, parafraseo: parafraseo),
            LimitesSeguridad.Crear(1500, 10, 2),
            ["u_1", "u_2"],
            new DateTimeOffset(2026, 6, 10, 12, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 11, 9, 0, 0, TimeSpan.Zero));
    }

    private static MensajeInicial CrearMensaje()
    {
        return MensajeInicial.Crear(
            "mi_1",
            "saludo",
            "Hola {{nombre}}.",
            1,
            ["nombre", "campania"],
            EstadoRegistro.Activo,
            PlantillaWhatsApp.Crear("el_tejido_saludo", "es", ["nombre", "campania"]));
    }

    private static Pregunta CrearPregunta()
    {
        return Pregunta.Crear(
            "p_ingresos",
            "Escribe una idea para mejorar los ingresos.",
            "Se concreto.",
            "ingresos",
            1,
            EstadoRegistro.Activo,
            "r_general",
            3,
            new Dictionary<string, string> { ["evaluar"] = "pr_eval" },
            1,
            LimitesSeguridad.ParaPregunta(1500, 2),
            ConfigMarkdown.Crear(TipoArtefactoMarkdown.Respuesta));
    }

    private sealed class FakeCampaniasCosmosContainer : ICampaniasCosmosContainer
    {
        public List<(CampaniaCosmosDocument Document, string PartitionKey)> Upserts { get; } = [];

        public string? LastReadId { get; private set; }

        public FiltroCampaniasCosmos? LastFilter { get; private set; }

        public CampaniaCosmosDocument? ReadResult { get; init; }

        public IReadOnlyCollection<CampaniaCosmosDocument> QueryResult { get; init; } = [];

        public Task UpsertAsync(
            CampaniaCosmosDocument document,
            string partitionKey,
            CancellationToken cancellationToken)
        {
            Upserts.Add((document, partitionKey));
            return Task.CompletedTask;
        }

        public Task<CampaniaCosmosDocument?> ReadByIdAsync(string id, CancellationToken cancellationToken)
        {
            LastReadId = id;
            return Task.FromResult(ReadResult);
        }

        public Task<IReadOnlyCollection<CampaniaCosmosDocument>> QueryAsync(
            FiltroCampaniasCosmos filtro,
            CancellationToken cancellationToken)
        {
            LastFilter = filtro;
            return Task.FromResult(QueryResult);
        }
    }
}
