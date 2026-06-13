using ElTejido.Infrastructure.WhatsApp;
using FluentAssertions;

namespace ElTejido.UnitTests.WhatsApp;

public sealed class RepositorioWebhookDedupeCosmosTests
{
    [Fact]
    public async Task IntentarRegistrarMensajeAsync_WhenMessageIsNew_CreatesDedupeDocument()
    {
        var container = new FakeLeasesCosmosContainer { Created = true };
        var repository = new RepositorioWebhookDedupeCosmos(container);
        var processedAt = new DateTimeOffset(2026, 6, 11, 14, 5, 1, TimeSpan.FromHours(-5));

        var result = await repository.IntentarRegistrarMensajeAsync(
            " wamid.123 ",
            processedAt,
            CancellationToken.None);

        result.Should().BeTrue();
        container.Creates.Should().ContainSingle();
        var create = container.Creates.Single();
        create.PartitionKey.Should().Be("wamid.123");
        create.Document.Id.Should().Be("wamid.123");
        create.Document.Type.Should().Be(WebhookDedupeCosmosDocument.DocumentType);
        create.Document.Ttl.Should().Be(WebhookDedupeCosmosDocument.TimeToLiveSeconds);
        create.Document.ProcesadoEn.Should().Be(processedAt.ToUniversalTime());
    }

    [Fact]
    public async Task IntentarRegistrarMensajeAsync_WhenMessageAlreadyExists_ReturnsFalse()
    {
        var container = new FakeLeasesCosmosContainer { Created = false };
        var repository = new RepositorioWebhookDedupeCosmos(container);

        var result = await repository.IntentarRegistrarMensajeAsync(
            "wamid.repeated",
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        result.Should().BeFalse();
        container.Creates.Should().ContainSingle();
    }

    [Fact]
    public async Task IntentarRegistrarMensajeAsync_WhenMessageIdIsBlank_Throws()
    {
        var repository = new RepositorioWebhookDedupeCosmos(new FakeLeasesCosmosContainer());

        var action = async () => await repository.IntentarRegistrarMensajeAsync(
            " ",
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        await action.Should().ThrowAsync<ArgumentException>();
    }

    private sealed class FakeLeasesCosmosContainer : ILeasesCosmosContainer
    {
        public List<(WebhookDedupeCosmosDocument Document, string PartitionKey)> Creates { get; } = [];

        public bool Created { get; init; } = true;

        public Task<bool> TryCreateAsync(
            WebhookDedupeCosmosDocument document,
            string partitionKey,
            CancellationToken cancellationToken)
        {
            Creates.Add((document, partitionKey));
            return Task.FromResult(Created);
        }
    }
}
