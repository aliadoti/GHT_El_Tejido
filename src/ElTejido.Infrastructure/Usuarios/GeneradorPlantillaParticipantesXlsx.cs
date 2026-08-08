using ClosedXML.Excel;
using ElTejido.Application.Usuarios.CargaMasiva;

namespace ElTejido.Infrastructure.Usuarios;

/// <summary>
/// Construye el <c>.xlsx</c> vacio de la plantilla oficial (I-08 §4.5) a partir de
/// <see cref="PlantillaParticipantes.Cabecera"/>, de modo que lo que se descarga es exactamente lo que
/// el lector espera. Solo cabecera: los datos los pone GHT.
/// </summary>
public sealed class GeneradorPlantillaParticipantesXlsx : IGeneradorPlantillaParticipantes
{
    public string NombreArchivo => "plantilla_participantes_v1.xlsx";

    public string TipoContenido => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public byte[] Generar()
    {
        using var memoria = new MemoryStream();
        using (var libro = new XLWorkbook())
        {
            var hoja = libro.Worksheets.Add("Participantes");
            for (var columna = 0; columna < PlantillaParticipantes.Cabecera.Count; columna++)
            {
                var celda = hoja.Cell(1, columna + 1);
                celda.Value = PlantillaParticipantes.Cabecera[columna];
                celda.Style.Font.Bold = true;
            }

            // El telefono se escribe como texto para que Excel no se coma los ceros a la izquierda ni
            // convierta el numero a notacion cientifica al diligenciar la plantilla.
            hoja.Column(PlantillaParticipantes.IndiceTelefono + 1).Style.NumberFormat.Format = "@";
            hoja.Columns().AdjustToContents();
            libro.SaveAs(memoria);
        }

        return memoria.ToArray();
    }
}
