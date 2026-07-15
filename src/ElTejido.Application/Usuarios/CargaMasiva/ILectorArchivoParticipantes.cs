namespace ElTejido.Application.Usuarios.CargaMasiva;

/// <summary>
/// Puerto de lectura de un archivo de participantes (I-08). Aisla el formato del archivo del
/// <c>ServicioCargaMasiva</c>: hoy solo hay un lector CSV (sin dependencia, Sprint 1a), pero la
/// frontera queda abierta para agregar un lector <c>.xlsx</c> en Infraestructura (I-08 §7) sin tocar
/// el servicio de aplicacion. El servicio elige el lector por <see cref="Soporta"/>.
/// </summary>
public interface ILectorArchivoParticipantes
{
    /// <summary>Indica si este lector procesa la extension dada (con punto, p. ej. <c>".csv"</c>).</summary>
    bool Soporta(string extensionArchivo);

    /// <summary>
    /// Parsea el contenido en filas crudas. No valida los datos (eso es del servicio); solo separa
    /// columnas y descarta la cabecera. Lanza <see cref="Common.ErrorValidacion"/> si el archivo no
    /// tiene la cabecera esperada o esta vacio.
    /// </summary>
    Task<IReadOnlyList<FilaParticipanteCarga>> LeerAsync(Stream contenido, CancellationToken cancellationToken);
}
