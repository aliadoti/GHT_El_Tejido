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
        if (solicitud.Tipo == TipoArtefactoMarkdown.Idea)
        {
            return await CompilarIdeaAsync(solicitud, cancellationToken);
        }

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

    /// <summary>
    /// I-19 §10: artefacto canónico por <c>ideaId</c>, válido para ideas maduras, pendientes y
    /// rechazadas. Se renderiza desde la versión confirmada vigente (o la propuesta, marcada como no
    /// confirmada) y su evaluación, si existe; nunca desde un aporte suelto. Cada regeneración
    /// incrementa la versión y sobrescribe la misma ruta canónica.
    /// </summary>
    private async Task<ArtefactoMarkdown> CompilarIdeaAsync(
        SolicitudCompilacion solicitud, CancellationToken cancellationToken)
    {
        var campaniaId = Requerir(solicitud.CampaniaId, "campaniaId");
        var ideaId = Requerir(solicitud.IdeaId, "ideaId");

        var idea = await _respuestas.ObtenerIdeaConsolidadaAsync(campaniaId, ideaId, cancellationToken)
            ?? throw new ErrorNoEncontrado("La idea consolidada no existe.");

        var versionVigente = await ObtenerVersionAsync(campaniaId, idea.VersionConfirmadaRef, cancellationToken)
            ?? await ObtenerVersionAsync(campaniaId, idea.VersionPropuestaRef, cancellationToken);
        // La evaluación vigente solo se sella al cerrar la idea; mientras sigue abierta se usa la de la
        // versión exacta que está vigente, nunca la de una versión anterior (I-19 §10).
        var evaluacion = string.IsNullOrWhiteSpace(idea.EvaluacionVigenteRef)
            ? await ObtenerEvaluacionDeLaVersionAsync(campaniaId, idea, versionVigente, cancellationToken)
            : await _respuestas.ObtenerEvaluacionPorIdAsync(campaniaId, idea.EvaluacionVigenteRef, cancellationToken);

        var usuario = await _usuarios.ObtenerUsuarioPorIdAsync(idea.UsuarioId, cancellationToken)
            ?? throw new ErrorNoEncontrado("El usuario de la idea no existe.");
        var campania = await _campanias.ObtenerCampaniaPorIdAsync(campaniaId, cancellationToken)
            ?? throw new ErrorNoEncontrado("La campania no existe.");
        var pregunta = campania.Preguntas.FirstOrDefault(p => p.Id == idea.PreguntaId)
            ?? throw new ErrorNoEncontrado("La pregunta de la idea no existe.");

        var versiones = (await _respuestas.ListarVersionesIdeaAsync(campaniaId, ideaId, cancellationToken))
            .OrderBy(version => version.NumeroVersion)
            .ToArray();
        var aportes = (await _respuestas.ListarRespuestasAsync(campaniaId, cancellationToken))
            .Where(respuesta => respuesta.IdeaId == ideaId)
            .OrderBy(respuesta => respuesta.Fecha)
            .ToArray();

        var contenido = RenderizarIdea(campania, usuario, pregunta, idea, versionVigente, evaluacion, versiones, aportes);
        var blobPath = $"campanias/{campaniaId}/idea/{ideaId}.md";
        var artefactoId = "md_" + ideaId;
        var existente = await _respuestas.ObtenerArtefactoAsync(campaniaId, artefactoId, cancellationToken);
        var ahora = _tiempo.GetUtcNow();

        var artefacto = ArtefactoMarkdown.Crear(
            artefactoId,
            campaniaId,
            TipoArtefactoMarkdown.Idea,
            idea.UsuarioId,
            idea.PreguntaId,
            respuestaRef: null,
            evaluacion?.Id,
            contenido,
            blobPath,
            EstadoArtefacto.Generado,
            (existente?.Version ?? 0) + 1,
            existente?.CreadoEn ?? ahora,
            ahora,
            ideaId,
            versionVigente?.Id);

        await _blob.GuardarTextoAsync(blobPath, artefacto.ContenidoMarkdown, cancellationToken);
        await _respuestas.GuardarArtefactoAsync(artefacto, cancellationToken);
        return artefacto;
    }

    private async Task<DominioEvaluacion?> ObtenerEvaluacionDeLaVersionAsync(
        string campaniaId,
        IdeaConsolidada idea,
        VersionIdeaConsolidada? versionVigente,
        CancellationToken cancellationToken)
    {
        if (versionVigente is null)
        {
            return null;
        }

        var evaluacion = await _respuestas.ObtenerEvaluacionPorRespuestaAsync(
            campaniaId, idea.RespuestaRaizId, cancellationToken);
        return evaluacion?.VersionIdeaId == versionVigente.Id ? evaluacion : null;
    }

    private async Task<VersionIdeaConsolidada?> ObtenerVersionAsync(
        string campaniaId, string? versionId, CancellationToken cancellationToken)
        => string.IsNullOrWhiteSpace(versionId)
            ? null
            : await _respuestas.ObtenerVersionIdeaAsync(campaniaId, versionId, cancellationToken);

    private static string RenderizarIdea(
        Campania campania,
        Usuario usuario,
        Pregunta pregunta,
        IdeaConsolidada idea,
        VersionIdeaConsolidada? versionVigente,
        DominioEvaluacion? evaluacion,
        IReadOnlyList<VersionIdeaConsolidada> versiones,
        IReadOnlyList<Respuesta> aportes)
    {
        var sb = new StringBuilder();
        sb.Append("# Idea de ").AppendLine(usuario.Nombre);
        sb.AppendLine();
        sb.AppendLine("## Metadatos");
        sb.Append("- Campaña: ").AppendLine(campania.Nombre);
        sb.Append("- Participante: ").AppendLine(usuario.Nombre);
        sb.Append("- Área: ").AppendLine(usuario.Area);
        sb.Append("- Empresa: ").AppendLine(usuario.Empresa);
        sb.Append("- Fecha: ").AppendLine(idea.ActualizadaEn.ToString("o", CultureInfo.InvariantCulture));
        sb.Append("- Pregunta: ").AppendLine(pregunta.Texto);
        sb.Append("- Tags: ").AppendLine(string.Join(", ", aportes.FirstOrDefault()?.TagsSnapshot ?? Array.Empty<string>()));
        sb.Append("- Estado de la idea: ").AppendLine(
            idea.EstadoResultado is null ? "en curso" : MinusculaInicial(idea.EstadoResultado.Value.ToString()));
        sb.Append("- Estado del flujo: ").AppendLine(MinusculaInicial(idea.EstadoFlujo.ToString()));
        sb.Append("- Nivel de madurez: ").AppendLine(TextoNivelMadurez(idea.NivelMadurez));
        sb.Append("- Estado de curaduría: ").AppendLine(
            idea.EstadoCuraduria is null ? "no aplica" : MinusculaInicial(idea.EstadoCuraduria.Value.ToString()));
        sb.Append("- Motivo de cierre: ").AppendLine(idea.MotivoCierre ?? "no aplica");
        sb.Append("- Confirmación de la versión vigente: ").AppendLine(
            versionVigente is null ? "sin versión" : MinusculaInicial(versionVigente.EstadoConfirmacion.ToString()));
        if (evaluacion is not null)
        {
            sb.Append("- Rúbrica / Versión: ").Append(evaluacion.RubricaRef).Append(" / v")
                .Append(evaluacion.VersionRubrica.ToString(CultureInfo.InvariantCulture)).AppendLine();
            sb.Append("- Prompt / Versión: ").Append(evaluacion.PromptRef).Append(" / v")
                .Append(evaluacion.VersionPrompt.ToString(CultureInfo.InvariantCulture)).AppendLine();
            sb.Append("- Calificación total: ").AppendLine(
                evaluacion.CalificacionTotal.ToString(CultureInfo.InvariantCulture));
        }

        sb.AppendLine();
        sb.AppendLine("## Idea consolidada");
        sb.AppendLine(versionVigente?.Texto ?? "(sin versión consolidada)");

        sb.AppendLine();
        sb.AppendLine("## Aportes originales");
        foreach (var aporte in aportes)
        {
            sb.Append("- ").Append(aporte.TipoAporte is null ? "aporte" : MinusculaInicial(aporte.TipoAporte.Value.ToString()))
                .Append(": ").AppendLine(aporte.Texto);
        }

        sb.AppendLine();
        sb.AppendLine("## Historial de versiones");
        foreach (var version in versiones)
        {
            sb.Append("- v").Append(version.NumeroVersion.ToString(CultureInfo.InvariantCulture))
                .Append(" (").Append(MinusculaInicial(version.EstadoConfirmacion.ToString())).Append("): ")
                .AppendLine(version.Texto);
        }

        if (evaluacion is not null)
        {
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
        }

        sb.AppendLine();
        sb.AppendLine("## Notas de trazabilidad");
        sb.Append("- ID de conversación: ").AppendLine(idea.ConversacionId);
        sb.Append("- ID de idea: ").AppendLine(idea.Id);
        sb.Append("- ID de versión vigente: ").AppendLine(versionVigente?.Id ?? "sin versión");
        sb.Append("- IDs de aportes: ").AppendLine(string.Join(", ", aportes.Select(aporte => aporte.Id)));
        sb.Append("- ID de evaluación: ").AppendLine(evaluacion?.Id ?? "sin evaluación");

        return sb.ToString();
    }

    private static string MinusculaInicial(string valor) => char.ToLowerInvariant(valor[0]) + valor[1..];

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
        if (respuesta.IdeaRaizId is not null)
        {
            sb.Append("- Idea raiz: ").AppendLine(respuesta.IdeaRaizId);
            sb.Append("- Revision: ").AppendLine((respuesta.RevisionIndice ?? 0).ToString(CultureInfo.InvariantCulture));
        }
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
