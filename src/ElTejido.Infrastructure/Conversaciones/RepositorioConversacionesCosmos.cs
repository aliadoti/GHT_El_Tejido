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

    public async Task<IReadOnlyCollection<Conversacion>> ListarConversacionesAsync(
        string campaniaId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaniaId);

        var query = new QueryDefinition("SELECT * FROM c WHERE c.type = @type")
            .WithParameter("@type", ConversacionCosmosDocument.DocumentType);

        var documentos = await _container.QueryAsync<ConversacionCosmosDocument>(query, campaniaId.Trim(), cancellationToken);
        return documentos.Select(d => d.ToDomain()).ToArray();
    }

    public async Task<IReadOnlyCollection<Conversacion>> ListarAbiertasInactivasAsync(
        DateTimeOffset limite,
        CancellationToken cancellationToken)
    {
        // _ts es la marca de tiempo (epoch segundos) de la ultima escritura del documento en Cosmos;
        // la conversacion se reescribe en cada turno, por lo que refleja su ultima actividad.
        var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.type = @type AND c.estado = @estado AND c._ts < @limite")
            .WithParameter("@type", ConversacionCosmosDocument.DocumentType)
            .WithParameter("@estado", "abierta")
            .WithParameter("@limite", limite.ToUnixTimeSeconds());

        var documentos = await _container.QueryCrossPartitionAsync<ConversacionCosmosDocument>(query, cancellationToken);
        return documentos.Select(d => d.ToDomain()).ToArray();
    }

    public async Task<IReadOnlyCollection<Mensaje>> ListarMensajesAsync(
        string campaniaId,
        string conversacionId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaniaId);
        ArgumentException.ThrowIfNullOrWhiteSpace(conversacionId);

        var query = new QueryDefinition("SELECT * FROM c WHERE c.type = @type AND c.conversacionId = @conversacionId")
            .WithParameter("@type", MensajeCosmosDocument.DocumentType)
            .WithParameter("@conversacionId", conversacionId.Trim());

        var documentos = await _container.QueryAsync<MensajeCosmosDocument>(query, campaniaId.Trim(), cancellationToken);
        return documentos
            .Select(d => d.ToDomain())
            .OrderBy(m => m.Timestamp)
            .ToArray();
    }

    public Task GuardarMensajeAsync(Mensaje mensaje, CancellationToken cancellationToken)
        => _container.CreateMensajeAsync(
            MensajeCosmosDocument.FromDomain(mensaje),
            mensaje.CampaniaId,
            cancellationToken);

    public async Task<ConteoBorradoConversaciones> EliminarPorUsuarioAsync(
        string campaniaId,
        string? usuarioId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaniaId);
        var pk = campaniaId.Trim();
        var usuario = string.IsNullOrWhiteSpace(usuarioId) ? null : usuarioId.Trim();

        var queryConv = usuario is null
            ? new QueryDefinition("SELECT * FROM c WHERE c.type = @type")
                .WithParameter("@type", ConversacionCosmosDocument.DocumentType)
            : new QueryDefinition("SELECT * FROM c WHERE c.type = @type AND c.usuarioId = @usuarioId")
                .WithParameter("@type", ConversacionCosmosDocument.DocumentType)
                .WithParameter("@usuarioId", usuario);
        var conversaciones = await _container.QueryAsync<ConversacionCosmosDocument>(queryConv, pk, cancellationToken);
        var idsConversaciones = conversaciones.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);

        // Mensaje no lleva usuarioId (pertenece a una conversacion): para toda la campania se borran
        // todos; para un usuario, solo los de sus conversaciones.
        var mensajes = await _container.QueryAsync<MensajeCosmosDocument>(
            new QueryDefinition("SELECT * FROM c WHERE c.type = @type").WithParameter("@type", MensajeCosmosDocument.DocumentType),
            pk,
            cancellationToken);
        IReadOnlyCollection<MensajeCosmosDocument> mensajesEnAlcance = usuario is null
            ? mensajes
            : mensajes.Where(m => idsConversaciones.Contains(m.ConversacionId)).ToArray();

        foreach (var mensaje in mensajesEnAlcance)
        {
            await _container.DeleteAsync(mensaje.Id, pk, cancellationToken);
        }

        foreach (var conversacion in conversaciones)
        {
            await _container.DeleteAsync(conversacion.Id, pk, cancellationToken);
        }

        return new ConteoBorradoConversaciones(conversaciones.Count, mensajesEnAlcance.Count);
    }
}
