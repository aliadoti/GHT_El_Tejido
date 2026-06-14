namespace ElTejido.Application.WhatsApp;

/// <summary>Estado agregado de un job de envio (04 §5.9).</summary>
public enum EstadoJob
{
    EnProceso,
    Completado,
}

/// <summary>
/// Instantanea del estado de un job de envio masivo (04 §5.4/§5.9). Es la vista que consulta el
/// portal por <c>GET /api/admin/jobs/{jobId}</c>.
/// </summary>
public sealed record JobEnvio(
    string Id,
    string CampaniaId,
    int Encolados,
    int Enviados,
    int Errores,
    EstadoJob Estado,
    DateTimeOffset CreadoEn);
