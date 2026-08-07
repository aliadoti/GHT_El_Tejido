namespace ElTejido.Application.Usuarios.CargaMasiva;

/// <summary>
/// Caso de uso de carga masiva de participantes (I-08, 04 §5.1). Lee un archivo server-side con la
/// plantilla oficial de GHT, procesa fila por fila (una fila mala no aborta el lote), hace upsert por
/// telefono <b>entre usuarios activos</b> (06 §2), asegura la tag de empresa, asocia opcionalmente a
/// una campania y devuelve un reporte por fila. Idempotente: reprocesar el mismo archivo no duplica.
/// </summary>
public interface IServicioCargaMasiva
{
    /// <param name="nombreArchivo">Nombre original (para resolver la extension y elegir el lector).</param>
    /// <param name="contenido">Stream del archivo (el llamador valida tamano/extension en el edge).</param>
    /// <param name="campaniaId">Opcional: si se envia, asocia los creados/actualizados a la campania.</param>
    /// <param name="modo">
    /// <see cref="ModoCargaMasiva.Upsert"/> (default) o <see cref="ModoCargaMasiva.SoloActualizar"/>
    /// (I-08 §4.3).
    /// </param>
    /// <param name="resoluciones">
    /// Decisiones del admin para las filas que quedaron en <c>conflicto_titular</c> en una pasada
    /// anterior del <b>mismo archivo</b> (I-08 §4.4). Vacio en la primera pasada.
    /// </param>
    Task<ReporteCargaMasiva> CargarAsync(
        string nombreArchivo,
        Stream contenido,
        string? campaniaId,
        string modo,
        IReadOnlyCollection<ResolucionConflictoTitular> resoluciones,
        CancellationToken cancellationToken);
}
