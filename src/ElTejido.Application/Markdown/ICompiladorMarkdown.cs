using ElTejido.Domain.Campanas;
using ElTejido.Domain.Respuestas;

namespace ElTejido.Application.Markdown;

/// <summary>
/// Puerto de compilacion de Markdown (09 §2): compila una respuesta evaluada en un artefacto durable,
/// atribuido y regenerable, lo guarda en Blob y registra sus metadatos en Cosmos. El artefacto es
/// cache materializada; siempre regenerable desde datos operativos (REQ §22.4.6).
/// </summary>
public interface ICompiladorMarkdown
{
    Task<ArtefactoMarkdown> CompilarAsync(SolicitudCompilacion solicitud, CancellationToken cancellationToken);
}

/// <summary>
/// Parametros de compilacion (09 §2). MVP: tipo <c>respuesta</c> con <see cref="RespuestaId"/>.
/// </summary>
public sealed record SolicitudCompilacion(
    string CampaniaId,
    TipoArtefactoMarkdown Tipo,
    string? RespuestaId,
    string? UsuarioId,
    string? PreguntaId);
