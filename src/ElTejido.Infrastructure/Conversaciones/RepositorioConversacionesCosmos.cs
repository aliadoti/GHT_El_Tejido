using ElTejido.Application.Conversacion;
using ElTejido.Domain.Conversaciones;
using Microsoft.Azure.Cosmos;

namespace ElTejido.Infrastructure.Conversaciones;

/// <summary>
/// Adaptador Cosmos del contenedor <c>conversations</c> (pk <c>campaniaId</c>) para Conversacion y
/// Mensaje (03 §3.6-§3.7). Conversacion por upsert (transiciones de estado); Mensaje append-only.
/// </summary>
public sealed class RepositorioConversacionesCosmos : IRepositorioConversaciones
{
    private readonly IConversationsCosmosContainer _container;

    public RepositorioConversacionesCosmos(Container container)
        : this(new ConversationsCosmosContainer(container))
    {
    }

    internal RepositorioConversacionesCosmos(IConversationsCosmosContainer container)
    {
        _container = container;
    }

    public Task GuardarConversacionAsync(Conversacion conversacion, CancellationToken cancellationToken)
        => _container.UpsertConversacionAsync(
            ConversacionCosmosDocument.FromDomain(conversacion),
            conversacion.CampaniaId,
            cancellationToken);

    public async Task<Conversacion?> ObtenerConversacionAsync(
        string campaniaId,
        string conversacionId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaniaId);
        ArgumentException.ThrowIfNullOrWhiteSpace(conversacionId);

        var documento = await _container.ReadConversacionAsync(conversacionId.Trim(), campaniaId.Trim(), cancellationToken);
        return documento?.ToDomain();
    }

    public Task GuardarMensajeAsync(Mensaje mensaje, CancellationToken cancellationToken)
        => _container.CreateMensajeAsync(
            MensajeCosmosDocument.FromDomain(mensaje),
            mensaje.CampaniaId,
            cancellationToken);
}
