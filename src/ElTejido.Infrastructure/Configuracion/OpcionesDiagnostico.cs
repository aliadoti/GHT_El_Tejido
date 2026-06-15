namespace ElTejido.Infrastructure.Configuracion;

/// <summary>
/// Configuracion del endpoint de preparacion <c>/health/ready</c> (seccion <c>Diagnostico</c>).
/// La clave puede venir de Key Vault (<see cref="ClaveSecretName"/>, recomendado) o directamente de
/// configuracion/app settings (<see cref="Clave"/>). Si ninguna esta configurada, el endpoint queda
/// deshabilitado (responde 404). Nunca se loguea el valor de la clave.
/// </summary>
public sealed class OpcionesDiagnostico
{
    public const string Seccion = "Diagnostico";

    /// <summary>Nombre del secreto en Key Vault que contiene la clave de diagnostico (preferido).</summary>
    public string? ClaveSecretName { get; set; }

    /// <summary>Clave directa (app settings/user-secrets) si no se usa Key Vault.</summary>
    public string? Clave { get; set; }
}
