using ElTejido.Domain.Campanas;
using ElTejido.Domain.Participantes;
using ElTejido.Domain.Usuarios;

namespace ElTejido.Application.Identidad;

/// <summary>
/// Resuelve un numero entrante al participante autorizado para una campania activa, o a un
/// rechazo tipado (06 §3.1, REQ §26.3). Es el guardian de matricula previo a procesar cualquier
/// respuesta entrante.
/// </summary>
public interface IResolutorParticipante
{
    Task<ResultadoResolucion> ResolverAsync(string numeroCrudo, CancellationToken cancellationToken);

    /// <summary>
    /// P-26 (06 §3): valida usuario/rol/estado y devuelve TODAS las campanias activas autorizadas con
    /// pregunta activa, sin elegir una. El enrutamiento de participacion decide despues entre trabajo
    /// pendiente, continuidad o menu. Rechazos neutrales identicos a <see cref="ResolverAsync"/>.
    /// </summary>
    Task<ResultadoCandidatos> ResolverCandidatosAsync(string numeroCrudo, CancellationToken cancellationToken);
}

/// <summary>Campania activa a la que el usuario esta asociado y habilitado, con su pregunta vigente (P-26 §5.2).</summary>
public sealed record CandidatoCampania(
    ParticipanteCampania Participante,
    Campania Campania,
    Pregunta PreguntaVigente);

/// <summary>Resultado de resolver los candidatos P-26: autorizado con 1..N campanias o rechazo neutral tipado.</summary>
public abstract record ResultadoCandidatos
{
    private ResultadoCandidatos()
    {
    }

    /// <summary>Usuario autorizado con al menos una campania activa candidata.</summary>
    public sealed record Autorizado(Usuario Usuario, IReadOnlyList<CandidatoCampania> Candidatos) : ResultadoCandidatos;

    /// <summary>Numero no autorizado; el motivo solo se registra, no se revela (06 §3.3).</summary>
    public sealed record NoAutorizado(MotivoRechazo Motivo) : ResultadoCandidatos;
}
