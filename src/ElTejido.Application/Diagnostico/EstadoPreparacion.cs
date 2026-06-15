namespace ElTejido.Application.Diagnostico;

/// <summary>
/// Estado de una comprobacion de preparacion (readiness) de una dependencia externa
/// (Key Vault, Cosmos, Blob, WhatsApp). Es informacion de postura de infraestructura: nunca
/// transporta valores de secretos, solo presencia/alcanzabilidad (13 §7, guia de Azure §11).
/// </summary>
public enum EstadoPreparacion
{
    /// <summary>La dependencia esta configurada y accesible.</summary>
    Ok,

    /// <summary>La dependencia falta o no esta configurada (p. ej. secreto ausente).</summary>
    Faltante,

    /// <summary>La dependencia esta configurada pero fallo al verificarse (auth/red).</summary>
    Error,

    /// <summary>La comprobacion no aplica en este modo (p. ej. Cosmos en modo memoria).</summary>
    NoAplica,
}
