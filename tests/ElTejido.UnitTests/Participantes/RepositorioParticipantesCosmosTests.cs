using ElTejido.Domain.Common;
using ElTejido.Domain.Identidad;
using ElTejido.Domain.Participantes;
using ElTejido.Infrastructure.Participantes;
using FluentAssertions;

namespace ElTejido.UnitTests.Participantes;

public sealed class RepositorioParticipantesCosmosTests
{
    [Fact]
    public async Task GuardarParticipanteAsync_UpsertsUsingCampaniaPartition()
    {
        var container = new FakeParticipantsCosmosContainer();
        var repository = new RepositorioParticipantesCosmos(container);

        await repository.GuardarParticipanteAsync(CrearParticipante(), CancellationToken.None);

        container.ParticipanteUpserts.Should().ContainSingle();
        var upsert = container.ParticipanteUpserts.Single();
        upsert.PartitionKey.Should().Be("c_2026conv");
        upsert.Document.Type.Should().Be(ParticipanteCampaniaCosmosDocument.DocumentType);
        upsert.Document.WhatsappNormalizado.Should().Be("573001112233");
        upsert.Document.EstadoEnvio.Should().Be("enviado");
        upsert.Document.EstadoRespuesta.Should().Be("respondio");
    }

    [Fact]
    public async Task ObtenerParticipantePorNumeroAsync_QueriesByCampaniaAndNumber()
    {
        var container = new FakeParticipantsCosmosContainer
        {
            ParticipanteQueryResult = [ParticipanteCampaniaCosmosDocument.FromDomain(CrearParticipante())],
        };
        var repository = new RepositorioParticipantesCosmos(container);

        var result = await repository.ObtenerParticipantePorNumeroAsync(
            "c_2026conv",
            NumeroWhatsApp.FromNormalized("573001112233"),
            CancellationToken.None);

        container.LastParticipanteQuery.Should().Be(("c_2026conv", (string?)null, "573001112233"));
        result.Should().NotBeNull();
        result!.UsuarioId.Should().Be("u_8f3c");
    }

    [Fact]
    public async Task BuscarParticipantesPorNumeroAsync_QueriesCrossPartition()
    {
        var container = new FakeParticipantsCosmosContainer
        {
            ParticipanteQueryResult = [ParticipanteCampaniaCosmosDocument.FromDomain(CrearParticipante())],
        };
        var repository = new RepositorioParticipantesCosmos(container);

        var result = await repository.BuscarParticipantesPorNumeroAsync(
            NumeroWhatsApp.FromNormalized("573001112233"),
            CancellationToken.None);

        container.LastParticipanteQuery.Should().Be(((string?)null, (string?)null, "573001112233"));
        result.Should().ContainSingle();
    }

    [Fact]
    public async Task RegistrarEnvioAsync_CreatesUsingCampaniaPartition()
    {
        var container = new FakeParticipantsCosmosContainer();
        var repository = new RepositorioParticipantesCosmos(container);

        await repository.RegistrarEnvioAsync(CrearEnvio(), CancellationToken.None);

        container.EnvioCreates.Should().ContainSingle();
        var create = container.EnvioCreates.Single();
        create.PartitionKey.Should().Be("c_2026conv");
        create.Document.Tipo.Should().Be("Inicial");
        create.Document.MensajeInicialId.Should().Be("mi_1");
    }

    [Fact]
    public async Task ExisteEnvioAsync_UsesIdempotencyKey()
    {
        var container = new FakeParticipantsCosmosContainer
        {
            EnvioQueryResult = [EnvioMensajeCosmosDocument.FromDomain(CrearEnvio())],
        };
        var repository = new RepositorioParticipantesCosmos(container);

        var exists = await repository.ExisteEnvioAsync(
            "c_2026conv",
            "u_8f3c",
            TipoEnvioMensaje.Inicial,
            "mi_1",
            CancellationToken.None);

        container.LastEnvioQuery.Should().Be(("c_2026conv", "u_8f3c", "Inicial", "mi_1"));
        exists.Should().BeTrue();
    }

    private static ParticipanteCampania CrearParticipante()
    {
        return ParticipanteCampania.Crear(
            "pc_1",
            "c_2026conv",
            "u_8f3c",
            NumeroWhatsApp.FromNormalized("573001112233"),
            EstadoRegistro.Activo,
            EstadoEnvio.Enviado,
            EstadoRespuestaParticipante.Respondio,
            new DateTimeOffset(2026, 6, 10, 12, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 11, 14, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 11, 14, 5, 0, TimeSpan.Zero));
    }

    private static EnvioMensaje CrearEnvio()
    {
        return EnvioMensaje.Crear(
            "env_1",
            "c_2026conv",
            "u_8f3c",
            "mi_1",
            NumeroWhatsApp.FromNormalized("573001112233"),
            EstadoEnvio.Enviado,
            TipoEnvioMensaje.Inicial,
            "wamid.abc",
            new DateTimeOffset(2026, 6, 11, 14, 0, 0, TimeSpan.Zero),
            null);
    }

    private sealed class FakeParticipantsCosmosContainer : IParticipantsCosmosContainer
    {
        public List<(ParticipanteCampaniaCosmosDocument Document, string PartitionKey)> ParticipanteUpserts { get; } = [];

        public List<(EnvioMensajeCosmosDocument Document, string PartitionKey)> EnvioCreates { get; } = [];

        public (string? CampaniaId, string? UsuarioId, string? Numero) LastParticipanteQuery { get; private set; }

        public (string CampaniaId, string? UsuarioId, string? Tipo, string? MensajeInicialId) LastEnvioQuery { get; private set; }

        public IReadOnlyCollection<ParticipanteCampaniaCosmosDocument> ParticipanteQueryResult { get; init; } = [];

        public IReadOnlyCollection<EnvioMensajeCosmosDocument> EnvioQueryResult { get; init; } = [];

        public Task UpsertParticipanteAsync(
            ParticipanteCampaniaCosmosDocument document,
            string partitionKey,
            CancellationToken cancellationToken)
        {
            ParticipanteUpserts.Add((document, partitionKey));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<ParticipanteCampaniaCosmosDocument>> QueryParticipantesAsync(
            string? campaniaId,
            string? usuarioId,
            string? whatsappNormalizado,
            CancellationToken cancellationToken)
        {
            LastParticipanteQuery = (campaniaId, usuarioId, whatsappNormalizado);
            return Task.FromResult(ParticipanteQueryResult);
        }

        public Task CreateEnvioAsync(
            EnvioMensajeCosmosDocument document,
            string partitionKey,
            CancellationToken cancellationToken)
        {
            EnvioCreates.Add((document, partitionKey));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<EnvioMensajeCosmosDocument>> QueryEnviosAsync(
            string campaniaId,
            string? usuarioId,
            string? tipo,
            string? mensajeInicialId,
            CancellationToken cancellationToken)
        {
            LastEnvioQuery = (campaniaId, usuarioId, tipo, mensajeInicialId);
            return Task.FromResult(EnvioQueryResult);
        }

        public List<(string Id, string PartitionKey)> Deletes { get; } = [];

        public Task DeleteAsync(string id, string partitionKey, CancellationToken cancellationToken)
        {
            Deletes.Add((id, partitionKey));
            return Task.CompletedTask;
        }
    }
}
