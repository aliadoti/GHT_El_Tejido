using ElTejido.Application.WhatsApp;
using Microsoft.Azure.Cosmos;

namespace ElTejido.Infrastructure.WhatsApp;

/// <summary>
/// Adaptador Cosmos del contenedor leases para idempotencia de webhooks WhatsApp.
/// Cubre 03 secciones 3.16 y 4, y 05 seccion 2.4.
/// </summary>
public sealed class RepositorioWebhookDedupeCosmos : IRegistroWebhookDedupe
{
    private readonly ILeasesCosmosContainer _container;

    public RepositorioWebhookDedupeCosmos(Container container)
        : this(new LeasesCosmosContainer(container))
    {
    }

    internal RepositorioWebhookDedupeCosmos(ILeasesCosmosContainer container)
    {
        _container = container;
    }

    public async Task<bool> IntentarRegistrarMensajeAsync(
        string whatsappMessageId,
        DateTimeOffset procesadoEn,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(whatsappMessageId);

        var document = WebhookDedupeCosmosDocument.Create(
            whatsappMessageId.Trim(),
            procesadoEn.ToUniversalTime());

        return await _container.TryCreateAsync(document, document.Id, cancellationToken);
    }
}
