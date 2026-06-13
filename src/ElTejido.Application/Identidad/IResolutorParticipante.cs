namespace ElTejido.Application.Identidad;

/// <summary>
/// Resuelve un numero entrante al participante autorizado para una campania activa, o a un
/// rechazo tipado (06 §3.1, REQ §26.3). Es el guardian de matricula previo a procesar cualquier
/// respuesta entrante.
/// </summary>
public interface IResolutorParticipante
{
    Task<ResultadoResolucion> ResolverAsync(string numeroCrudo, CancellationToken cancellationToken);
}
