using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using ElTejido.Application.Diagnostico;

namespace ElTejido.Infrastructure.Diagnostico;

/// <summary>
/// Comprueba que el contenedor de Blob para los Markdown sea accesible por la identidad administrada
/// (09 §6, guia de Azure §3/§7). Sin <c>Blob:AccountUrl</c> el sistema usa el almacen en memoria, asi
/// que reporta <see cref="EstadoPreparacion.NoAplica"/>. La verificacion solo consulta existencia del
/// contenedor (no escribe blobs de prueba).
/// </summary>
public sealed class ComprobacionBlob : IComprobacionPreparacion
{
    private const string Componente = "blob";

    private readonly string? _accountUrl;
    private readonly string _contenedor;

    public ComprobacionBlob(string? accountUrl, string contenedor)
    {
        _accountUrl = accountUrl;
        _contenedor = contenedor;
    }

    public async Task<IReadOnlyList<ResultadoComprobacion>> ComprobarAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_accountUrl))
        {
            return new[]
            {
                new ResultadoComprobacion(Componente, EstadoPreparacion.NoAplica, "Almacen en memoria (sin Blob:AccountUrl)."),
            };
        }

        try
        {
            var servicio = new BlobServiceClient(new Uri(_accountUrl), new DefaultAzureCredential());
            var contenedor = servicio.GetBlobContainerClient(_contenedor);
            var existe = await contenedor.ExistsAsync(cancellationToken);

            return existe.Value
                ? new[] { new ResultadoComprobacion(Componente, EstadoPreparacion.Ok, $"Contenedor '{_contenedor}' accesible.") }
                : new[] { new ResultadoComprobacion(Componente, EstadoPreparacion.Faltante, $"El contenedor '{_contenedor}' no existe.") };
        }
        catch (RequestFailedException ex) when (ex.Status is 401 or 403)
        {
            return new[]
            {
                new ResultadoComprobacion(
                    Componente,
                    EstadoPreparacion.Error,
                    $"Acceso denegado (HTTP {ex.Status}). Revisa el rol Storage Blob Data Contributor."),
            };
        }
        catch (Exception ex)
        {
            return new[]
            {
                new ResultadoComprobacion(Componente, EstadoPreparacion.Error, $"Blob inaccesible: {ex.GetType().Name}."),
            };
        }
    }
}
