using System.Globalization;
using System.Text;
using ElTejido.Application.Campanas;
using ElTejido.Application.Common;
using ElTejido.Application.Respuestas;
using ElTejido.Application.Usuarios;
using ElTejido.Domain.Campanas;
using ElTejido.Domain.Respuestas;
using ElTejido.Domain.Usuarios;
using DominioEvaluacion = ElTejido.Domain.Evaluacion.Evaluacion;

namespace ElTejido.Application.Markdown;

/// <summary>
/// Compila un artefacto Markdown de forma <b>determinista</b> desde los datos operativos (09 §4-§5):
/// carga respuesta+evaluacion+usuario+campania+pregunta, renderiza la plantilla estandar (sin
/// secretos, REQ §22.4.9), lo guarda en Blob y registra/actualiza el <c>ArtefactoMarkdown</c>
/// incrementando la version al regenerar (09 §7). El id del artefacto es estable por respuesta para
/// que la regeneracion sobreescriba la ruta canonica.
/// </summary>
public sealed class CompiladorMarkdown : ICompiladorMarkdown
{
    private readonly IRepositorioRespuestas _respuestas;
    private readonly IRepositorioUsuarios _usuarios;
    private readonly IRepositorioCampanias _campanias;
    private readonly IAlmacenBlob _blob;
    private readonly TimeProvider _tiempo;

    public CompiladorMarkdown(
        IRepositorioRespuestas respuestas,
        IRepositorioUsuarios usuarios,
        IRepositorioCampanias campanias,
        IAlmacenBlob blob,
        TimeProvider tiempo)
    {
        _respuestas = respuestas;
        _usuarios = usuarios;
        _campanias = campanias;
        _blob = blob;
        _tiempo = tiempo;
    }

    public async Task<ArtefactoMarkdown> CompilarAsync(SolicitudCompilacion solicitud, CancellationToken cancellationToken)
    {
        var campaniaId = Requerir(solicitud.CampaniaId, "campaniaId");
        var respuestaId = Requerir(solicitud.RespuestaId, "respuestaId");

        var respuesta = await _respuestas.ObtenerRespuestaAsync(campaniaId, respuestaId, cancellationToken)
            ?? throw new ErrorNoEncontrado("La respuesta no existe.");

        var evaluacion = await _respuestas.ObtenerEvaluacionPorRespuestaAsync(campaniaId, respuestaId, cancellationToken)
            ?? throw new ErrorNoEncontrado("La evaluacion de la respuesta no existe.");

        var usuario = await _usuarios.ObtenerUsuarioPorIdAsync(respuesta.UsuarioId, cancellationToken)
            ?? throw new ErrorNoEncontrado("El usuario de la respuesta no existe.");

        var campania = await _campanias.ObtenerCampaniaPorIdAsync(campaniaId, cancellationToken)
            ?? throw new ErrorNoEncontrado("La campania no existe.");

        var pregunta = campania.Preguntas.FirstOrDefault(p => p.Id == respuesta.PreguntaId)
            ?? throw new ErrorNoEncontrado("La pregunta de la respuesta no existe.");

        var contenido = Renderizar(campania, usuario, pregunta, respuesta, evaluacion);

        var tipoTexto = solicitud.Tipo.ToString().ToLowerInvariant();
        var blobPath = $"campanias/{campaniaId}/{tipoTexto}/{respuestaId}.md";

        var artefactoId = "md_" + respuestaId;
        var existente = await _respuestas.ObtenerArtefactoAsync(campaniaId, artefactoId, cancellationToken);
        var ahora = _tiempo.GetUtcNow();

        var artefacto = ArtefactoMarkdown.Crear(
            artefactoId,
            campaniaId,
            solicitud.Tipo,
            respuesta.UsuarioId,
            respuesta.PreguntaId,
            respuesta.Id,
            evaluacion.Id,
            contenido,
            blobPath,
            EstadoArtefacto.Generado,
            (existente?.Version ?? 0) + 1,
            existente?.CreadoEn ?? ahora,
            ahora);

        // El Blob y el documento embebido guardan el mismo contenido canonico (09 §6).
        await _blob.GuardarTextoAsync(blobPath, artefacto.ContenidoMarkdown, cancellationToken);
        await _respuestas.GuardarArtefactoAsync(artefacto, cancellationToken);
        return artefacto;
    }

    private static string Renderizar(
        Campania campania,
        Usuario usuario,
        Pregunta pregunta,
        Respuesta respuesta,
        DominioEvaluacion evaluacion)
    {
        var sb = new StringBuilder();
        sb.Append("# Aporte de ").AppendLine(usuario.Nombre);
        sb.AppendLine();
        sb.AppendLine("## Metadatos");
        sb.Append("- Campaña: ").AppendLine(campania.Nombre);
        sb.Append("- Participante: ").AppendLine(usuario.Nombre);
        sb.Append("- Área: ").AppendLine(usuario.Area);
        sb.Append("- Empresa: ").AppendLine(usuario.Empresa);
        sb.Append("- Fecha: ").AppendLine(respuesta.Fecha.ToString("o", CultureInfo.InvariantCulture));
        sb.Append("- Pregunta: ").AppendLine(pregunta.Texto);
        sb.Append("- Tags: ").AppendLine(string.Join(", ", respuesta.TagsSnapshot));
        sb.Append("- Rúbrica / Versión: ").Append(evaluacion.RubricaRef).Append(" / v").Append(evaluacion.VersionRubrica.ToString(CultureInfo.InvariantCulture)).AppendLine();
        sb.Append("- Prompt / Versión: ").Append(evaluacion.PromptRef).Append(" / v").Append(evaluacion.VersionPrompt.ToString(CultureInfo.InvariantCulture)).AppendLine();
        sb.Append("- Calificación total: ").AppendLine(evaluacion.CalificacionTotal.ToString(CultureInfo.InvariantCulture));
        // I-17 (09): nivel de madurez sellado al evaluar; metadato determinista, sin secretos. Regenerable.
        sb.Append("- Nivel de madurez: ").AppendLine(TextoNivelMadurez(respuesta.NivelMadurez));
        sb.AppendLine();
        sb.AppendLine("## Respuesta original");
        sb.AppendLine(respuesta.Texto);
        sb.AppendLine();
        sb.AppendLine("## Evaluación");
        sb.AppendLine("### Calificación por criterio");
        sb.AppendLine("| Criterio | Puntaje | Justificación |");
        sb.AppendLine("|---|---:|---|");
        foreach (var criterio in evaluacion.CalificacionPorCriterio)
        {
            sb.Append("| ").Append(criterio.Criterio)
                .Append(" | ").Append(criterio.Puntaje.ToString(CultureInfo.InvariantCulture))
                .Append(" | ").Append(criterio.Justificacion)
                .AppendLine(" |");
        }

        sb.AppendLine();
        sb.AppendLine("## Retroalimentación enviada");
        sb.AppendLine(evaluacion.RetroalimentacionEnviada);
        sb.AppendLine();
        sb.AppendLine("## Temas identificados");
        foreach (var tema in evaluacion.Temas)
        {
            sb.Append("- ").AppendLine(tema);
        }

        sb.AppendLine();
        sb.AppendLine("## Entidades mencionadas");
        foreach (var entidad in evaluacion.Entidades)
        {
            sb.Append("- ").AppendLine(entidad);
        }

        sb.AppendLine();
        sb.AppendLine("## Notas de trazabilidad");
        sb.Append("- ID de conversación: ").AppendLine(respuesta.ConversacionId);
        sb.Append("- ID de respuesta: ").AppendLine(respuesta.Id);
        sb.Append("- ID de evaluación: ").AppendLine(evaluacion.Id);

        return sb.ToString();
    }

    private static string TextoNivelMadurez(NivelMadurez nivel)
        => nivel == NivelMadurez.Maduro ? "maduro" : "incubacion";

    private static string Requerir(string? valor, string campo)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            throw new ErrorValidacion(
                $"El campo {campo} es obligatorio para compilar.",
                new[] { new DetalleError(campo, "obligatorio") });
        }

        return valor.Trim();
    }
}
