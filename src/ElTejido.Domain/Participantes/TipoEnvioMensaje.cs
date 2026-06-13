namespace ElTejido.Domain.Participantes;

/// <summary>
/// Tipo de mensaje saliente registrado en el contenedor participants.
/// Cubre 03 seccion 3.5 y REQ 29.6.
/// </summary>
public enum TipoEnvioMensaje
{
    Inicial,
    Reenvio,
    Repregunta,
    Cierre,
    Autenticacion,
}
