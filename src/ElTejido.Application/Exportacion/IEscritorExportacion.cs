using ElTejido.Application.Respuestas;

namespace ElTejido.Application.Exportacion;

/// <summary>
/// P-34 §4.5: escribe una exportación ya construida sobre el flujo de salida. El formato vive en el
/// adaptador; la Application solo arma filas de texto. El `csv` se escribe fila por fila; el `xlsx`
/// lo compone la librería, y por eso el tope de filas de <see cref="ExportacionResultados"/> es la
/// protección real de memoria (`§7`).
/// </summary>
public interface IEscritorExportacion
{
    string ContentType(FormatoExportacion formato);

    Task EscribirAsync(
        Stream destino,
        FormatoExportacion formato,
        ExportacionTabular contenido,
        CancellationToken cancellationToken);
}
