namespace ElTejido.Application.Mantenimiento;

/// <summary>
/// P-15 — Purga total de datos de campañas para empezar pruebas en frío. A diferencia del reinicio
/// P-03 (que conserva campañas, configuración y usuarios y solo limpia el flujo), esta operación
/// <b>borra fisicamente</b> todas las campañas y todo lo asociado (preguntas y mensajes viven dentro
/// del documento de campaña; conversaciones, mensajes, respuestas, evaluaciones, artefactos Markdown y
/// sus blobs; participantes y envios) y elimina los usuarios <b>no administrativos</b> (rol
/// Participante). Conserva siempre: usuarios administrativos (Admin/Visor), Config LLM, Rúbricas,
/// Prompts y Tags. Es irreversible; el endpoint que la expone exige rol admin + CSRF, una palabra de
/// confirmacion y el flag operativo <c>Seguridad:PermitirReinicioDatos</c>.
/// </summary>
public interface IServicioPurgaCampanias
{
    /// <summary>Ejecuta la purga total y devuelve el reporte de conteos para que el operador confirme la limpieza.</summary>
    Task<ReportePurgaCampanias> PurgarTodoAsync(CancellationToken cancellationToken);
}

/// <summary>Conteos de una purga total (P-15).</summary>
public sealed record ReportePurgaCampanias(
    int Campanias,
    int Conversaciones,
    int Mensajes,
    int Respuestas,
    int Evaluaciones,
    int Artefactos,
    int BlobsBorrados,
    int BlobsFallidos,
    int Participantes,
    int UsuariosBorrados)
{
    public static readonly ReportePurgaCampanias Vacio = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
}
