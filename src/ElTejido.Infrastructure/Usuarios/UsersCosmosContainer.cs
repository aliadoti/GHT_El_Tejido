using System.Net;
using Microsoft.Azure.Cosmos;

namespace ElTejido.Infrastructure.Usuarios;

internal sealed class UsersCosmosContainer : IUsersCosmosContainer
{
    private readonly Container _container;

    public UsersCosmosContainer(Container container)
    {
        _container = container;
    }

    public async Task UpsertUsuarioAsync(
        UsuarioCosmosDocument document,
        string partitionKey,
        CancellationToken cancellationToken)
    {
        await _container.UpsertItemAsync(
            document,
            new PartitionKey(partitionKey),
            cancellationToken: cancellationToken);
    }

    public async Task<UsuarioCosmosDocument?> ReadUsuarioByIdAsync(
        string id,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _container.ReadItemAsync<UsuarioCosmosDocument>(
                id,
                new PartitionKey(UsuarioCosmosDocument.PartitionKeyValue),
                cancellationToken: cancellationToken);

            return response.Resource;
        }
        catch (CosmosException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<IReadOnlyCollection<UsuarioCosmosDocument>> QueryUsuariosAsync(
        FiltroUsuariosCosmos filtro,
        CancellationToken cancellationToken)
    {
        using var iterator = _container.GetItemQueryIterator<UsuarioCosmosDocument>(
            CreateUsuariosQueryDefinition(filtro));

        var documents = new List<UsuarioCosmosDocument>();
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            documents.AddRange(page);
        }

        return documents;
    }

    public async Task UpsertTagAsync(
        TagCosmosDocument document,
        string partitionKey,
        CancellationToken cancellationToken)
    {
        await _container.UpsertItemAsync(
            document,
            new PartitionKey(partitionKey),
            cancellationToken: cancellationToken);
    }

    public async Task<TagCosmosDocument?> ReadTagByIdAsync(
        string id,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _container.ReadItemAsync<TagCosmosDocument>(
                id,
                new PartitionKey(TagCosmosDocument.PartitionKeyValue),
                cancellationToken: cancellationToken);

            return response.Resource;
        }
        catch (CosmosException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<IReadOnlyCollection<TagCosmosDocument>> QueryTagsAsync(
        FiltroTagsCosmos filtro,
        CancellationToken cancellationToken)
    {
        using var iterator = _container.GetItemQueryIterator<TagCosmosDocument>(
            CreateTagsQueryDefinition(filtro));

        var documents = new List<TagCosmosDocument>();
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            documents.AddRange(page);
        }

        return documents;
    }

    private static QueryDefinition CreateUsuariosQueryDefinition(FiltroUsuariosCosmos filtro)
    {
        var filters = new List<string>
        {
            "c.type = @type",
            "c.pk = @pk",
        };

        if (filtro.WhatsappNormalizado is not null)
        {
            filters.Add("c.whatsappNormalizado = @whatsappNormalizado");
        }

        if (filtro.Rol is not null)
        {
            filters.Add("c.rol = @rol");
        }

        if (filtro.Estado is not null)
        {
            filters.Add("c.estado = @estado");
        }

        if (filtro.Area is not null)
        {
            filters.Add("c.area = @area");
        }

        if (filtro.Empresa is not null)
        {
            filters.Add("c.empresa = @empresa");
        }

        for (var index = 0; index < filtro.Tags.Count; index++)
        {
            filters.Add($"ARRAY_CONTAINS(c.tags, @tag{index})");
        }

        if (filtro.Busqueda is not null)
        {
            filters.Add("(CONTAINS(LOWER(c.nombre), @busqueda) OR CONTAINS(c.whatsappNormalizado, @busqueda))");
        }

        var query = new QueryDefinition($"SELECT * FROM c WHERE {string.Join(" AND ", filters)}")
            .WithParameter("@type", UsuarioCosmosDocument.DocumentType)
            .WithParameter("@pk", UsuarioCosmosDocument.PartitionKeyValue);

        if (filtro.WhatsappNormalizado is not null)
        {
            query.WithParameter("@whatsappNormalizado", filtro.WhatsappNormalizado);
        }

        if (filtro.Rol is not null)
        {
            query.WithParameter("@rol", filtro.Rol);
        }

        if (filtro.Estado is not null)
        {
            query.WithParameter("@estado", filtro.Estado);
        }

        if (filtro.Area is not null)
        {
            query.WithParameter("@area", filtro.Area);
        }

        if (filtro.Empresa is not null)
        {
            query.WithParameter("@empresa", filtro.Empresa);
        }

        var tagIndex = 0;
        foreach (var tag in filtro.Tags)
        {
            query.WithParameter($"@tag{tagIndex}", tag);
            tagIndex++;
        }

        if (filtro.Busqueda is not null)
        {
            query.WithParameter("@busqueda", filtro.Busqueda.ToLowerInvariant());
        }

        return query;
    }

    private static QueryDefinition CreateTagsQueryDefinition(FiltroTagsCosmos filtro)
    {
        var filters = new List<string>
        {
            "c.type = @type",
            "c.pk = @pk",
        };

        if (filtro.TipoTag is not null)
        {
            filters.Add("c.tipoTag = @tipoTag");
        }

        if (filtro.Estado is not null)
        {
            filters.Add("c.estado = @estado");
        }

        var query = new QueryDefinition($"SELECT * FROM c WHERE {string.Join(" AND ", filters)}")
            .WithParameter("@type", TagCosmosDocument.DocumentType)
            .WithParameter("@pk", TagCosmosDocument.PartitionKeyValue);

        if (filtro.TipoTag is not null)
        {
            query.WithParameter("@tipoTag", filtro.TipoTag);
        }

        if (filtro.Estado is not null)
        {
            query.WithParameter("@estado", filtro.Estado);
        }

        return query;
    }
}
