namespace ElTejido.Application.Identidad;

/// <summary>
/// Motivo interno por el que un numero no queda autorizado a participar (06 §3.2). Nunca se
/// revela al usuario (la respuesta es neutral, 06 §3.3); solo se registra en <c>LogSeguridad</c>.
/// </summary>
public enum MotivoRechazo
{
    /// <summary>El numero no corresponde a ningun usuario (06 §3.2 paso 2).</summary>
    NoMatriculado,

    /// <summary>El usuario existe pero esta inactivo (06 §3.2 paso 3).</summary>
    Inactivo,

    /// <summary>El usuario no tiene rol participante (06 §3.2 paso 4).</summary>
    NoEsParticipante,

    /// <summary>No hay campania activa asociada al participante (06 §3.2 paso 5).</summary>
    SinCampaniaActiva,

    /// <summary>La campania activa no tiene una pregunta vigente (06 §3.2 paso 6).</summary>
    SinPreguntaVigente,
}
