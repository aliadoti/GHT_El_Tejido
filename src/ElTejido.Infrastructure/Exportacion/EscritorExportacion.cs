using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using ElTejido.Application.Exportacion;
using ElTejido.Application.Respuestas;

namespace ElTejido.Infrastructure.Exportacion;

/// <summary>
/// Adaptador de escritura de exportaciones (P-34 §4.5). `xlsx` con ClosedXML —la misma librería que
/// ya usa la carga masiva de I-08, sin dependencias nuevas—, con cabeceras congeladas y anchos
/// ajustados; `csv` en UTF-8 **con BOM**, porque sin él Excel abre los acentos rotos.
/// </summary>
public sealed class EscritorExportacion : IEscritorExportacion
{
    /// <summary>Ancho máximo de columna: sin tope, una idea larga deja la hoja inservible.</summary>
    private const double AnchoMaximo = 60;

    public string ContentType(FormatoExportacion formato)
        => formato == FormatoExportacion.Csv
            ? "text/csv; charset=utf-8"
            : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public async Task EscribirAsync(
        Stream destino,
        FormatoExportacion formato,
        ExportacionTabular contenido,
        CancellationToken cancellationToken)
    {
        if (formato == FormatoExportacion.Csv)
        {
            await EscribirCsvAsync(destino, contenido, cancellationToken);
            return;
        }

        EscribirXlsx(destino, contenido);
    }

    /// <summary>
    /// El CSV no tiene hojas: el alcance va como líneas iniciales prefijadas con `#`, que Excel
    /// muestra como filas y un parser puede descartar (04 §5.8). Se escribe fila por fila.
    /// </summary>
    private static async Task EscribirCsvAsync(
        Stream destino, ExportacionTabular contenido, CancellationToken cancellationToken)
    {
        await using var escritor = new StreamWriter(destino, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), leaveOpen: true);
        foreach (var (clave, valor) in contenido.Filtros.Lineas)
        {
            await escritor.WriteLineAsync($"# {clave}: {valor}".AsMemory(), cancellationToken);
        }

        await escritor.WriteLineAsync(Linea(contenido.Tabla.Encabezados).AsMemory(), cancellationToken);
        foreach (var fila in contenido.Tabla.Filas)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await escritor.WriteLineAsync(Linea(fila).AsMemory(), cancellationToken);
        }

        await escritor.FlushAsync(cancellationToken);
    }

    private static void EscribirXlsx(Stream destino, ExportacionTabular contenido)
    {
        using var libro = new XLWorkbook();

        var alcance = libro.Worksheets.Add("Filtros aplicados");
        var fila = 1;
        foreach (var (clave, valor) in contenido.Filtros.Lineas)
        {
            alcance.Cell(fila, 1).Value = clave;
            alcance.Cell(fila, 2).Value = valor;
            fila++;
        }

        alcance.Column(1).Style.Font.Bold = true;
        AjustarAnchos(alcance);

        var hoja = libro.Worksheets.Add(contenido.Tabla.Nombre);
        for (var columna = 0; columna < contenido.Tabla.Encabezados.Count; columna++)
        {
            hoja.Cell(1, columna + 1).Value = contenido.Tabla.Encabezados[columna];
        }

        hoja.Row(1).Style.Font.Bold = true;
        hoja.SheetView.FreezeRows(1);

        for (var indice = 0; indice < contenido.Tabla.Filas.Count; indice++)
        {
            var valores = contenido.Tabla.Filas[indice];
            for (var columna = 0; columna < valores.Count; columna++)
            {
                // Texto explícito: sin esto, Excel convierte códigos y fechas a su antojo.
                hoja.Cell(indice + 2, columna + 1).SetValue(valores[columna]);
            }
        }

        AjustarAnchos(hoja);
        libro.SaveAs(destino);
    }

    private static void AjustarAnchos(IXLWorksheet hoja)
    {
        hoja.Columns().AdjustToContents();
        foreach (var columna in hoja.ColumnsUsed())
        {
            if (columna.Width > AnchoMaximo)
            {
                columna.Width = AnchoMaximo;
            }
        }
    }

    /// <summary>Comillas dobles solo donde hacen falta, escapadas como manda el formato.</summary>
    private static string Linea(IReadOnlyList<string> valores)
        => string.Join(',', valores.Select(Campo));

    private static string Campo(string valor)
    {
        var texto = valor ?? string.Empty;
        var necesitaComillas = texto.Contains(',', StringComparison.Ordinal)
            || texto.Contains('"', StringComparison.Ordinal)
            || texto.Contains('\n', StringComparison.Ordinal)
            || texto.Contains('\r', StringComparison.Ordinal);
        return necesitaComillas
            ? string.Create(CultureInfo.InvariantCulture, $"\"{texto.Replace("\"", "\"\"", StringComparison.Ordinal)}\"")
            : texto;
    }
}
