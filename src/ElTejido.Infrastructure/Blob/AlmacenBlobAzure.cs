using System.Text;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using ElTejido.Application.Markdown;

namespace ElTejido.Infrastructure.Blob;

/// <summary>
/// Almacen de blobs sobre Azure Blob Storage (09 §6). Sube el <c>.md</c> a la ruta canonica
/// (sobreescribe al regenerar, MVP) con <c>text/markdown</c>. El acceso usa la credencial del
/// contenedor inyectado (Managed Identity via <c>DefaultAzureCredential</c> en el composition root).
/// </summary>
public sealed class AlmacenBlobAzure : IAlmacenBlob
{
    private readonly BlobContainerClient _contenedor;

    public AlmacenBlobAzure(BlobContainerClient contenedor)
    {
        _contenedor = contenedor;
    }

    public async Task<string> GuardarTextoAsync(string ruta, string contenido, CancellationToken cancellationToken)
    {
        var blob = _contenedor.GetBlobClient(ruta);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(contenido));
        await blob.UploadAsync(
            stream,
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = "text/markdown; charset=utf-8" } },
            cancellationToken);
        return ruta;
    }
}
