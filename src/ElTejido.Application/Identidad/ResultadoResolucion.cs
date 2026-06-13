namespace ElTejido.Application.Identidad;

/// <summary>
/// Resultado tipado de resolver un numero entrante (06 §3.1): autorizado con su contexto, o no
/// autorizado con un motivo interno. Jerarquia cerrada para forzar el manejo de ambos casos.
/// </summary>
public abstract record ResultadoResolucion
{
    private ResultadoResolucion()
    {
    }

    /// <summary>Numero autorizado a participar en una campania activa.</summary>
    public sealed record Autorizado(ParticipanteResuelto Participante) : ResultadoResolucion;

    /// <summary>Numero no autorizado; el <paramref name="Motivo"/> solo se registra, no se revela.</summary>
    public sealed record NoAutorizado(MotivoRechazo Motivo) : ResultadoResolucion;
}
