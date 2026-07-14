namespace ElTejido.Application.Markdown;

/// <summary>
/// Puerto de almacenamiento de artefactos en Blob Storage (09 §6). Guarda el <c>.md</c> en la ruta
/// canonica y devuelve la ruta efectiva. El contenido <b>no</b> contiene secretos (REQ §22.4.9).
/// </summary>
public interface IAlmacenBlob
{
    Task<string> GuardarTextoAsync(string ruta, string contenido, CancellationToken cancellationToken);

    /// <summary>
    /// Borra el blob de la ruta indicada (P-03, reinicio de datos de prueba). Devuelve <c>true</c> si
    /// existia y se elimino, <c>false</c> si no estaba. El artefacto Markdown es regenerable
    /// (REQ §22.4.6), asi que un fallo del almacen se tolera: la implementacion no debe propagar la
    /// excepcion, sino devolver <c>false</c> para que el servicio lo reporte como blob fallido.
    /// </summary>
    Task<bool> EliminarAsync(string ruta, CancellationToken cancellationToken);
}
