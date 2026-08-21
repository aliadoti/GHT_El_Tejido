using System.IO.Compression;
using System.Text;
using ElTejido.Api.Auth;
using ElTejido.Application.Auth;
using ElTejido.Application.Campanas;
using ElTejido.Application.Common;
using ElTejido.Application.Exportacion;
using ElTejido.Application.Respuestas;
using ElTejido.Domain.Respuestas;

namespace ElTejido.Api.Admin;

/// <summary>
/// P-34 §4.5 (04 §5.8): exportación de resultados y ZIP de documentos. Son <c>GET</c> bajo el mismo
/// guard admin —lectura para <c>admin</c>/<c>visor</c>—, resuelven el alcance en el servidor con el
/// mismo filtro y el mismo orden que la pantalla, e ignoran <c>page</c>/<c>pageSize</c>: exportar
/// media página sería una trampa.
/// </summary>
internal static class EndpointsAdminExportacion
{
    public static IEndpointRouteBuilder MapearEndpointsAdminExportacion(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/admin")
            .AddEndpointFilter<AutorizacionAdminEndpointFilter>();

        grupo.MapGet("/campanias/{campaniaId}/exportar", ExportarAsync);
        grupo.MapGet("/campanias/{campaniaId}/documentos.zip", ExportarDocumentosAsync);

        return app;
    }

    private static async Task<IResult> ExportarAsync(
        string campaniaId, HttpContext contexto, CancellationToken ct)
    {
        var query = contexto.Request.Query;
        var recurso = ExportacionResultados.LeerRecurso(query["recurso"]);
        var formato = ExportacionResultados.LeerFormato(query["formato"]);
        var anonimizado = ExportacionResultados.LeerAnonimizado(query["anonimizado"]);
        var criterios = ConsultaIdeasResultados.Interpretar(LeerCriterios(query));

        var repo = contexto.RequestServices.GetRequiredService<IRepositorioRespuestas>();
        var alcance = await ConsultaResultadosCompartida.ResolverAsync(contexto, campaniaId, criterios, query, ct);
        ExportacionResultados.VerificarTope(alcance.Ideas.Count);

        var ideaIds = alcance.Ideas.Select(idea => idea.Id).ToArray();
        var versionesPorIdea = (await repo.ListarVersionesDeIdeasAsync(campaniaId, ideaIds, ct))
            .ToLookup(version => version.IdeaId, StringComparer.Ordinal);

        var tabla = recurso switch
        {
            RecursoExportacion.Aportes => ExportacionResultados.ConstruirAportes(
                alcance.Ideas,
                alcance.Participantes,
                await AportesDeAsync(repo, campaniaId, ct),
                versionesPorIdea,
                anonimizado),
            RecursoExportacion.Evaluaciones => ExportacionResultados.ConstruirEvaluaciones(
                alcance.Ideas,
                alcance.Participantes,
                await repo.ListarEvaluacionesAsync(campaniaId, ct) is var evaluaciones ? [.. evaluaciones] : [],
                anonimizado),
            _ => ExportacionResultados.ConstruirIdeas(
                alcance.Ideas,
                alcance.Participantes,
                await EvaluacionesVigentesAsync(repo, campaniaId, alcance, ct),
                versionesPorIdea,
                anonimizado),
        };

        // El tope vuelve a mirarse sobre las filas reales: un recurso de grano fino (aportes) puede
        // multiplicar por varios lo que en ideas cabía de sobra.
        ExportacionResultados.VerificarTope(tabla.Filas.Count);

        var nombreCampania = await NombreCampaniaAsync(contexto, campaniaId, ct);
        var ahora = Ahora(contexto);
        var contenido = new ExportacionTabular(
            ExportacionResultados.ConstruirHojaFiltros(
                nombreCampania,
                recurso,
                anonimizado,
                ConsultaResultadosCompartida.FiltrosAplicados(query),
                tabla.Filas.Count,
                ahora,
                QuienExporta(contexto)),
            tabla);

        var escritor = contexto.RequestServices.GetRequiredService<IEscritorExportacion>();
        var nombreArchivo = ExportacionResultados.NombreArchivo(nombreCampania, recurso, formato, ahora);
        contexto.Response.Headers.ContentDisposition = $"attachment; filename=\"{nombreArchivo}\"";

        if (formato == FormatoExportacion.Csv)
        {
            // El CSV sí se escribe fila por fila sobre la respuesta, con escrituras asíncronas.
            return Results.Stream(
                destino => escritor.EscribirAsync(destino, formato, contenido, ct),
                escritor.ContentType(formato));
        }

        // ClosedXML solo sabe escribir de forma síncrona, y escribir así sobre el socket está
        // prohibido (`AllowSynchronousIO`) además de bloquear un hilo. El libro se arma en un archivo
        // temporal y la respuesta lo envía en modo asíncrono; el archivo se borra al cerrarse.
        var temporal = await ArchivoTemporalAsync(
            destino => escritor.EscribirAsync(destino, formato, contenido, ct));
        return Results.Stream(temporal, escritor.ContentType(formato));
    }

    /// <summary>
    /// P-34 §4.5: un `.md` por idea con documento, con nombre legible. Se escribe entrada por entrada
    /// sobre la respuesta; nunca se arma el ZIP completo en memoria.
    /// </summary>
    private static async Task<IResult> ExportarDocumentosAsync(
        string campaniaId, HttpContext contexto, CancellationToken ct)
    {
        var query = contexto.Request.Query;
        var anonimizado = ExportacionResultados.LeerAnonimizado(query["anonimizado"]);
        var criterios = ConsultaIdeasResultados.Interpretar(LeerCriterios(query));

        var repo = contexto.RequestServices.GetRequiredService<IRepositorioRespuestas>();
        var alcance = await ConsultaResultadosCompartida.ResolverAsync(contexto, campaniaId, criterios, query, ct);
        ExportacionResultados.VerificarTope(alcance.Ideas.Count);

        var artefactos = (await repo.ListarArtefactosAsync(campaniaId, ct))
            .Where(artefacto => !string.IsNullOrWhiteSpace(artefacto.IdeaRef))
            .ToLookup(artefacto => artefacto.IdeaRef!, StringComparer.Ordinal);

        var documentos = new List<(string Nombre, string Contenido)>();
        var usados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var idea in alcance.Ideas)
        {
            var artefacto = artefactos[idea.Id].OrderByDescending(item => item.Version).FirstOrDefault();
            if (artefacto is null)
            {
                continue;
            }

            var nombre = ExportacionResultados.NombreDocumento(
                idea, alcance.Participantes.GetValueOrDefault(idea.UsuarioId), anonimizado);
            // Dos ideas del mismo participante con el mismo índice no pueden pisarse dentro del ZIP.
            if (!usados.Add(nombre))
            {
                nombre = $"{Path.GetFileNameWithoutExtension(nombre)}_{idea.Id}.md";
                usados.Add(nombre);
            }

            documentos.Add((nombre, artefacto.ContenidoMarkdown ?? string.Empty));
        }

        var nombreCampania = await NombreCampaniaAsync(contexto, campaniaId, ct);
        var nombreArchivo = $"{nombreCampania}_documentos_{Ahora(contexto):yyyy-MM-dd}.zip";
        contexto.Response.Headers.ContentDisposition = $"attachment; filename=\"{nombreArchivo}\"";

        // `ZipArchive` cierra su directorio central con escrituras síncronas, que sobre el socket
        // están prohibidas. Se arma en un archivo temporal —una entrada por vez, nunca el ZIP entero
        // en memoria— y la respuesta lo envía en modo asíncrono; el archivo se borra al cerrarse.
        var temporal = await ArchivoTemporalAsync(async destino =>
        {
            using var zip = new ZipArchive(destino, ZipArchiveMode.Create, leaveOpen: true);
            foreach (var (nombre, texto) in documentos)
            {
                ct.ThrowIfCancellationRequested();
                var entrada = zip.CreateEntry(nombre, CompressionLevel.Optimal);
                await using var flujo = entrada.Open();
                await using var escritor = new StreamWriter(flujo, new UTF8Encoding(true));
                await escritor.WriteAsync(texto.AsMemory(), ct);
            }
        });

        return Results.Stream(temporal, "application/zip");
    }

    /// <summary>
    /// Escribe el archivo en disco y lo devuelve listo para enviarse: `DeleteOnClose` lo borra cuando
    /// la respuesta termina de leerlo, incluso si el cliente corta a mitad de la descarga.
    /// </summary>
    private static async Task<Stream> ArchivoTemporalAsync(Func<Stream, Task> escribir)
    {
        var ruta = Path.Combine(Path.GetTempPath(), $"eltejido-exportacion-{Guid.NewGuid():N}");
        try
        {
            await using (var escritura = new FileStream(ruta, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await escribir(escritura);
            }

            return new FileStream(
                ruta,
                FileMode.Open,
                FileAccess.Read,
                FileShare.None,
                bufferSize: 64 * 1024,
                FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        }
        catch
        {
            if (File.Exists(ruta))
            {
                File.Delete(ruta);
            }

            throw;
        }
    }

    /// <summary>Los aportes del recurso «aportes»: una consulta por partición, no una por idea.</summary>
    private static async Task<IReadOnlyList<Respuesta>> AportesDeAsync(
        IRepositorioRespuestas repo, string campaniaId, CancellationToken ct)
        => [.. await repo.ListarRespuestasAsync(campaniaId, ct)];

    private static async Task<IReadOnlyDictionary<string, ElTejido.Domain.Evaluacion.Evaluacion>> EvaluacionesVigentesAsync(
        IRepositorioRespuestas repo,
        string campaniaId,
        ConsultaResultadosCompartida.AlcanceResultados alcance,
        CancellationToken ct)
        => alcance.Evaluaciones.Count > 0
            ? alcance.Evaluaciones
            : await ConsultaResultadosCompartida.EvaluacionesDeAsync(repo, campaniaId, alcance.Ideas, ct);

    private static CriteriosIdeasCrudos LeerCriterios(IQueryCollection query)
        => new(
            Q: query["q"].ToString(),
            Area: query["area"].ToString(),
            Empresa: query["empresa"].ToString(),
            Sede: query["sede"].ToString(),
            Desde: query["desde"].ToString(),
            Hasta: query["hasta"].ToString(),
            CalificacionMin: query["calificacionMin"].ToString(),
            CalificacionMax: query["calificacionMax"].ToString(),
            Confirmada: query["confirmada"].ToString(),
            Orden: query["orden"].ToString(),
            Dir: query["dir"].ToString());

    private static async Task<string> NombreCampaniaAsync(
        HttpContext contexto, string campaniaId, CancellationToken ct)
    {
        var campanias = contexto.RequestServices.GetRequiredService<IRepositorioCampanias>();
        var campania = await campanias.ObtenerCampaniaPorIdAsync(campaniaId, ct)
            ?? throw new ErrorNoEncontrado("La campania no existe.");
        return campania.Nombre;
    }

    /// <summary>Quién pidió el archivo: es la mitad de la auditoría, junto con la fecha (§4.5).</summary>
    private static string QuienExporta(HttpContext contexto)
        => contexto.Items[AutorizacionAdminEndpointFilter.PrincipalItemKey] is PrincipalSesion principal
            ? $"{principal.Nombre} ({principal.UsuarioId})"
            : "sesion no identificada";

    private static DateTimeOffset Ahora(HttpContext contexto)
        => contexto.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
}
