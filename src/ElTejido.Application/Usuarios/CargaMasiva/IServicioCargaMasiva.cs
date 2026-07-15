namespace ElTejido.Application.Usuarios.CargaMasiva;

/// <summary>
/// Caso de uso de carga masiva de participantes (I-08, 04 §5.1). Lee un archivo server-side, hace
/// upsert por numero normalizado (06 §2), crea las tags faltantes, asocia opcionalmente a una campania
/// y devuelve un reporte por fila. Idempotente: reprocesar el mismo archivo no duplica.
/// </summary>
public interface IServicioCargaMasiva
{
    /// <param name="nombreArchivo">Nombre original (para resolver la extension y elegir el lector).</param>
    /// <param name="contenido">Stream del archivo (el llamador valida tamano/extension en el edge).</param>
    /// <param name="campaniaId">Opcional: si se envia, asocia los creados/actualizados a la campania.</param>
    Task<ReporteCargaMasiva> CargarAsync(
        string nombreArchivo,
        Stream contenido,
        string? campaniaId,
        CancellationToken cancellationToken);
}
