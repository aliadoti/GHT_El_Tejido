using ElTejido.Application.Conversacion;
using ElTejido.Domain.Conversaciones;
using Microsoft.Azure.Cosmos;

namespace ElTejido.Infrastructure.Conversaciones;

/// <summary>
/// Adaptador Cosmos P-26 del tipo <c>EnrutamientoAporte</c> (03 §3.6.1) sobre el contenedor existente
/// <c>conversations</c>, particion interna <c>routing:&lt;usuarioId&gt;</c>. Upsert con id determinista
/// (usuario + whatsappMessageId): un reintento reutiliza el mismo documento. Sin borrado: el
/// vencimiento es logico para conservar auditoria.
/// </summary>
public sealed class RepositorioEnrutamientosAporteCosmos : IRepositorioEnrutamientosAporte
{
    private readonly IConversationsCosmosContainer _container;

    public RepositorioEnrutamientosAporteCosmos(Container container)
        : this(new ConversationsCosmosContainer(container))
    {
    }

    internal RepositorioEnrutamientosAporteCosmos(IConversationsCosmosContainer container)
    {
        _container = container;
    }

    public Task GuardarAsync(EnrutamientoAporte enrutamiento, CancellationToken cancellationToken)
        => _container.UpsertEnrutamientoAsync(
            EnrutamientoAporteCosmosDocument.FromDomain(enrutamiento),
            enrutamiento.ParticionRouting,
            cancellationToken);

    public async Task<EnrutamientoAporte?> ObtenerPorMensajeAsync(
        string usuarioId,
        string whatsappMessageId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(usuarioId);
        ArgumentException.ThrowIfNullOrWhiteSpace(whatsappMessageId);

        var documento = await _container.ReadEnrutamientoAsync(
            EnrutamientoAporte.GenerarId(usuarioId.Trim(), whatsappMessageId.Trim()),
            EnrutamientoAporte.ParticionRoutingDe(usuarioId.Trim()),
            cancellationToken);
        return documento?.ToDomain();
    }

    public async Task<IReadOnlyCollection<EnrutamientoAporte>> ListarPorUsuarioAsync(
        string usuarioId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(usuarioId);

        var query = new QueryDefinition("SELECT * FROM c WHERE c.type = @type")
            .WithParameter("@type", EnrutamientoAporteCosmosDocument.DocumentType);

        var documentos = await _container.QueryAsync<EnrutamientoAporteCosmosDocument>(
            query,
            EnrutamientoAporte.ParticionRoutingDe(usuarioId.Trim()),
            cancellationToken);
        return documentos.Select(d => d.ToDomain()).ToArray();
    }
}
