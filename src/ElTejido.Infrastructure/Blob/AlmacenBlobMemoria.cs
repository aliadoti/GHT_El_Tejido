using System.Collections.Concurrent;
using ElTejido.Application.Markdown;

namespace ElTejido.Infrastructure.Blob;

/// <summary>
/// Almacen de blobs in-process (fallback cuando no hay <c>Blob:AccountUrl</c> configurado, p. ej.
/// local/CI sin Azurite). Mantiene el contenido en memoria; al reiniciar se pierde, pero el
/// artefacto siempre es regenerable desde datos operativos (REQ §22.4.6). El contenido tambien se
/// embebe en el documento <c>ArtefactoMarkdown</c> para consulta.
/// </summary>
public sealed class AlmacenBlobMemoria : IAlmacenBlob
{
    private readonly ConcurrentDictionary<string, string> _blobs = new(StringComparer.Ordinal);

    public Task<string> GuardarTextoAsync(string ruta, string contenido, CancellationToken cancellationToken)
    {
        _blobs[ruta] = contenido;
        return Task.FromResult(ruta);
    }

    public string? Leer(string ruta) => _blobs.GetValueOrDefault(ruta);
}
