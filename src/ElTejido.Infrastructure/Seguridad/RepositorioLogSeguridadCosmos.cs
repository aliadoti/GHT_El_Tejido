using ElTejido.Application.Seguridad;
using ElTejido.Domain.Seguridad;
using Microsoft.Azure.Cosmos;

namespace ElTejido.Infrastructure.Seguridad;

/// <summary>
/// Adaptador Cosmos append-only del contenedor security para LogSeguridad.
/// Cubre 03 §3.15, 10 §6.4 y REQ §30.
/// </summary>
public sealed class RepositorioLogSeguridadCosmos : IRepositorioLogSeguridad
{
    private readonly ISecurityCosmosContainer _container;

    public RepositorioLogSeguridadCosmos(Container container)
        : this(new SecurityCosmosContainer(container))
    {
    }

    internal RepositorioLogSeguridadCosmos(ISecurityCosmosContainer container)
    {
        _container = container;
    }

    public async Task RegistrarAsync(LogSeguridad log, CancellationToken cancellationToken)
    {
        var document = LogSeguridadCosmosDocument.FromDomain(log);
        await _container.CreateLogAsync(document, document.Pk, cancellationToken);
    }
}
