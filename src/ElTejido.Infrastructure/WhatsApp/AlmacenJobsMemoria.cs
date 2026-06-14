using System.Collections.Concurrent;
using ElTejido.Application.WhatsApp;

namespace ElTejido.Infrastructure.WhatsApp;

/// <summary>
/// Almacen de jobs de envio in-process (02 §5, 04 §5.9). Estado por job protegido por bloqueo para
/// soportar el reporte concurrente del trabajador. No persiste: al reiniciar el proceso se pierde
/// (el envio se redispara desde el portal por estado de participante).
/// </summary>
public sealed class AlmacenJobsMemoria : IAlmacenJobs
{
    private readonly ConcurrentDictionary<string, EstadoJobMutable> _jobs = new(StringComparer.Ordinal);
    private readonly TimeProvider _tiempo;

    public AlmacenJobsMemoria(TimeProvider tiempo)
    {
        _tiempo = tiempo;
    }

    public JobEnvio CrearJob(string campaniaId, int encolados)
    {
        var estado = new EstadoJobMutable(
            "job_" + Guid.NewGuid().ToString("N"),
            campaniaId,
            encolados,
            _tiempo.GetUtcNow());
        _jobs[estado.Id] = estado;
        return estado.Snapshot();
    }

    public void RegistrarResultado(string jobId, bool exito)
    {
        if (_jobs.TryGetValue(jobId, out var estado))
        {
            estado.Registrar(exito);
        }
    }

    public JobEnvio? ObtenerJob(string jobId)
        => _jobs.TryGetValue(jobId, out var estado) ? estado.Snapshot() : null;

    private sealed class EstadoJobMutable
    {
        private readonly object _candado = new();
        private int _enviados;
        private int _errores;

        public EstadoJobMutable(string id, string campaniaId, int encolados, DateTimeOffset creadoEn)
        {
            Id = id;
            CampaniaId = campaniaId;
            Encolados = encolados;
            CreadoEn = creadoEn;
        }

        public string Id { get; }

        public string CampaniaId { get; }

        public int Encolados { get; }

        public DateTimeOffset CreadoEn { get; }

        public void Registrar(bool exito)
        {
            lock (_candado)
            {
                if (exito)
                {
                    _enviados++;
                }
                else
                {
                    _errores++;
                }
            }
        }

        public JobEnvio Snapshot()
        {
            lock (_candado)
            {
                var estado = _enviados + _errores >= Encolados ? EstadoJob.Completado : EstadoJob.EnProceso;
                return new JobEnvio(Id, CampaniaId, Encolados, _enviados, _errores, estado, CreadoEn);
            }
        }
    }
}
