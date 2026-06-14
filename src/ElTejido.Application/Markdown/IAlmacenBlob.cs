namespace ElTejido.Application.Markdown;

/// <summary>
/// Puerto de almacenamiento de artefactos en Blob Storage (09 §6). Guarda el <c>.md</c> en la ruta
/// canonica y devuelve la ruta efectiva. El contenido <b>no</b> contiene secretos (REQ §22.4.9).
/// </summary>
public interface IAlmacenBlob
{
    Task<string> GuardarTextoAsync(string ruta, string contenido, CancellationToken cancellationToken);
}
