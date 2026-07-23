using ElTejido.Application.Campanas;
using ElTejido.Domain.Campanas;
using Microsoft.Azure.Cosmos;

namespace ElTejido.Infrastructure.Campanas;

/// <summary>
/// Adaptador Cosmos del contenedor campaigns para Campania.
/// Cubre REQ 11, 15, 16 y ARQ 8-9 conservando el dominio libre de DTOs Cosmos.
/// </summary>
public sealed class RepositorioCampaniasCosmos : IRepositorioCampanias
{
    private readonly ICampaniasCosmosContainer _container;

    public RepositorioCampaniasCosmos(Container container)
        : this(new CampaniasCosmosContainer(container))
    {
    }

    internal RepositorioCampaniasCosmos(ICampaniasCosmosContainer container)
    {
        _container = container;
    }

    public async Task GuardarCampaniaAsync(Campania campania, CancellationToken cancellationToken)
    {
        var document = CampaniaCosmosDocument.FromDomain(campania);
        await _container.UpsertAsync(document, document.Id, cancellationToken);
    }

    public async Task<Campania?> ObtenerCampaniaPorIdAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var document = await _container.ReadByIdAsync(id.Trim(), cancellationToken);
        return document?.ToDomain();
    }

    public async Task<IReadOnlyCollection<Campania>> BuscarCampaniasAsync(
        FiltroCampanias filtro,
        CancellationToken cancellationToken)
    {
        var documents = await _container.QueryAsync(
            new FiltroCampaniasCosmos(
                filtro.Estado is null ? null : CampaniaCosmosDocument.ToCosmosEstado(filtro.Estado.Value),
                filtro.Busqueda),
            cancellationToken);

        return documents
            .Select(document => document.ToDomain())
            .ToArray();
    }

    public async Task EliminarCampaniaAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        await _container.DeleteAsync(id.Trim(), cancellationToken);
    }
}
