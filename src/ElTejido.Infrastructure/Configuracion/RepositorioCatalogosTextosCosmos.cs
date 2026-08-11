using System.Net;
using ElTejido.Application.Common;
using ElTejido.Application.Configuracion;
using ElTejido.Domain.Configuracion;
using Microsoft.Azure.Cosmos;

namespace ElTejido.Infrastructure.Configuracion;

public sealed class RepositorioCatalogosTextosCosmos : IRepositorioCatalogosTextos
{
    private readonly Container _container;
    private static readonly PartitionKey PartitionKey = new(CatalogoTextosCosmosDocument.PartitionKeyValue);

    public RepositorioCatalogosTextosCosmos(Container container)
    {
        _container = container;
    }

    public async Task<IReadOnlyCollection<VersionCatalogoTextos>> BuscarAsync(
        string? idioma,
        EstadoCatalogoTextos? estado,
        CancellationToken cancellationToken)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.type = @type" +
            (idioma is null ? string.Empty : " AND c.idioma = @idioma") +
            (estado is null ? string.Empty : " AND c.estado = @estado"))
            .WithParameter("@type", CatalogoTextosCosmosDocument.DocumentType);
        if (idioma is not null)
        {
            query.WithParameter("@idioma", idioma);
        }

        if (estado is not null)
        {
            query.WithParameter("@estado", CatalogoTextosCosmosDocument.ToCosmosEstado(estado.Value));
        }

        return (await ConsultarAsync(query, cancellationToken))
            .Select(Mapear)
            .OrderBy(x => x.Catalogo.FamiliaId, StringComparer.Ordinal)
            .ThenBy(x => x.Catalogo.Idioma, StringComparer.Ordinal)
            .ThenByDescending(x => x.Catalogo.Version)
            .ToArray();
    }

    public async Task<IReadOnlyCollection<VersionCatalogoTextos>> ListarVersionesAsync(
        string familiaId,
        string idioma,
        CancellationToken cancellationToken)
    {
        var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.type = @type AND c.familiaId = @familiaId AND c.idioma = @idioma")
            .WithParameter("@type", CatalogoTextosCosmosDocument.DocumentType)
            .WithParameter("@familiaId", familiaId)
            .WithParameter("@idioma", idioma);
        return (await ConsultarAsync(query, cancellationToken))
            .OrderByDescending(x => x.Version)
            .Select(Mapear)
            .ToArray();
    }

    public async Task<VersionCatalogoTextos?> ObtenerAsync(
        string familiaId,
        string idioma,
        int version,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _container.ReadItemAsync<CatalogoTextosCosmosDocument>(
                CatalogoTextosCosmosDocument.CrearId(familiaId, idioma, version),
                PartitionKey,
                cancellationToken: cancellationToken);
            return new VersionCatalogoTextos(response.Resource.ToDomain(), response.ETag);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<VersionCatalogoTextos?> ObtenerActivoAsync(string idioma, CancellationToken cancellationToken)
    {
        var puntero = await ObtenerPunteroActivoAsync(idioma, cancellationToken);
        if (puntero is not null)
        {
            return await ObtenerAsync(puntero.FamiliaId, idioma, puntero.Version, cancellationToken)
                ?? throw new InvalidOperationException("El puntero del catalogo activo referencia una version inexistente.");
        }

        // Compatibilidad de migracion con documentos creados antes del puntero singleton.
        var activos = await BuscarAsync(idioma, EstadoCatalogoTextos.Activo, cancellationToken);
        if (activos.Count > 1)
        {
            throw new InvalidOperationException($"Hay mas de un catalogo activo para el idioma {idioma}.");
        }

        return activos.SingleOrDefault();
    }

    public async Task<VersionCatalogoTextos> CrearAsync(
        CatalogoTextosConversacion catalogo,
        CancellationToken cancellationToken)
    {
        try
        {
            var document = CatalogoTextosCosmosDocument.FromDomain(catalogo);
            var response = await _container.CreateItemAsync(document, PartitionKey, cancellationToken: cancellationToken);
            return new VersionCatalogoTextos(response.Resource.ToDomain(), response.ETag);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            throw new ErrorConflicto("La version del catalogo ya existe.");
        }
    }

    public async Task<VersionCatalogoTextos> ReemplazarBorradorAsync(
        CatalogoTextosConversacion catalogo,
        string etag,
        CancellationToken cancellationToken)
    {
        try
        {
            var document = CatalogoTextosCosmosDocument.FromDomain(catalogo);
            var response = await _container.ReplaceItemAsync(
                document,
                document.Id,
                PartitionKey,
                new ItemRequestOptions { IfMatchEtag = etag },
                cancellationToken);
            return new VersionCatalogoTextos(response.Resource.ToDomain(), response.ETag);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
        {
            throw ConflictoEtag();
        }
    }

    public async Task<VersionCatalogoTextos> ActivarAsync(
        CatalogoTextosConversacion catalogoActivo,
        string etag,
        CancellationToken cancellationToken)
    {
        var punteroActual = await ObtenerPunteroActivoAsync(catalogoActivo.Idioma, cancellationToken);
        IReadOnlyCollection<VersionCatalogoTextos> activos;
        if (punteroActual is null)
        {
            // Cubre la primera activacion y la migracion de datos previos. Crear el puntero dentro del
            // batch actua como exclusion optimista entre activaciones concurrentes.
            activos = await BuscarAsync(catalogoActivo.Idioma, EstadoCatalogoTextos.Activo, cancellationToken);
        }
        else
        {
            var actual = await ObtenerAsync(
                    punteroActual.FamiliaId,
                    catalogoActivo.Idioma,
                    punteroActual.Version,
                    cancellationToken)
                ?? throw new InvalidOperationException("El puntero del catalogo activo referencia una version inexistente.");
            activos = new[] { actual };
        }

        var batch = _container.CreateTransactionalBatch(PartitionKey);
        foreach (var actual in activos.Where(x =>
                     x.Catalogo.FamiliaId != catalogoActivo.FamiliaId
                     || x.Catalogo.Version != catalogoActivo.Version))
        {
            var inactivo = actual.Catalogo.CambiarEstado(
                EstadoCatalogoTextos.Inactivo,
                catalogoActivo.ActualizadoEn);
            var document = CatalogoTextosCosmosDocument.FromDomain(inactivo);
            batch.ReplaceItem(
                document.Id,
                document,
                new TransactionalBatchItemRequestOptions { IfMatchEtag = actual.Etag });
        }

        var candidato = CatalogoTextosCosmosDocument.FromDomain(catalogoActivo);
        batch.ReplaceItem(
            candidato.Id,
            candidato,
            new TransactionalBatchItemRequestOptions { IfMatchEtag = etag });
        var nuevoPuntero = CatalogoTextosActivoCosmosDocument.Crear(catalogoActivo);
        if (punteroActual is null)
        {
            batch.CreateItem(nuevoPuntero);
        }
        else
        {
            batch.ReplaceItem(
                nuevoPuntero.Id,
                nuevoPuntero,
                new TransactionalBatchItemRequestOptions { IfMatchEtag = punteroActual.Etag });
        }

        var response = await batch.ExecuteAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode is HttpStatusCode.PreconditionFailed
                or HttpStatusCode.Conflict
                or HttpStatusCode.FailedDependency)
            {
                throw ConflictoEtag();
            }

            throw new InvalidOperationException(
                $"No fue posible activar el catalogo en Cosmos. Estado: {(int)response.StatusCode}.");
        }

        return await ObtenerAsync(
                catalogoActivo.FamiliaId,
                catalogoActivo.Idioma,
                catalogoActivo.Version,
                cancellationToken)
            ?? throw new InvalidOperationException("Cosmos no devolvio el catalogo recien activado.");
    }

    private async Task<IReadOnlyCollection<CatalogoTextosCosmosDocument>> ConsultarAsync(
        QueryDefinition query,
        CancellationToken cancellationToken)
    {
        using var iterator = _container.GetItemQueryIterator<CatalogoTextosCosmosDocument>(
            query,
            requestOptions: new QueryRequestOptions { PartitionKey = PartitionKey });
        var resultado = new List<CatalogoTextosCosmosDocument>();
        while (iterator.HasMoreResults)
        {
            var pagina = await iterator.ReadNextAsync(cancellationToken);
            resultado.AddRange(pagina);
        }

        return resultado;
    }

    private async Task<CatalogoTextosActivoCosmosDocument?> ObtenerPunteroActivoAsync(
        string idioma,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _container.ReadItemAsync<CatalogoTextosActivoCosmosDocument>(
                CatalogoTextosActivoCosmosDocument.CrearId(idioma),
                PartitionKey,
                cancellationToken: cancellationToken);
            return response.Resource with { Etag = response.ETag };
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private static VersionCatalogoTextos Mapear(CatalogoTextosCosmosDocument document)
        => new(document.ToDomain(), document.Etag);

    private static ErrorConflicto ConflictoEtag()
        => new("El catalogo cambio desde la ultima lectura. Recargalo antes de guardar.");
}
