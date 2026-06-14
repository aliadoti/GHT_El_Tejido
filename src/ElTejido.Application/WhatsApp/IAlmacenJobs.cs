namespace ElTejido.Application.WhatsApp;

/// <summary>
/// Almacen de estado de jobs de envio (04 §5.4/§5.9). En el MVP es in-process (02 §5); si el
/// proceso se reinicia los jobs en curso se pierden y el envio se redispara desde el portal por
/// estado de participante (02 §5, decision documentada en 13).
/// </summary>
public interface IAlmacenJobs
{
    /// <summary>Crea un job con la cantidad de items encolados y lo devuelve en estado <c>EnProceso</c>.</summary>
    JobEnvio CrearJob(string campaniaId, int encolados);

    /// <summary>Registra el resultado de un item; al completar todos marca el job como <c>Completado</c>.</summary>
    void RegistrarResultado(string jobId, bool exito);

    JobEnvio? ObtenerJob(string jobId);
}
