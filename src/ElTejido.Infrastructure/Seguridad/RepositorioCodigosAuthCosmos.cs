using ElTejido.Application.Seguridad;
using ElTejido.Domain.Identidad;
using ElTejido.Domain.Seguridad;
using Microsoft.Azure.Cosmos;

namespace ElTejido.Infrastructure.Seguridad;

/// <summary>
/// Adaptador Cosmos del contenedor security para CodigoAuthAdmin (OTP con TTL nativo).
/// Cubre 03 §3.14, 06 §4 y REQ §10.3.
/// </summary>
public sealed class RepositorioCodigosAuthCosmos : IRepositorioCodigosAuth
{
    private readonly ISecurityCosmosContainer _container;

    public RepositorioCodigosAuthCosmos(Container container)
        : this(new SecurityCosmosContainer(container))
    {
    }

    internal RepositorioCodigosAuthCosmos(ISecurityCosmosContainer container)
    {
        _container = container;
    }

    public async Task GuardarAsync(CodigoAuthAdmin codigo, CancellationToken cancellationToken)
    {
        var document = CodigoAuthAdminCosmosDocument.FromDomain(codigo);
        await _container.UpsertCodigoAsync(document, document.Pk, cancellationToken);
    }

    public async Task<CodigoAuthAdmin?> ObtenerVigenteMasRecienteAsync(
        NumeroWhatsApp numero,
        CancellationToken cancellationToken)
    {
        var document = await _container.QueryCodigoMasRecienteAsync(numero.Valor, cancellationToken);
        return document?.ToDomain();
    }
}
