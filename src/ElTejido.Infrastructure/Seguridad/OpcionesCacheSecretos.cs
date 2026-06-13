namespace ElTejido.Infrastructure.Seguridad;

/// <summary>
/// Opciones de la cache corta de secretos (10 §4): expiracion en minutos para no golpear
/// Key Vault en cada llamada. Se enlaza desde la seccion de configuracion <see cref="Seccion"/>.
/// </summary>
public sealed class OpcionesCacheSecretos
{
    /// <summary>Seccion de configuracion (02 §6, bloque <c>Seguridad</c>).</summary>
    public const string Seccion = "Seguridad:CacheSecretos";

    /// <summary>Duracion de la cache en minutos. Default 5 (rango sugerido 5–10, 10 §4).</summary>
    public int DuracionMinutos { get; set; } = 5;
}
