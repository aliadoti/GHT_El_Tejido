using System.Globalization;
using ElTejido.Application.Common;
using ElTejido.Application.Conversacion;
using ElTejido.Application.Markdown;
using ElTejido.Application.Respuestas;
using ElTejido.Application.Usuarios;
using ElTejido.Domain.Conversaciones;
using ElTejido.Domain.Respuestas;
using ElTejido.Domain.Usuarios;
using Microsoft.Extensions.Primitives;
using DominioEvaluacion = ElTejido.Domain.Evaluacion.Evaluacion;

namespace ElTejido.Api.Admin;

/// <summary>
/// Consulta de resultados (04 §5.8, REQ §27.3): conversaciones, respuestas, evaluaciones y Markdown
/// (con descarga `.md` y regeneracion). Lectura para <c>admin</c>/<c>visor</c>; la regeneracion es
/// mutacion (<c>admin</c> + CSRF). Las listas se acotan por <c>campaniaId</c> (particion Cosmos);
/// los demas filtros de §2 se aplican en memoria en el MVP (ver SUPUESTOS).
/// </summary>
internal static class EndpointsAdminResultados
{
    public static IEndpointRouteBuilder MapearEndpointsAdminResultados(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/admin")
            .AddEndpointFilter<AutorizacionAdminEndpointFilter>();

        grupo.MapGet("/conversaciones", ListarConversacionesAsync);
        grupo.MapGet("/conversaciones/{id}", ObtenerConversacionAsync);
        grupo.MapGet("/ideas", ListarIdeasAsync);
        grupo.MapGet("/ideas/{id}", ObtenerIdeaAsync);
        grupo.MapGet("/respuestas", ListarRespuestasAsync);
        grupo.MapGet("/respuestas/{id}", ObtenerRespuestaAsync);
        grupo.MapGet("/evaluaciones", ListarEvaluacionesAsync);
        grupo.MapGet("/evaluaciones/{id}", ObtenerEvaluacionAsync);
        grupo.MapGet("/markdown", ListarMarkdownAsync);
        grupo.MapGet("/markdown/{id}", ObtenerMarkdownAsync);
        grupo.MapGet("/markdown/{id}/raw", DescargarMarkdownAsync);
        grupo.MapPost("/markdown/{id}/regenerar", RegenerarMarkdownAsync);

        return app;
    }

    private static async Task<IResult> ListarConversacionesAsync(HttpContext contexto, CancellationToken ct)
    {
        var query = contexto.Request.Query;
        var campaniaId = RequerirCampania(query);
        var conversaciones = await Conversaciones(contexto).ListarConversacionesAsync(campaniaId, ct);

        var filtradas = conversaciones
            .Where(c => CoincideOpcional(query["usuarioId"], c.UsuarioId) && CoincideOpcional(query["preguntaId"], c.PreguntaId))
            .Select(MapearConversacion)
            .ToArray();

        return Results.Ok(Paginar(filtradas, query));
    }

    private static async Task<IResult> ObtenerConversacionAsync(string id, HttpContext contexto, CancellationToken ct)
    {
        var campaniaId = RequerirCampania(contexto.Request.Query);
        var conversaciones = Conversaciones(contexto);
        var conversacion = await conversaciones.ObtenerConversacionAsync(campaniaId, id, ct)
            ?? throw new ErrorNoEncontrado("La conversacion no existe.");
        var mensajes = await conversaciones.ListarMensajesAsync(campaniaId, id, ct);

        return Results.Ok(new
        {
            conversacion = MapearConversacion(conversacion),
            mensajes = mensajes.Select(MapearMensaje),
        });
    }

    /// <summary>
    /// I-19 (04 §5.8): una fila por idea lógica. P-34 §4.1/§4.2 suma la identidad del participante
    /// resuelta por el servidor, la calificación vigente, los filtros de área/empresa/sede/texto/
    /// fecha/calificación/confirmada y el orden configurable.
    /// <para>
    /// Orden de lectura pensado para no repetir H-10: primero los filtros que solo miran la idea,
    /// luego una consulta de identidad acotada a esos participantes, y el texto o la calificación
    /// <b>solo</b> si algún criterio los necesita antes de paginar. Cuando no hacen falta para
    /// filtrar u ordenar, se resuelven únicamente para la página devuelta.
    /// </para>
    /// </summary>
    private static async Task<IResult> ListarIdeasAsync(HttpContext contexto, CancellationToken ct)
    {
        var query = contexto.Request.Query;
        var campaniaId = RequerirCampania(query);
        var criterios = ConsultaIdeasResultados.Interpretar(LeerCriterios(query));
        var repo = Respuestas(contexto);
        var ideas = await repo.ListarIdeasConsolidadasAsync(campaniaId, ct);

        var candidatas = ideas
            .Where(i => CoincideOpcional(query["usuarioId"], i.UsuarioId)
                && CoincideOpcional(query["preguntaId"], i.PreguntaId)
                && CoincideEnum(query["estadoResultado"], i.EstadoResultado?.ToString())
                && CoincideEnum(query["estadoFlujo"], i.EstadoFlujo.ToString())
                && CoincideEnum(query["estadoCuraduria"], i.EstadoCuraduria?.ToString()))
            .ToArray();

        // P-34 §4.1: la identidad la resuelve el servidor. Es lo que permite filtrar y ordenar por
        // área, empresa o sede sin mentir sobre el `total`, y lo que elimina el join del maestro de
        // usuarios en el navegador (origen de H-01/H-02).
        var participantes = await ParticipantesDeAsync(Usuarios(contexto), candidatas, ct);

        var textos = criterios.NecesitaTexto
            ? TextosDe(candidatas, await VersionesDeAsync(repo, campaniaId, candidatas, ct))
            : SinTextos;
        var evaluaciones = criterios.NecesitaCalificacion
            ? await EvaluacionesDeAsync(repo, campaniaId, candidatas, ct)
            : SinEvaluaciones;

        var filtradas = ConsultaIdeasResultados.FiltrarYOrdenar(
            candidatas, criterios, participantes, textos, CalificacionesDe(evaluaciones));

        var (page, pageSize) = LeerPaginacion(query);
        var pagina = filtradas.Skip((page - 1) * pageSize).Take(pageSize).ToArray();
        var versionesPagina = await VersionesDeAsync(repo, campaniaId, pagina, ct);
        var evaluacionesPagina = criterios.NecesitaCalificacion
            ? evaluaciones
            : await EvaluacionesDeAsync(repo, campaniaId, pagina, ct);

        var resumenes = pagina
            .Select(idea => MapearIdeaResumen(
                idea,
                VersionVigente(idea, versionesPagina),
                participantes.GetValueOrDefault(idea.UsuarioId),
                evaluacionesPagina.GetValueOrDefault(idea.Id)))
            .ToArray();

        return Results.Ok(Envolver(resumenes, page, pageSize, filtradas.Count));
    }

    /// <summary>
    /// I-19: detalle auditable de una idea — versión confirmada vigente, propuesta pendiente si aplica,
    /// evaluación vigente, aportes originales y todas las versiones en orden.
    /// </summary>
    private static async Task<IResult> ObtenerIdeaAsync(string id, HttpContext contexto, CancellationToken ct)
    {
        var campaniaId = RequerirCampania(contexto.Request.Query);
        var repo = Respuestas(contexto);
        var idea = await repo.ObtenerIdeaConsolidadaAsync(campaniaId, id, ct)
            ?? throw new ErrorNoEncontrado("La idea no existe.");

        var confirmada = await VersionAsync(repo, campaniaId, idea.VersionConfirmadaRef, ct);
        var propuesta = await VersionAsync(repo, campaniaId, idea.VersionPropuestaRef, ct);
        var evaluacion = string.IsNullOrWhiteSpace(idea.EvaluacionVigenteRef)
            ? null
            : await repo.ObtenerEvaluacionPorIdAsync(campaniaId, idea.EvaluacionVigenteRef, ct);
        var versiones = (await repo.ListarVersionesIdeaAsync(campaniaId, id, ct))
            .OrderBy(v => v.NumeroVersion)
            .Select(MapearVersionIdea)
            .ToArray();
        // P-34 §6 (H-10): los aportes se piden por `ideaId`; antes se traia la particion completa.
        var aportes = (await repo.ListarRespuestasPorIdeaAsync(campaniaId, id, ct))
            .OrderBy(r => r.Fecha)
            .Select(MapearRespuesta)
            .ToArray();

        // P-34 §4.1: el detalle usa el mismo DTO enriquecido, con la identidad resuelta por el
        // servidor; es una lectura puntual para una sola idea, no un join en el navegador.
        var participante = await Usuarios(contexto).ObtenerUsuarioPorIdAsync(idea.UsuarioId, ct);

        return Results.Ok(new
        {
            idea = MapearIdeaResumen(idea, confirmada ?? propuesta, participante, evaluacion),
            versionConfirmada = confirmada is null ? null : MapearVersionIdea(confirmada),
            versionPropuesta = propuesta is null ? null : MapearVersionIdea(propuesta),
            evaluacion = evaluacion is null ? null : MapearEvaluacion(evaluacion),
            versiones,
            aportes,
        });
    }

    private static readonly IReadOnlyDictionary<string, string> SinTextos =
        new Dictionary<string, string>(StringComparer.Ordinal);

    private static readonly IReadOnlyDictionary<string, DominioEvaluacion> SinEvaluaciones =
        new Dictionary<string, DominioEvaluacion>(StringComparer.Ordinal);

    /// <summary>Traduce la query string a los criterios crudos de P-34 (04 §5.8).</summary>
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

    /// <summary>
    /// P-34 §4.1: identidad de los participantes de un conjunto de ideas, en una consulta acotada por
    /// ids. Un id sin usuario no entra al índice y la fila se presenta como no identificada.
    /// </summary>
    private static async Task<IReadOnlyDictionary<string, Usuario>> ParticipantesDeAsync(
        IRepositorioUsuarios usuarios, IReadOnlyCollection<IdeaConsolidada> ideas, CancellationToken ct)
    {
        var ids = ideas
            .Select(idea => idea.UsuarioId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<string, Usuario>(StringComparer.Ordinal);
        }

        var encontrados = await usuarios.ListarUsuariosPorIdsAsync(ids, ct);
        var indice = new Dictionary<string, Usuario>(encontrados.Count, StringComparer.Ordinal);
        foreach (var usuario in encontrados)
        {
            indice[usuario.Id] = usuario;
        }

        return indice;
    }

    /// <summary>P-34 §4.2: texto vigente por idea, para la búsqueda libre.</summary>
    private static IReadOnlyDictionary<string, string> TextosDe(
        IReadOnlyCollection<IdeaConsolidada> ideas, IReadOnlyDictionary<string, VersionIdeaConsolidada> versiones)
    {
        var textos = new Dictionary<string, string>(ideas.Count, StringComparer.Ordinal);
        foreach (var idea in ideas)
        {
            var texto = VersionVigente(idea, versiones)?.Texto;
            if (!string.IsNullOrWhiteSpace(texto))
            {
                textos[idea.Id] = texto;
            }
        }

        return textos;
    }

    /// <summary>
    /// P-34 §5: evaluación vigente por idea, en una sola consulta por ids (mismo patrón que las
    /// versiones). Una idea sin `evaluacionVigenteRef` no consume nada.
    /// </summary>
    private static async Task<IReadOnlyDictionary<string, DominioEvaluacion>> EvaluacionesDeAsync(
        IRepositorioRespuestas repo, string campaniaId, IReadOnlyCollection<IdeaConsolidada> ideas, CancellationToken ct)
    {
        var referencias = ideas
            .Select(idea => idea.EvaluacionVigenteRef)
            .Where(referencia => !string.IsNullOrWhiteSpace(referencia))
            .Select(referencia => referencia!.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (referencias.Length == 0)
        {
            return SinEvaluaciones;
        }

        var evaluaciones = await repo.ListarEvaluacionesPorIdsAsync(campaniaId, referencias, ct);
        var porId = evaluaciones.ToDictionary(evaluacion => evaluacion.Id, StringComparer.Ordinal);
        var porIdea = new Dictionary<string, DominioEvaluacion>(ideas.Count, StringComparer.Ordinal);
        foreach (var idea in ideas)
        {
            if (!string.IsNullOrWhiteSpace(idea.EvaluacionVigenteRef)
                && porId.TryGetValue(idea.EvaluacionVigenteRef.Trim(), out var evaluacion))
            {
                porIdea[idea.Id] = evaluacion;
            }
        }

        return porIdea;
    }

    private static IReadOnlyDictionary<string, decimal> CalificacionesDe(
        IReadOnlyDictionary<string, DominioEvaluacion> evaluaciones)
        => evaluaciones.ToDictionary(par => par.Key, par => par.Value.CalificacionTotal, StringComparer.Ordinal);

    /// <summary>
    /// P-34 §6 (H-10): una sola consulta con las versiones referidas por las ideas de la página.
    /// Devuelve el índice por id; una referencia sin documento simplemente no aparece y la idea cae a
    /// la propuesta, igual que cuando la lectura puntual devolvía <c>null</c>.
    /// </summary>
    private static async Task<IReadOnlyDictionary<string, VersionIdeaConsolidada>> VersionesDeAsync(
        IRepositorioRespuestas repo, string campaniaId, IReadOnlyCollection<IdeaConsolidada> ideas, CancellationToken ct)
    {
        var referencias = ideas
            .SelectMany(idea => new[] { idea.VersionConfirmadaRef, idea.VersionPropuestaRef })
            .Where(referencia => !string.IsNullOrWhiteSpace(referencia))
            .Select(referencia => referencia!.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (referencias.Length == 0)
        {
            return new Dictionary<string, VersionIdeaConsolidada>(StringComparer.Ordinal);
        }

        var versiones = await repo.ListarVersionesDeCampaniaAsync(campaniaId, referencias, ct);
        var indice = new Dictionary<string, VersionIdeaConsolidada>(versiones.Count, StringComparer.Ordinal);
        foreach (var version in versiones)
        {
            indice[version.Id] = version;
        }

        return indice;
    }

    /// <summary>Confirmada si existe; si no, la propuesta marcada (misma precedencia de I-19).</summary>
    private static VersionIdeaConsolidada? VersionVigente(
        IdeaConsolidada idea, IReadOnlyDictionary<string, VersionIdeaConsolidada> versiones)
        => Version(idea.VersionConfirmadaRef, versiones) ?? Version(idea.VersionPropuestaRef, versiones);

    private static VersionIdeaConsolidada? Version(
        string? versionId, IReadOnlyDictionary<string, VersionIdeaConsolidada> versiones)
        => string.IsNullOrWhiteSpace(versionId) ? null : versiones.GetValueOrDefault(versionId.Trim());

    private static async Task<VersionIdeaConsolidada?> VersionAsync(
        IRepositorioRespuestas repo, string campaniaId, string? versionId, CancellationToken ct)
        => string.IsNullOrWhiteSpace(versionId)
            ? null
            : await repo.ObtenerVersionIdeaAsync(campaniaId, versionId, ct);

    private static async Task<IResult> ListarRespuestasAsync(HttpContext contexto, CancellationToken ct)
    {
        var query = contexto.Request.Query;
        var campaniaId = RequerirCampania(query);
        var respuestas = await Respuestas(contexto).ListarRespuestasAsync(campaniaId, ct);

        var filtradas = respuestas
            .Where(r => CoincideOpcional(query["usuarioId"], r.UsuarioId)
                && CoincideOpcional(query["preguntaId"], r.PreguntaId)
                && CoincideEstadoRespuesta(query["estado"], r.Estado)
                && CoincideNivelMadurez(query["nivelMadurez"], r.NivelMadurez))
            .Select(MapearRespuesta)
            .ToArray();

        return Results.Ok(Paginar(filtradas, query));
    }

    private static async Task<IResult> ObtenerRespuestaAsync(string id, HttpContext contexto, CancellationToken ct)
    {
        var campaniaId = RequerirCampania(contexto.Request.Query);
        var repo = Respuestas(contexto);
        var respuesta = await repo.ObtenerRespuestaAsync(campaniaId, id, ct)
            ?? throw new ErrorNoEncontrado("La respuesta no existe.");
        var evaluacion = await repo.ObtenerEvaluacionPorRespuestaAsync(campaniaId, id, ct);

        return Results.Ok(new
        {
            respuesta = MapearRespuesta(respuesta),
            evaluacion = evaluacion is null ? null : MapearEvaluacion(evaluacion),
        });
    }

    private static async Task<IResult> ObtenerEvaluacionAsync(string id, HttpContext contexto, CancellationToken ct)
    {
        var campaniaId = RequerirCampania(contexto.Request.Query);
        var evaluacion = await Respuestas(contexto).ObtenerEvaluacionPorIdAsync(campaniaId, id, ct)
            ?? throw new ErrorNoEncontrado("La evaluacion no existe.");
        return Results.Ok(MapearEvaluacion(evaluacion));
    }

    /// <summary>
    /// DT-QA-02 (04 §5.8): lista de diagnóstico sin texto libre. La vigencia se deriva con el
    /// mismo orden de I-16 (fecha descendente), sin persistir estados de enlace.
    /// </summary>
    private static async Task<IResult> ListarEvaluacionesAsync(HttpContext contexto, CancellationToken ct)
    {
        var query = contexto.Request.Query;
        var campaniaId = RequerirCampania(query);
        var repo = Respuestas(contexto);
        var evaluaciones = await repo.ListarEvaluacionesAsync(campaniaId, ct);
        var respuestasPorId = (await repo.ListarRespuestasAsync(campaniaId, ct))
            .ToDictionary(respuesta => respuesta.Id, StringComparer.Ordinal);
        var vigentesPorRespuesta = evaluaciones
            .Where(evaluacion => !string.IsNullOrWhiteSpace(evaluacion.RespuestaId))
            .GroupBy(evaluacion => evaluacion.RespuestaId, StringComparer.Ordinal)
            .ToDictionary(
                grupo => grupo.Key,
                grupo => grupo.OrderByDescending(evaluacion => evaluacion.Fecha).First().Id,
                StringComparer.Ordinal);

        var filtradas = evaluaciones
            .OrderByDescending(evaluacion => evaluacion.Fecha)
            .Select(evaluacion => DiagnosticarEnlace(evaluacion, respuestasPorId, vigentesPorRespuesta))
            .Where(evaluacion => CoincideOpcional(query["usuarioId"], evaluacion.Evaluacion.UsuarioId)
                && CoincideOpcional(query["preguntaId"], evaluacion.Evaluacion.PreguntaId)
                && CoincideOpcional(query["respuestaId"], evaluacion.Evaluacion.RespuestaId)
                && CoincideOpcional(query["ideaId"], evaluacion.Evaluacion.IdeaId ?? string.Empty)
                && CoincideEnum(query["recomendacion"], evaluacion.Evaluacion.Recomendacion.ToString())
                && CoincideBooleano(query["anomaliaSeguridad"], evaluacion.Evaluacion.AnomaliaSeguridad)
                && CoincideEnum(query["enlace"], evaluacion.Enlace)
                && CoincideFecha(query["desde"], evaluacion.Evaluacion.Fecha, esDesde: true)
                && CoincideFecha(query["hasta"], evaluacion.Evaluacion.Fecha, esDesde: false))
            .ToArray();

        return Results.Ok(PaginarEvaluaciones(filtradas, query));
    }

    private static async Task<IResult> ListarMarkdownAsync(HttpContext contexto, CancellationToken ct)
    {
        var query = contexto.Request.Query;
        var campaniaId = RequerirCampania(query);
        var artefactos = await Respuestas(contexto).ListarArtefactosAsync(campaniaId, ct);

        var filtrados = artefactos
            .Where(a => CoincideOpcional(query["usuarioId"], a.UsuarioId)
                && CoincideOpcional(query["preguntaId"], a.PreguntaId)
                && CoincideOpcional(query["tipoArtefacto"], a.TipoArtefacto.ToString().ToLowerInvariant()))
            .Select(MapearArtefactoResumen)
            .ToArray();

        return Results.Ok(Paginar(filtrados, query));
    }

    private static async Task<IResult> ObtenerMarkdownAsync(string id, HttpContext contexto, CancellationToken ct)
    {
        var campaniaId = RequerirCampania(contexto.Request.Query);
        var artefacto = await Respuestas(contexto).ObtenerArtefactoAsync(campaniaId, id, ct)
            ?? throw new ErrorNoEncontrado("El artefacto Markdown no existe.");
        return Results.Ok(MapearArtefactoCompleto(artefacto));
    }

    private static async Task<IResult> DescargarMarkdownAsync(string id, HttpContext contexto, CancellationToken ct)
    {
        var campaniaId = RequerirCampania(contexto.Request.Query);
        var artefacto = await Respuestas(contexto).ObtenerArtefactoAsync(campaniaId, id, ct)
            ?? throw new ErrorNoEncontrado("El artefacto Markdown no existe.");
        return Results.Text(artefacto.ContenidoMarkdown, "text/markdown");
    }

    private static async Task<IResult> RegenerarMarkdownAsync(string id, HttpContext contexto, CancellationToken ct)
    {
        var campaniaId = RequerirCampania(contexto.Request.Query);
        var artefacto = await Respuestas(contexto).ObtenerArtefactoAsync(campaniaId, id, ct)
            ?? throw new ErrorNoEncontrado("El artefacto Markdown no existe.");

        var compilado = await contexto.RequestServices.GetRequiredService<ICompiladorMarkdown>().CompilarAsync(
            new SolicitudCompilacion(campaniaId, artefacto.TipoArtefacto, artefacto.RespuestaRef, artefacto.UsuarioId, artefacto.PreguntaId),
            ct);

        return Results.Ok(MapearArtefactoCompleto(compilado));
    }

    private static IRepositorioRespuestas Respuestas(HttpContext contexto)
        => contexto.RequestServices.GetRequiredService<IRepositorioRespuestas>();

    private static IRepositorioConversaciones Conversaciones(HttpContext contexto)
        => contexto.RequestServices.GetRequiredService<IRepositorioConversaciones>();

    private static IRepositorioUsuarios Usuarios(HttpContext contexto)
        => contexto.RequestServices.GetRequiredService<IRepositorioUsuarios>();

    private static object MapearConversacion(Conversacion c)
        => new
        {
            c.Id,
            c.CampaniaId,
            c.UsuarioId,
            c.PreguntaId,
            c.Canal,
            c.Idioma,
            estado = c.Estado.ToString().ToLowerInvariant(),
            estadoMaquina = MinusculaInicial(c.EstadoMaquina.ToString()),
            c.RepreguntasUsadas,
            coachingIdeas = c.CoachingIdeas is null ? null : new
            {
                estado = MinusculaInicial(c.CoachingIdeas.Estado.ToString()),
                c.CoachingIdeas.RespuestaPadreId,
                c.CoachingIdeas.IdeaActivaIndice,
                ideas = c.CoachingIdeas.Ideas.Select(idea => new
                {
                    idea.IdeaIndice,
                    idea.RespuestaRaizId,
                    idea.RespuestaVigenteId,
                    estado = MinusculaInicial(idea.Estado.ToString()),
                    motivoFinalizacion = idea.MotivoFinalizacion is null
                        ? null
                        : MinusculaInicial(idea.MotivoFinalizacion.Value.ToString()),
                    idea.RepreguntasUsadas,
                    idea.IniciadaEn,
                    idea.FinalizadaEn,
                }),
            },
            c.VentanaServicioVenceEn,
            c.FechaInicio,
            c.FechaCierre,
        };

    private static object MapearMensaje(Mensaje m)
        => new
        {
            m.Id,
            direccion = m.Direccion.ToString().ToLowerInvariant(),
            m.Texto,
            m.WhatsappMessageId,
            m.Timestamp,
        };

    private static object MapearIdeaResumen(IdeaConsolidada idea, VersionIdeaConsolidada? vigente)
        => MapearIdeaResumen(idea, vigente, participante: null, evaluacion: null);

    /// <summary>
    /// P-34 §4.1 (04 §5.8): el DTO incorpora la identidad resuelta por el servidor y la calificación
    /// vigente. `participante` viaja siempre —con `resuelto=false` cuando el usuario ya no existe—
    /// para que el cliente nunca tenga que presentar un id técnico como si fuera un nombre, y no
    /// expone número, email ni tags.
    /// </summary>
    private static object MapearIdeaResumen(
        IdeaConsolidada idea,
        VersionIdeaConsolidada? vigente,
        Usuario? participante,
        DominioEvaluacion? evaluacion)
        => new
        {
            participante = new
            {
                usuarioId = idea.UsuarioId,
                codigoUsuarioLegible = participante?.CodigoUsuarioLegible,
                nombre = participante?.Nombre,
                area = participante?.Area,
                empresa = participante?.Empresa,
                sede = participante?.Sede,
                estado = participante is null ? null : MinusculaInicial(participante.Estado.ToString()),
                resuelto = participante is not null,
            },
            calificacionTotal = evaluacion?.CalificacionTotal,
            evaluadaEn = evaluacion?.Fecha,
            idea.Id,
            idea.CampaniaId,
            idea.UsuarioId,
            idea.PreguntaId,
            idea.ConversacionId,
            idea.IdeaIndice,
            idea.RespuestaRaizId,
            texto = vigente?.Texto,
            // Deja explícito si el texto que se muestra todavía no fue confirmado por el participante.
            confirmada = vigente?.EstadoConfirmacion == EstadoConfirmacionVersionIdea.Confirmada,
            estadoFlujo = MinusculaInicial(idea.EstadoFlujo.ToString()),
            estadoResultado = idea.EstadoResultado is null ? null : MinusculaInicial(idea.EstadoResultado.Value.ToString()),
            nivelMadurez = MinusculaInicial(idea.NivelMadurez.ToString()),
            estadoCuraduria = idea.EstadoCuraduria is null ? null : MinusculaInicial(idea.EstadoCuraduria.Value.ToString()),
            idea.MotivoCierre,
            idea.VersionConfirmadaRef,
            idea.VersionPropuestaRef,
            idea.EvaluacionVigenteRef,
            idea.CreadaEn,
            idea.ActualizadaEn,
        };

    private static object MapearVersionIdea(VersionIdeaConsolidada v)
        => new
        {
            v.Id,
            v.IdeaId,
            v.NumeroVersion,
            v.VersionAnteriorId,
            v.Texto,
            estadoConfirmacion = MinusculaInicial(v.EstadoConfirmacion.ToString()),
            origen = MinusculaInicial(v.Origen.ToString()),
            aporteIdsAcumulados = v.AporteIdsAcumulados,
            aporteNuevoIds = v.AporteNuevoIds,
            v.EvaluacionRef,
            v.GeneradaEn,
            v.ConfirmadaEn,
        };

    private static object MapearRespuesta(Respuesta r)
        => new
        {
            r.Id,
            r.CampaniaId,
            r.UsuarioId,
            r.PreguntaId,
            r.ConversacionId,
            r.Texto,
            r.Canal,
            r.EsRepregunta,
            estado = MinusculaInicial(r.Estado.ToString()),
            r.Fecha,
            tagsSnapshot = r.TagsSnapshot,
            r.IdeaIndice,
            r.RespuestaPadreId,
            r.IdeaRaizId,
            r.RespuestaAnteriorId,
            r.RevisionIndice,
            nivelMadurez = MinusculaInicial(r.NivelMadurez.ToString()),
            // I-19 (04 §5.8): enlazan el aporte con su idea logica. Ausentes en datos legacy, que es
            // justo lo que permite a Resultados distinguir un "resultado historico".
            r.IdeaId,
            tipoAporte = r.TipoAporte is null ? null : MinusculaInicial(r.TipoAporte.Value.ToString()),
        };

    private static object MapearEvaluacion(DominioEvaluacion e)
        => new
        {
            e.Id,
            e.CampaniaId,
            e.RespuestaId,
            e.UsuarioId,
            e.PreguntaId,
            e.RubricaRef,
            e.VersionRubrica,
            e.PromptRef,
            e.VersionPrompt,
            configLLMRef = e.ConfigLlmRef,
            configLLMSnapshot = new
            {
                e.ConfigLlmSnapshot.Proveedor,
                e.ConfigLlmSnapshot.Modelo,
                e.ConfigLlmSnapshot.Endpoint,
                e.ConfigLlmSnapshot.Parametros,
            },
            pesosUsados = e.PesosUsados,
            calificacionPorCriterio = e.CalificacionPorCriterio.Select(c => new { c.Criterio, c.Puntaje, c.Justificacion }),
            e.CalificacionTotal,
            e.Explicacion,
            e.RetroalimentacionEnviada,
            e.ParafraseoDevuelto,
            recomendacion = e.Recomendacion.ToString().ToLowerInvariant(),
            e.RepreguntaSugerida,
            temas = e.Temas,
            entidades = e.Entidades,
            e.AnomaliaSeguridad,
            e.Fecha,
        };

    private static object MapearEvaluacionResumen(EvaluacionListada evaluacion)
    {
        var e = evaluacion.Evaluacion;
        return new
        {
            e.Id,
            e.CampaniaId,
            e.RespuestaId,
            e.IdeaId,
            e.VersionIdeaId,
            e.OrigenTextoEvaluado,
            e.UsuarioId,
            e.PreguntaId,
            e.CalificacionTotal,
            recomendacion = e.Recomendacion.ToString().ToLowerInvariant(),
            e.AnomaliaSeguridad,
            e.Fecha,
            enlace = evaluacion.Enlace,
            evaluacion.MotivoDesenlace,
        };
    }

    private static object MapearArtefactoResumen(ArtefactoMarkdown a)
        => new
        {
            a.Id,
            a.CampaniaId,
            tipoArtefacto = a.TipoArtefacto.ToString().ToLowerInvariant(),
            a.UsuarioId,
            a.PreguntaId,
            a.RespuestaRef,
            a.EvaluacionRef,
            a.IdeaRef,
            a.VersionIdeaRef,
            a.BlobPath,
            estado = a.Estado.ToString().ToLowerInvariant(),
            a.Version,
            a.CreadoEn,
            a.ActualizadoEn,
        };

    private static object MapearArtefactoCompleto(ArtefactoMarkdown a)
        => new
        {
            a.Id,
            a.CampaniaId,
            tipoArtefacto = a.TipoArtefacto.ToString().ToLowerInvariant(),
            a.UsuarioId,
            a.PreguntaId,
            a.RespuestaRef,
            a.EvaluacionRef,
            a.IdeaRef,
            a.VersionIdeaRef,
            a.ContenidoMarkdown,
            a.BlobPath,
            estado = a.Estado.ToString().ToLowerInvariant(),
            a.Version,
            a.CreadoEn,
            a.ActualizadoEn,
        };

    private static string RequerirCampania(IQueryCollection query)
    {
        var campaniaId = query["campaniaId"].ToString();
        if (string.IsNullOrWhiteSpace(campaniaId))
        {
            throw new ErrorValidacion(
                "El parametro campaniaId es obligatorio.",
                new[] { new DetalleError("campaniaId", "obligatorio") });
        }

        return campaniaId.Trim();
    }

    private static bool CoincideOpcional(StringValues filtro, string valor)
    {
        var texto = filtro.ToString();
        return string.IsNullOrWhiteSpace(texto) || string.Equals(texto.Trim(), valor, StringComparison.Ordinal);
    }

    private static bool CoincideEstadoRespuesta(StringValues filtro, EstadoRespuesta estado)
    {
        var texto = filtro.ToString();
        return string.IsNullOrWhiteSpace(texto) || string.Equals(texto.Trim(), MinusculaInicial(estado.ToString()), StringComparison.OrdinalIgnoreCase);
    }

    // I-19 (04 §5.8): filtro aditivo por un enum en minuscula inicial; vacio = todas. Un valor nulo en
    // el documento (p. ej. una idea sin estadoResultado) nunca coincide con un filtro explicito.
    private static bool CoincideEnum(StringValues filtro, string? valor)
    {
        var texto = filtro.ToString();
        return string.IsNullOrWhiteSpace(texto)
            || (valor is not null
                && string.Equals(texto.Trim(), MinusculaInicial(valor), StringComparison.OrdinalIgnoreCase));
    }

    // I-17 (04 §5.8): filtro aditivo por nivel de madurez (maduro|incubacion); vacio = todas.
    private static bool CoincideNivelMadurez(StringValues filtro, NivelMadurez nivel)
    {
        var texto = filtro.ToString();
        return string.IsNullOrWhiteSpace(texto) || string.Equals(texto.Trim(), MinusculaInicial(nivel.ToString()), StringComparison.OrdinalIgnoreCase);
    }

    private static bool CoincideBooleano(StringValues filtro, bool valor)
    {
        var texto = filtro.ToString();
        return string.IsNullOrWhiteSpace(texto)
            || (bool.TryParse(texto.Trim(), out var esperado) && esperado == valor);
    }

    private static bool CoincideFecha(StringValues filtro, DateTimeOffset fecha, bool esDesde)
    {
        var texto = filtro.ToString();
        if (string.IsNullOrWhiteSpace(texto))
        {
            return true;
        }

        return DateTimeOffset.TryParse(
                texto.Trim(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var limite)
            && (esDesde ? fecha >= limite : fecha <= limite);
    }

    private static EvaluacionListada DiagnosticarEnlace(
        DominioEvaluacion evaluacion,
        IReadOnlyDictionary<string, Respuesta> respuestasPorId,
        IReadOnlyDictionary<string, string> vigentesPorRespuesta)
    {
        if (string.IsNullOrWhiteSpace(evaluacion.RespuestaId))
        {
            return new EvaluacionListada(evaluacion, "huerfana", "respuesta_id_vacio");
        }

        if (!respuestasPorId.ContainsKey(evaluacion.RespuestaId))
        {
            return new EvaluacionListada(evaluacion, "huerfana", "respuesta_inexistente");
        }

        if (!string.IsNullOrWhiteSpace(evaluacion.IdeaId) && string.IsNullOrWhiteSpace(evaluacion.VersionIdeaId))
        {
            return new EvaluacionListada(evaluacion, "sin_version_idea", "sin_version_idea");
        }

        if (vigentesPorRespuesta.TryGetValue(evaluacion.RespuestaId, out var evaluacionVigenteId)
            && !string.Equals(evaluacion.Id, evaluacionVigenteId, StringComparison.Ordinal))
        {
            return new EvaluacionListada(evaluacion, "superada", "evaluacion_mas_reciente_existe");
        }

        return new EvaluacionListada(evaluacion, "enlazada", null);
    }

    private static object Paginar(IReadOnlyCollection<object> items, IQueryCollection query)
    {
        var (page, pageSize) = LeerPaginacion(query);
        return Envolver(items.Skip((page - 1) * pageSize).Take(pageSize).ToArray(), page, pageSize, items.Count);
    }

    /// <summary>Página pedida y tamaño efectivo (el servidor recorta el máximo, 04 §5.8).</summary>
    private static (int Page, int PageSize) LeerPaginacion(IQueryCollection query)
        => (ParsearEntero(query["page"], 1), Math.Min(ParsearEntero(query["pageSize"], 25), 100));

    /// <summary>
    /// Sobre de paginación: <c>total</c> es siempre el del conjunto filtrado completo, aunque los
    /// elementos ya vengan recortados (P-34 §6 pagina antes de resolver versiones).
    /// </summary>
    private static object Envolver(IReadOnlyCollection<object> items, int page, int pageSize, int total)
        => new
        {
            items,
            page,
            pageSize,
            total,
        };

    private static object PaginarEvaluaciones(IReadOnlyCollection<EvaluacionListada> evaluaciones, IQueryCollection query)
    {
        var page = ParsearEntero(query["page"], 1);
        var pageSize = Math.Min(ParsearEntero(query["pageSize"], 25), 100);
        return new
        {
            resumen = new
            {
                total = evaluaciones.Count,
                enlazadas = evaluaciones.Count(evaluacion => evaluacion.Enlace == "enlazada"),
                huerfanas = evaluaciones.Count(evaluacion => evaluacion.Enlace == "huerfana"),
                superadas = evaluaciones.Count(evaluacion => evaluacion.Enlace == "superada"),
                sinVersionIdea = evaluaciones.Count(evaluacion => evaluacion.Enlace == "sin_version_idea"),
            },
            items = evaluaciones
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(MapearEvaluacionResumen)
                .ToArray(),
            page,
            pageSize,
            total = evaluaciones.Count,
        };
    }

    private static int ParsearEntero(StringValues valor, int porDefecto)
    {
        var texto = valor.ToString();
        return int.TryParse(texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numero) && numero > 0
            ? numero
            : porDefecto;
    }

    private static string MinusculaInicial(string valor)
        => string.IsNullOrEmpty(valor) ? valor : char.ToLowerInvariant(valor[0]) + valor[1..];

    private sealed record EvaluacionListada(
        DominioEvaluacion Evaluacion,
        string Enlace,
        string? MotivoDesenlace);
}
