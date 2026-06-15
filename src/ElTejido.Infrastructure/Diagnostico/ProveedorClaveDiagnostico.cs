using ElTejido.Application.Diagnostico;
using ElTejido.Application.Seguridad;
using ElTejido.Infrastructure.Configuracion;
using Microsoft.Extensions.Options;

namespace ElTejido.Infrastructure.Diagnostico;

/// <summary>
/// Resuelve la clave esperada de diagnostico: primero por <c>Diagnostico:ClaveSecretName</c>
/// (Key Vault, recomendado), si no por <c>Diagnostico:Clave</c> (app settings). Devuelve <c>null</c>
/// cuando no hay clave o el secreto no se puede leer, dejando el endpoint deshabilitado.
/// </summary>
public sealed class ProveedorClaveDiagnostico : IProveedorClaveDiagnostico
{
    private readonly OpcionesDiagnostico _opciones;
    private readonly ISecretProvider _secretos;

    public ProveedorClaveDiagnostico(IOptions<OpcionesDiagnostico> opciones, ISecretProvider secretos)
    {
        _opciones = opciones.Value;
        _secretos = secretos;
    }

    public async Task<string?> ObtenerClaveEsperadaAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_opciones.ClaveSecretName))
        {
            try
            {
                var valor = await _secretos.ObtenerSecretoAsync(_opciones.ClaveSecretName, cancellationToken);
                return string.IsNullOrWhiteSpace(valor) ? null : valor;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Sin clave legible el endpoint queda deshabilitado (404), nunca abierto.
                return null;
            }
        }

        return string.IsNullOrWhiteSpace(_opciones.Clave) ? null : _opciones.Clave;
    }
}
