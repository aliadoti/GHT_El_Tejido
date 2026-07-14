namespace ElTejido.Application.Reinicio;

/// <summary>
/// P-03 — Sistema de reinicio de datos del flujo. Borra las entidades producidas por las
/// interacciones (conversaciones, mensajes, respuestas, evaluaciones, artefactos Markdown y su blob)
/// y resetea el estado de los participantes, conservando la campania, su configuracion y los
/// usuarios. Habilita repetir las pruebas humanas del flujo sin recrear la campania (cold-start real,
/// <c>Reglas §2.1</c>). Cubre REQ §26, ARQ §6/§13; specs <c>04 §5.3</c>, <c>03 §3.4-§3.10</c>.
/// </summary>
public interface IServicioReinicioDatos
{
    /// <summary>Reinicia los datos de un unico participante dentro de una campania.</summary>
    Task<ReporteReinicioDatos> ReiniciarParticipanteAsync(
        string campaniaId,
        string usuarioId,
        bool reiniciarEnvios,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reinicia los datos de todos los participantes de una campania. Con <paramref name="usuarioIds"/>
    /// no vacio, acota a ese subconjunto; vacio o null = todos.
    /// </summary>
    Task<ReporteReinicioDatos> ReiniciarCampaniaAsync(
        string campaniaId,
        IReadOnlyCollection<string>? usuarioIds,
        bool reiniciarEnvios,
        CancellationToken cancellationToken);
}

/// <summary>Reporte de conteos de un reinicio de datos (P-03), para que el operador confirme la limpieza.</summary>
public sealed record ReporteReinicioDatos(
    int Conversaciones,
    int Mensajes,
    int Respuestas,
    int Evaluaciones,
    int Artefactos,
    int BlobsBorrados,
    int BlobsFallidos,
    int ParticipantesReseteados)
{
    public static readonly ReporteReinicioDatos Vacio = new(0, 0, 0, 0, 0, 0, 0, 0);

    public ReporteReinicioDatos Sumar(ReporteReinicioDatos otro)
        => new(
            Conversaciones + otro.Conversaciones,
            Mensajes + otro.Mensajes,
            Respuestas + otro.Respuestas,
            Evaluaciones + otro.Evaluaciones,
            Artefactos + otro.Artefactos,
            BlobsBorrados + otro.BlobsBorrados,
            BlobsFallidos + otro.BlobsFallidos,
            ParticipantesReseteados + otro.ParticipantesReseteados);
}
