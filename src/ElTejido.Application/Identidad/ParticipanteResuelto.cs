using ElTejido.Domain.Campanas;
using ElTejido.Domain.Participantes;
using ElTejido.Domain.Usuarios;

namespace ElTejido.Application.Identidad;

/// <summary>
/// Participante autorizado y su contexto vigente (06 §3.1): usuario, campania activa, su
/// asociacion a la campania y la pregunta vigente del hilo.
/// </summary>
public sealed record ParticipanteResuelto(
    Usuario Usuario,
    Campania Campania,
    ParticipanteCampania Participante,
    Pregunta PreguntaVigente);
