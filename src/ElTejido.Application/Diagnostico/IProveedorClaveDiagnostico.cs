namespace ElTejido.Application.Diagnostico;

/// <summary>
/// Resuelve la clave esperada para acceder al endpoint de preparacion (<c>/health/ready</c>).
/// Devuelve <c>null</c> cuando no hay clave configurada: en ese caso el endpoint se comporta como
/// inexistente (404), para no exponer la postura de infraestructura en un App Service publico.
/// </summary>
public interface IProveedorClaveDiagnostico
{
    Task<string?> ObtenerClaveEsperadaAsync(CancellationToken cancellationToken);
}
