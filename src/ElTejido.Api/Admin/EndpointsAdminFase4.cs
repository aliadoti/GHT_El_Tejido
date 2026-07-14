using ElTejido.Application.Campanas;
using ElTejido.Application.Common;
using ElTejido.Application.Configuracion;
using ElTejido.Application.Reinicio;
using ElTejido.Domain.Campanas;
using ElTejido.Domain.Common;
using ElTejido.Domain.Configuracion;
using ElTejido.Domain.Participantes;
using Microsoft.Extensions.Primitives;

namespace ElTejido.Api.Admin;

internal static class EndpointsAdminFase4
{
    private const int PaginaPorDefecto = 1;
    private const int TamanoPaginaPorDefecto = 25;
    private const int TamanoPaginaMaximo = 100;

    public static IEndpointRouteBuilder MapearEndpointsAdminFase4(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/admin")
            .AddEndpointFilter<AutorizacionAdminEndpointFilter>();

        var campanias = grupo.MapGroup("/campanias");
        campanias.MapGet("", ListarCampaniasAsync);
        campanias.MapPost("", CrearCampaniaAsync);
        campanias.MapGet("/{id}", ObtenerCampaniaAsync);
        campanias.MapPut("/{id}", ActualizarCampaniaAsync);
        campanias.MapPatch("/{id}/estado", CambiarEstadoCampaniaAsync);
        campanias.MapPost("/{id}/duplicar", DuplicarCampaniaAsync);
        campanias.MapGet("/{id}/mensajes-iniciales", ListarMensajesInicialesAsync);
        campanias.MapPost("/{id}/mensajes-iniciales", CrearMensajeInicialAsync);
        campanias.MapPut("/{id}/mensajes-iniciales/{miId}", ActualizarMensajeInicialAsync);
        campanias.MapDelete("/{id}/mensajes-iniciales/{miId}", EliminarMensajeInicialAsync);
        campanias.MapGet("/{id}/preguntas", ListarPreguntasAsync);
        campanias.MapPost("/{id}/preguntas", CrearPreguntaAsync);
        campanias.MapPut("/{id}/preguntas/{preguntaId}", ActualizarPreguntaAsync);
        campanias.MapDelete("/{id}/preguntas/{preguntaId}", EliminarPreguntaAsync);
        campanias.MapGet("/{id}/participantes", ListarParticipantesAsync);
        campanias.MapPost("/{id}/participantes", AsociarParticipantesAsync);
        campanias.MapDelete("/{id}/participantes/{usuarioId}", DesasociarParticipanteAsync);
        campanias.MapGet("/{id}/participantes/preview", PreviewParticipantesGetAsync);
        campanias.MapPost("/{id}/participantes/preview", PreviewParticipantesPostAsync);
        // P-03: reinicio de datos de prueba (borra flujo, conserva campania/config/usuarios).
        campanias.MapPost("/{id}/participantes/{usuarioId}/reiniciar", ReiniciarParticipanteAsync);
        campanias.MapPost("/{id}/reiniciar-datos", ReiniciarDatosCampaniaAsync);

        var rubricas = grupo.MapGroup("/rubricas");
        rubricas.MapGet("", ListarRubricasAsync);
        rubricas.MapPost("", CrearRubricaAsync);
        rubricas.MapGet("/{id}", ObtenerRubricaAsync);
        rubricas.MapPut("/{id}", ActualizarRubricaAsync);
        rubricas.MapGet("/{id}/versiones", VersionesRubricaAsync);
        rubricas.MapPost("/{id}/versiones", CrearVersionRubricaAsync);
        rubricas.MapPatch("/{id}/estado", CambiarEstadoRubricaAsync);

        var prompts = grupo.MapGroup("/prompts");
        prompts.MapGet("", ListarPromptsAsync);
        prompts.MapPost("", CrearPromptAsync);
        prompts.MapGet("/{id}", ObtenerPromptAsync);
        prompts.MapPut("/{id}", ActualizarPromptAsync);
        prompts.MapGet("/{id}/versiones", VersionesPromptAsync);
        prompts.MapPost("/{id}/versiones", CrearVersionPromptAsync);
        prompts.MapPost("/{id}/aprobar", AprobarPromptAsync);
        prompts.MapPatch("/{id}/estado", CambiarEstadoPromptAsync);

        var configs = grupo.MapGroup("/config-llm");
        configs.MapGet("", ListarConfigsLlmAsync);
        configs.MapPost("", CrearConfigLlmAsync);
        configs.MapGet("/{id}", ObtenerConfigLlmAsync);
        configs.MapPut("/{id}", ActualizarConfigLlmAsync);
        configs.MapPatch("/{id}/estado", CambiarEstadoConfigLlmAsync);

        return app;
    }

    private static async Task<IResult> ListarCampaniasAsync(HttpContext contexto, CancellationToken ct)
    {
        var query = contexto.Request.Query;
        var campanias = await ServicioCampanias(contexto).BuscarCampaniasAsync(
            new FiltroCampanias(ParseEstadoCampaniaOpcional(query["estado"]), query["q"].ToString()),
            ct);
        return Results.Ok(Paginar(campanias.Select(MapearCampaniaResumen).ToArray(), query["page"], query["pageSize"]));
    }

    private static async Task<IResult> CrearCampaniaAsync(CampaniaRequest request, HttpContext contexto, CancellationToken ct)
    {
        var campania = await ServicioCampanias(contexto).CrearCampaniaAsync(ToSolicitudCampania(request), ct);
        return Results.Created($"/api/admin/campanias/{campania.Id}", MapearCampania(campania));
    }

    private static async Task<IResult> ObtenerCampaniaAsync(string id, HttpContext contexto, CancellationToken ct)
        => Results.Ok(MapearCampania(await ServicioCampanias(contexto).ObtenerCampaniaAsync(id, ct)));

    private static async Task<IResult> ActualizarCampaniaAsync(
        string id,
        CampaniaPatchRequest request,
        HttpContext contexto,
        CancellationToken ct)
    {
        var campania = await ServicioCampanias(contexto).ActualizarCampaniaAsync(
            id,
            new SolicitudActualizarCampania(
                request.Nombre,
                request.Descripcion,
                request.Objetivo,
                request.RubricaRef,
                request.PromptRefs,
                request.ConfigLlmRef,
                request.ConfigMarkdown is null ? null : ToConfigMarkdown(request.ConfigMarkdown),
                request.ConfigConversacional is null ? null : ToConfigConversacional(request.ConfigConversacional),
                request.ConfigSeguridad is null ? null : ToLimitesCampania(request.ConfigSeguridad)),
            ct);
        return Results.Ok(MapearCampania(campania));
    }

    private static async Task<IResult> CambiarEstadoCampaniaAsync(
        string id,
        CambiarEstadoRequest request,
        HttpContext contexto,
        CancellationToken ct)
        => Results.Ok(MapearCampania(await ServicioCampanias(contexto).CambiarEstadoCampaniaAsync(id, ParseEstadoCampania(request.Estado), ct)));

    private static async Task<IResult> DuplicarCampaniaAsync(string id, HttpContext contexto, CancellationToken ct)
    {
        var campania = await ServicioCampanias(contexto).DuplicarCampaniaAsync(id, ct);
        return Results.Created($"/api/admin/campanias/{campania.Id}", MapearCampania(campania));
    }

    private static async Task<IResult> ListarMensajesInicialesAsync(string id, HttpContext contexto, CancellationToken ct)
        => Results.Ok((await ServicioCampanias(contexto).ObtenerCampaniaAsync(id, ct)).MensajesIniciales.Select(MapearMensajeInicial));

    private static async Task<IResult> CrearMensajeInicialAsync(
        string id,
        MensajeInicialRequest request,
        HttpContext contexto,
        CancellationToken ct)
    {
        var mensaje = await ServicioCampanias(contexto).AgregarMensajeInicialAsync(id, ToSolicitudMensaje(request), ct);
        return Results.Created($"/api/admin/campanias/{id}/mensajes-iniciales/{mensaje.Id}", MapearMensajeInicial(mensaje));
    }

    private static async Task<IResult> ActualizarMensajeInicialAsync(
        string id,
        string miId,
        MensajeInicialPatchRequest request,
        HttpContext contexto,
        CancellationToken ct)
        => Results.Ok(MapearMensajeInicial(await ServicioCampanias(contexto).ActualizarMensajeInicialAsync(
            id,
            miId,
            new SolicitudActualizarMensajeInicial(
                request.NombreInterno,
                request.Texto,
                request.Orden,
                request.VariablesDinamicas,
                ParseEstadoRegistroOpcional(request.Estado, "estado"),
                request.PlantillaWhatsApp is null ? null : ToPlantilla(request.PlantillaWhatsApp)),
            ct)));

    private static async Task<IResult> EliminarMensajeInicialAsync(string id, string miId, HttpContext contexto, CancellationToken ct)
    {
        await ServicioCampanias(contexto).EliminarMensajeInicialAsync(id, miId, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> ListarPreguntasAsync(string id, HttpContext contexto, CancellationToken ct)
        => Results.Ok((await ServicioCampanias(contexto).ObtenerCampaniaAsync(id, ct)).Preguntas.Select(MapearPregunta));

    private static async Task<IResult> CrearPreguntaAsync(
        string id,
        PreguntaRequest request,
        HttpContext contexto,
        CancellationToken ct)
    {
        var pregunta = await ServicioCampanias(contexto).AgregarPreguntaAsync(id, ToSolicitudPregunta(request), ct);
        return Results.Created($"/api/admin/campanias/{id}/preguntas/{pregunta.Id}", MapearPregunta(pregunta));
    }

    private static async Task<IResult> ActualizarPreguntaAsync(
        string id,
        string preguntaId,
        PreguntaPatchRequest request,
        HttpContext contexto,
        CancellationToken ct)
        => Results.Ok(MapearPregunta(await ServicioCampanias(contexto).ActualizarPreguntaAsync(
            id,
            preguntaId,
            new SolicitudActualizarPregunta(
                request.Texto,
                request.Instruccion,
                request.Categoria,
                request.Orden,
                ParseEstadoRegistroOpcional(request.Estado, "estado"),
                request.RubricaRef,
                request.VersionRubrica,
                request.PromptRefs,
                request.MaxRepreguntas,
                request.LimitesSeguridad is null ? null : ToLimitesPregunta(request.LimitesSeguridad),
                request.ConfigMarkdown is null ? null : ToConfigMarkdown(request.ConfigMarkdown)),
            ct)));

    private static async Task<IResult> EliminarPreguntaAsync(string id, string preguntaId, HttpContext contexto, CancellationToken ct)
    {
        await ServicioCampanias(contexto).EliminarPreguntaAsync(id, preguntaId, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> ListarParticipantesAsync(string id, HttpContext contexto, CancellationToken ct)
        => Results.Ok((await ServicioCampanias(contexto).ListarParticipantesAsync(id, ct)).Select(MapearParticipante));

    private static async Task<IResult> AsociarParticipantesAsync(
        string id,
        AsociarParticipantesRequest request,
        HttpContext contexto,
        CancellationToken ct)
        => Results.Ok((await ServicioCampanias(contexto).AsociarParticipantesAsync(
            id,
            new SolicitudAsociarParticipantes(request.UsuarioIds, ToFiltroParticipantes(request.Filtro)),
            ct)).Select(MapearParticipante));

    private static async Task<IResult> DesasociarParticipanteAsync(string id, string usuarioId, HttpContext contexto, CancellationToken ct)
    {
        await ServicioCampanias(contexto).DesasociarParticipanteAsync(id, usuarioId, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> PreviewParticipantesGetAsync(
        string id,
        HttpContext contexto,
        CancellationToken ct)
    {
        _ = id;
        var query = contexto.Request.Query;
        var preview = await ServicioCampanias(contexto).PreviewParticipantesAsync(
            new SolicitudFiltroParticipantes(query["area"].ToString(), query["empresa"].ToString(), ParseTags(query["tag"], query["tags"]), query["q"].ToString()),
            ct);
        return Results.Ok(new { total = preview.Count, items = preview });
    }

    private static async Task<IResult> PreviewParticipantesPostAsync(
        string id,
        FiltroParticipantesRequest request,
        HttpContext contexto,
        CancellationToken ct)
    {
        _ = id;
        var preview = await ServicioCampanias(contexto).PreviewParticipantesAsync(ToFiltroParticipantes(request)!, ct);
        return Results.Ok(new { total = preview.Count, items = preview });
    }

    private static async Task<IResult> ListarRubricasAsync(HttpContext contexto, CancellationToken ct)
    {
        var query = contexto.Request.Query;
        var rubricas = await ServicioConfig(contexto).BuscarRubricasAsync(ParseEstadoRubricaOpcional(query["estado"]), ct);
        return Results.Ok(Paginar(rubricas.Select(MapearRubrica).ToArray(), query["page"], query["pageSize"]));
    }

    private static async Task<IResult> CrearRubricaAsync(RubricaRequest request, HttpContext contexto, CancellationToken ct)
    {
        var rubrica = await ServicioConfig(contexto).CrearRubricaAsync(ToSolicitudRubrica(request), ct);
        return Results.Created($"/api/admin/rubricas/{rubrica.Id}", MapearRubrica(rubrica));
    }

    private static async Task<IResult> ObtenerRubricaAsync(string id, HttpContext contexto, CancellationToken ct)
        => Results.Ok(MapearRubrica(await ServicioConfig(contexto).ObtenerRubricaAsync(id, ct)));

    private static async Task<IResult> VersionesRubricaAsync(string id, HttpContext contexto, CancellationToken ct)
        => Results.Ok((await ServicioConfig(contexto).ListarVersionesRubricaAsync(id, ct)).Select(MapearRubrica));

    private static async Task<IResult> ActualizarRubricaAsync(
        string id,
        RubricaRequest request,
        HttpContext contexto,
        CancellationToken ct)
        => Results.Ok(MapearRubrica(await ServicioConfig(contexto).ActualizarRubricaAsync(id, ToSolicitudRubrica(request with { Id = id }), ct)));

    private static async Task<IResult> CrearVersionRubricaAsync(
        string id,
        RubricaRequest request,
        HttpContext contexto,
        CancellationToken ct)
        => Results.Created($"/api/admin/rubricas/{id}", MapearRubrica(await ServicioConfig(contexto).CrearVersionRubricaAsync(id, ToSolicitudRubrica(request), ct)));

    private static async Task<IResult> CambiarEstadoRubricaAsync(string id, CambiarEstadoRequest request, HttpContext contexto, CancellationToken ct)
        => Results.Ok(MapearRubrica(await ServicioConfig(contexto).CambiarEstadoRubricaAsync(id, ParseEstadoRubrica(request.Estado), ct)));

    private static async Task<IResult> ListarPromptsAsync(HttpContext contexto, CancellationToken ct)
    {
        var query = contexto.Request.Query;
        var prompts = await ServicioConfig(contexto).BuscarPromptsAsync(query["tipoPrompt"].ToString(), ParseEstadoPromptOpcional(query["estado"]), ct);
        return Results.Ok(Paginar(prompts.Select(MapearPrompt).ToArray(), query["page"], query["pageSize"]));
    }

    private static async Task<IResult> CrearPromptAsync(PromptRequest request, HttpContext contexto, CancellationToken ct)
    {
        var prompt = await ServicioConfig(contexto).CrearPromptAsync(ToSolicitudPrompt(request), ct);
        return Results.Created($"/api/admin/prompts/{prompt.Id}", MapearPrompt(prompt));
    }

    private static async Task<IResult> ObtenerPromptAsync(string id, HttpContext contexto, CancellationToken ct)
        => Results.Ok(MapearPrompt(await ServicioConfig(contexto).ObtenerPromptAsync(id, ct)));

    private static async Task<IResult> VersionesPromptAsync(string id, HttpContext contexto, CancellationToken ct)
        => Results.Ok((await ServicioConfig(contexto).ListarVersionesPromptAsync(id, ct)).Select(MapearPrompt));

    private static async Task<IResult> ActualizarPromptAsync(string id, PromptRequest request, HttpContext contexto, CancellationToken ct)
        => Results.Ok(MapearPrompt(await ServicioConfig(contexto).ActualizarPromptAsync(id, ToSolicitudPrompt(request with { Id = id }), ct)));

    private static async Task<IResult> CrearVersionPromptAsync(string id, PromptRequest request, HttpContext contexto, CancellationToken ct)
        => Results.Created($"/api/admin/prompts/{id}", MapearPrompt(await ServicioConfig(contexto).CrearVersionPromptAsync(id, ToSolicitudPrompt(request), ct)));

    private static async Task<IResult> AprobarPromptAsync(
        string id,
        AprobarPromptRequest? request,
        HttpContext contexto,
        CancellationToken ct)
        => Results.Ok(MapearPrompt(await ServicioConfig(contexto).AprobarPromptAsync(id, request?.AprobadoPor ?? "admin", ct)));

    private static async Task<IResult> CambiarEstadoPromptAsync(string id, CambiarEstadoRequest request, HttpContext contexto, CancellationToken ct)
        => Results.Ok(MapearPrompt(await ServicioConfig(contexto).CambiarEstadoPromptAsync(id, ParseEstadoPrompt(request.Estado), ct)));

    private static async Task<IResult> ListarConfigsLlmAsync(HttpContext contexto, CancellationToken ct)
    {
        var query = contexto.Request.Query;
        var configs = await ServicioConfig(contexto).BuscarConfigsLlmAsync(ParseEstadoRegistroOpcional(query["estado"], "estado"), ct);
        return Results.Ok(Paginar(configs.Select(MapearConfigLlm).ToArray(), query["page"], query["pageSize"]));
    }

    private static async Task<IResult> CrearConfigLlmAsync(ConfigLlmRequest request, HttpContext contexto, CancellationToken ct)
    {
        var config = await ServicioConfig(contexto).CrearConfigLlmAsync(ToSolicitudConfigLlm(request), ct);
        return Results.Created($"/api/admin/config-llm/{config.Id}", MapearConfigLlm(config));
    }

    private static async Task<IResult> ObtenerConfigLlmAsync(string id, HttpContext contexto, CancellationToken ct)
        => Results.Ok(MapearConfigLlm(await ServicioConfig(contexto).ObtenerConfigLlmAsync(id, ct)));

    private static async Task<IResult> ActualizarConfigLlmAsync(
        string id,
        ConfigLlmPatchRequest request,
        HttpContext contexto,
        CancellationToken ct)
        => Results.Ok(MapearConfigLlm(await ServicioConfig(contexto).ActualizarConfigLlmAsync(id, ToSolicitudConfigLlmPatch(request), ct)));

    private static async Task<IResult> CambiarEstadoConfigLlmAsync(string id, CambiarEstadoRequest request, HttpContext contexto, CancellationToken ct)
        => Results.Ok(MapearConfigLlm(await ServicioConfig(contexto).CambiarEstadoConfigLlmAsync(id, ParseEstadoRegistro(request.Estado, "estado"), ct)));

    // P-03 — reinicio de datos del flujo. El borrado por participante queda gateado solo por el guard
    // admin + CSRF; el masivo, ademas, por el flag operativo Seguridad:PermitirReinicioDatos (default
    // true; se apaga en el acta del freeze para el dia-D). Ambos devuelven el reporte de conteos.
    private static async Task<IResult> ReiniciarParticipanteAsync(
        string id,
        string usuarioId,
        ReiniciarParticipanteRequest? request,
        HttpContext contexto,
        CancellationToken ct)
    {
        var reporte = await ServicioReinicio(contexto).ReiniciarParticipanteAsync(id, usuarioId, request?.ReiniciarEnvios ?? false, ct);
        return Results.Ok(MapearReporteReinicio(reporte));
    }

    private static async Task<IResult> ReiniciarDatosCampaniaAsync(
        string id,
        ReiniciarCampaniaRequest? request,
        HttpContext contexto,
        CancellationToken ct)
    {
        if (!PermitirReinicioDatos(contexto))
        {
            throw new ErrorConflicto("El reinicio masivo de datos esta deshabilitado (Seguridad:PermitirReinicioDatos).");
        }

        var reporte = await ServicioReinicio(contexto).ReiniciarCampaniaAsync(id, request?.UsuarioIds, request?.ReiniciarEnvios ?? false, ct);
        return Results.Ok(MapearReporteReinicio(reporte));
    }

    private static bool PermitirReinicioDatos(HttpContext contexto)
        => contexto.RequestServices.GetRequiredService<IConfiguration>().GetValue("Seguridad:PermitirReinicioDatos", true);

    private static object MapearReporteReinicio(ReporteReinicioDatos reporte)
        => new
        {
            reporte.Conversaciones,
            reporte.Mensajes,
            reporte.Respuestas,
            reporte.Evaluaciones,
            reporte.Artefactos,
            reporte.BlobsBorrados,
            reporte.BlobsFallidos,
            reporte.ParticipantesReseteados,
        };

    private static IServicioReinicioDatos ServicioReinicio(HttpContext contexto)
        => contexto.RequestServices.GetRequiredService<IServicioReinicioDatos>();

    private static IServicioGestionCampanias ServicioCampanias(HttpContext contexto)
        => contexto.RequestServices.GetRequiredService<IServicioGestionCampanias>();

    private static IServicioGestionConfiguracion ServicioConfig(HttpContext contexto)
        => contexto.RequestServices.GetRequiredService<IServicioGestionConfiguracion>();

    private static SolicitudGuardarCampania ToSolicitudCampania(CampaniaRequest request)
        => new(
            RequerirTexto(request.Nombre, "nombre"),
            RequerirTexto(request.Descripcion, "descripcion"),
            RequerirTexto(request.Objetivo, "objetivo"),
            RequerirTexto(request.RubricaRef, "rubricaRef"),
            request.PromptRefs,
            RequerirTexto(request.ConfigLlmRef, "configLLMRef"),
            ToConfigMarkdown(request.ConfigMarkdown),
            ToConfigConversacional(request.ConfigConversacional),
            ToLimitesCampania(request.ConfigSeguridad));

    private static SolicitudGuardarMensajeInicial ToSolicitudMensaje(MensajeInicialRequest request)
        => new(
            RequerirTexto(request.NombreInterno, "nombreInterno"),
            RequerirTexto(request.Texto, "texto"),
            request.Orden ?? 1,
            request.VariablesDinamicas,
            ParseEstadoRegistroOpcional(request.Estado, "estado") ?? EstadoRegistro.Activo,
            request.PlantillaWhatsApp is null ? null : ToPlantilla(request.PlantillaWhatsApp));

    private static SolicitudGuardarPregunta ToSolicitudPregunta(PreguntaRequest request)
        => new(
            RequerirTexto(request.Texto, "texto"),
            RequerirTexto(request.Instruccion, "instruccion"),
            RequerirTexto(request.Categoria, "categoria"),
            request.Orden ?? 1,
            ParseEstadoRegistroOpcional(request.Estado, "estado") ?? EstadoRegistro.Activo,
            request.RubricaRef,
            request.VersionRubrica,
            request.PromptRefs,
            request.MaxRepreguntas ?? 1,
            ToLimitesPregunta(request.LimitesSeguridad),
            ToConfigMarkdown(request.ConfigMarkdown));

    private static SolicitudGuardarRubrica ToSolicitudRubrica(RubricaRequest request)
        => new(
            RequerirTexto(request.Id, "id"),
            RequerirTexto(request.Nombre, "nombre"),
            RequerirTexto(request.Descripcion, "descripcion"),
            RequerirTexto(request.ContenidoMarkdown, "contenidoMarkdown"),
            EscalaRubrica.Crear(request.Escala?.Min ?? 1, request.Escala?.Max ?? 5),
            (request.Criterios ?? Array.Empty<CriterioRequest>()).Select(c => CriterioRubrica.Crear(RequerirTexto(c.Nombre, "criterio.nombre"), c.Peso)),
            ParseEstadoRubricaOpcional(request.Estado) ?? EstadoRubrica.Activa);

    private static SolicitudGuardarPrompt ToSolicitudPrompt(PromptRequest request)
        => new(
            RequerirTexto(request.Id, "id"),
            RequerirTexto(request.Nombre, "nombre"),
            RequerirTexto(request.TipoPrompt, "tipoPrompt"),
            RequerirTexto(request.Contenido, "contenido"),
            ParseEstadoPromptOpcional(request.Estado) ?? EstadoPrompt.Borrador);

    private static SolicitudGuardarConfigLlm ToSolicitudConfigLlm(ConfigLlmRequest request)
        => new(
            RequerirTexto(request.Nombre, "nombre"),
            RequerirTexto(request.Proveedor, "proveedor"),
            RequerirTexto(request.Modelo, "modelo"),
            RequerirTexto(request.Endpoint, "endpoint"),
            RequerirTexto(request.ApiKeyRef, "apiKeyRef"),
            request.Parametros,
            LimitesTokensLlm.Crear(request.LimitesTokens?.MaxPrompt ?? 6000, request.LimitesTokens?.MaxCompletion ?? 800),
            request.TimeoutSegundos ?? 30,
            request.MaxReintentos ?? 2,
            ParseEstadoRegistroOpcional(request.Estado, "estado") ?? EstadoRegistro.Activo);

    private static SolicitudActualizarConfigLlm ToSolicitudConfigLlmPatch(ConfigLlmPatchRequest request)
        => new(
            request.Nombre,
            request.Proveedor,
            request.Modelo,
            request.Endpoint,
            request.ApiKeyRef,
            request.Parametros,
            request.LimitesTokens is null ? null : LimitesTokensLlm.Crear(request.LimitesTokens.MaxPrompt, request.LimitesTokens.MaxCompletion),
            request.TimeoutSegundos,
            request.MaxReintentos,
            ParseEstadoRegistroOpcional(request.Estado, "estado"));

    private static SolicitudFiltroParticipantes? ToFiltroParticipantes(FiltroParticipantesRequest? request)
        => request is null ? null : new SolicitudFiltroParticipantes(request.Area, request.Empresa, request.Tags, request.Busqueda);

    private static ConfigMarkdown ToConfigMarkdown(ConfigMarkdownRequest? request)
        => ConfigMarkdown.Crear(ParseTipoArtefacto(request?.TipoArtefacto ?? "respuesta"));

    private static ConfigConversacional ToConfigConversacional(ConfigConversacionalRequest? request)
        => ConfigConversacional.Crear(request?.MaxRepreguntas ?? 1, RequerirTexto(request?.MensajeCierre ?? "Gracias. Tu aporte quedo registrado correctamente.", "mensajeCierre"));

    private static LimitesSeguridad ToLimitesCampania(LimitesSeguridadRequest? request)
        => LimitesSeguridad.Crear(
            request?.MaxCaracteresMensaje ?? 1500,
            request?.MaxMensajesPorUsuario ?? 10,
            request?.MaxLlamadasLlmPorUsuario ?? 2,
            request?.PresupuestoTokensCampania ?? 0);

    private static LimitesSeguridad ToLimitesPregunta(LimitesSeguridadPreguntaRequest? request)
        => LimitesSeguridad.ParaPregunta(request?.MaxCaracteresMensaje ?? 1500, request?.MaxLlamadasLlm ?? 2);

    private static PlantillaWhatsApp ToPlantilla(PlantillaWhatsAppRequest request)
        => PlantillaWhatsApp.Crear(
            RequerirTexto(request.Nombre, "plantillaWhatsApp.nombre"),
            RequerirTexto(request.Idioma, "plantillaWhatsApp.idioma"),
            request.Componentes);

    private static object MapearCampaniaResumen(Campania campania)
        => new
        {
            campania.Id,
            campania.Nombre,
            campania.Descripcion,
            campania.Objetivo,
            estado = ToApiEstado(campania.Estado),
            campania.CreadoEn,
            campania.ActualizadoEn,
        };

    private static object MapearCampania(Campania campania)
        => new
        {
            campania.Id,
            campania.Nombre,
            campania.Descripcion,
            campania.Objetivo,
            estado = ToApiEstado(campania.Estado),
            mensajesIniciales = campania.MensajesIniciales.Select(MapearMensajeInicial),
            preguntas = campania.Preguntas.Select(MapearPregunta),
            campania.RubricaRef,
            promptRefs = campania.PromptRefs,
            configLLMRef = campania.ConfigLlmRef,
            configMarkdown = MapearConfigMarkdown(campania.ConfigMarkdown),
            configConversacional = MapearConfigConversacional(campania.ConfigConversacional),
            configSeguridad = MapearLimitesCampania(campania.ConfigSeguridad),
            usuariosHabilitados = campania.UsuariosHabilitados,
            campania.CreadoEn,
            campania.ActualizadoEn,
        };

    private static object MapearMensajeInicial(MensajeInicial mensaje)
        => new
        {
            mensaje.Id,
            mensaje.NombreInterno,
            mensaje.Texto,
            mensaje.Orden,
            variablesDinamicas = mensaje.VariablesDinamicas,
            estado = ToApiEstado(mensaje.Estado),
            plantillaWhatsApp = mensaje.PlantillaWhatsApp is null ? null : new { mensaje.PlantillaWhatsApp.Nombre, mensaje.PlantillaWhatsApp.Idioma, componentes = mensaje.PlantillaWhatsApp.Componentes },
        };

    private static object MapearPregunta(Pregunta pregunta)
        => new
        {
            pregunta.Id,
            pregunta.Texto,
            pregunta.Instruccion,
            pregunta.Categoria,
            pregunta.Orden,
            estado = ToApiEstado(pregunta.Estado),
            pregunta.RubricaRef,
            pregunta.VersionRubrica,
            promptRefs = pregunta.PromptRefs,
            pregunta.MaxRepreguntas,
            limitesSeguridad = MapearLimitesPregunta(pregunta.LimitesSeguridad),
            configMarkdown = MapearConfigMarkdown(pregunta.ConfigMarkdown),
        };

    private static object MapearParticipante(ParticipanteCampania participante)
        => new
        {
            participante.Id,
            participante.CampaniaId,
            participante.UsuarioId,
            whatsappNormalizado = participante.WhatsappNormalizado.Valor,
            estado = ToApiEstado(participante.Estado),
            estadoEnvio = participante.EstadoEnvio.ToString().ToLowerInvariant(),
            estadoRespuesta = participante.EstadoRespuesta == EstadoRespuestaParticipante.SinRespuesta ? "sinRespuesta" : "respondio",
            participante.FechaInclusion,
            participante.FechaPrimerEnvio,
            participante.FechaUltimaRespuesta,
        };

    private static object MapearRubrica(Rubrica rubrica)
        => new
        {
            rubrica.Id,
            rubrica.Nombre,
            rubrica.Descripcion,
            rubrica.ContenidoMarkdown,
            escala = rubrica.Escala,
            criterios = rubrica.Criterios,
            rubrica.Version,
            estado = ToApiEstado(rubrica.Estado),
            rubrica.CreadoEn,
            rubrica.ActualizadoEn,
        };

    private static object MapearPrompt(Prompt prompt)
        => new
        {
            prompt.Id,
            prompt.Nombre,
            prompt.TipoPrompt,
            prompt.Contenido,
            prompt.Version,
            estado = ToApiEstado(prompt.Estado),
            prompt.AprobadoPor,
            prompt.FechaAprobacion,
            prompt.CreadoEn,
            prompt.ActualizadoEn,
        };

    private static object MapearConfigLlm(ConfigLlm config)
        => new
        {
            config.Id,
            config.Nombre,
            config.Proveedor,
            config.Modelo,
            config.Endpoint,
            config.ApiKeyRef,
            apiKeyMascara = "********",
            parametros = config.Parametros,
            limitesTokens = config.LimitesTokens,
            config.TimeoutSegundos,
            config.MaxReintentos,
            estado = config.Estado == EstadoRegistro.Activo ? "activa" : "inactiva",
            config.CreadoEn,
            config.ActualizadoEn,
        };

    private static object MapearConfigMarkdown(ConfigMarkdown config)
        => new { tipoArtefacto = ToApiTipoArtefacto(config.TipoArtefacto) };

    private static object MapearConfigConversacional(ConfigConversacional config)
        => new { config.MaxRepreguntas, config.MensajeCierre };

    private static object MapearLimitesCampania(LimitesSeguridad limites)
        => new { limites.MaxCaracteresMensaje, limites.MaxMensajesPorUsuario, limites.MaxLlamadasLlmPorUsuario, limites.PresupuestoTokensCampania };

    private static object MapearLimitesPregunta(LimitesSeguridad limites)
        => new { limites.MaxCaracteresMensaje, maxLlamadasLlm = limites.MaxLlamadasLlmPorUsuario };

    private static RespuestaPaginada<T> Paginar<T>(IReadOnlyCollection<T> items, StringValues page, StringValues pageSize)
    {
        var numeroPagina = ParsearEnteroPositivo(page, "page", PaginaPorDefecto);
        var tamanoPagina = Math.Min(ParsearEnteroPositivo(pageSize, "pageSize", TamanoPaginaPorDefecto), TamanoPaginaMaximo);
        return new RespuestaPaginada<T>(items.Skip((numeroPagina - 1) * tamanoPagina).Take(tamanoPagina).ToArray(), numeroPagina, tamanoPagina, items.Count);
    }

    private static int ParsearEnteroPositivo(StringValues valor, string campo, int porDefecto)
    {
        var texto = valor.ToString();
        if (string.IsNullOrWhiteSpace(texto))
        {
            return porDefecto;
        }

        if (int.TryParse(texto, out var numero) && numero > 0)
        {
            return numero;
        }

        throw new ErrorValidacion($"El campo {campo} debe ser un entero positivo.", new[] { new DetalleError(campo, "entero_positivo") });
    }

    private static IReadOnlyCollection<string> ParseTags(StringValues tag, StringValues tags)
        => tag.Concat(tags)
            .Where(v => v is not null)
            .SelectMany(v => v!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(v => v.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static string RequerirTexto(string? valor, string campo)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            throw new ErrorValidacion($"El campo {campo} es obligatorio.", new[] { new DetalleError(campo, "obligatorio") });
        }

        return valor.Trim();
    }

    private static EstadoCampania? ParseEstadoCampaniaOpcional(StringValues valor)
        => string.IsNullOrWhiteSpace(valor.ToString()) ? null : ParseEstadoCampania(valor.ToString());

    private static EstadoCampania ParseEstadoCampania(string? valor)
        => (valor ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "borrador" => EstadoCampania.Borrador,
            "activa" => EstadoCampania.Activa,
            "cerrada" => EstadoCampania.Cerrada,
            "archivada" => EstadoCampania.Archivada,
            _ => throw new ErrorValidacion("El estado de campania no es valido.", new[] { new DetalleError("estado", "valor_invalido") }),
        };

    private static EstadoRegistro? ParseEstadoRegistroOpcional(string? valor, string campo)
        => string.IsNullOrWhiteSpace(valor) ? null : ParseEstadoRegistro(valor, campo);

    private static EstadoRegistro? ParseEstadoRegistroOpcional(StringValues valor, string campo)
        => ParseEstadoRegistroOpcional(valor.ToString(), campo);

    private static EstadoRegistro ParseEstadoRegistro(string? valor, string campo)
        => (valor ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "activo" or "activa" => EstadoRegistro.Activo,
            "inactivo" or "inactiva" => EstadoRegistro.Inactivo,
            _ => throw new ErrorValidacion($"El campo {campo} no es valido.", new[] { new DetalleError(campo, "valor_invalido") }),
        };

    private static EstadoRubrica? ParseEstadoRubricaOpcional(StringValues valor)
        => ParseEstadoRubricaOpcional(valor.ToString());

    private static EstadoRubrica? ParseEstadoRubricaOpcional(string? valor)
        => string.IsNullOrWhiteSpace(valor) ? null : ParseEstadoRubrica(valor);

    private static EstadoRubrica ParseEstadoRubrica(string? valor)
        => (valor ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "activa" => EstadoRubrica.Activa,
            "archivada" => EstadoRubrica.Archivada,
            "borrador" => EstadoRubrica.Borrador,
            _ => throw new ErrorValidacion("El estado de rubrica no es valido.", new[] { new DetalleError("estado", "valor_invalido") }),
        };

    private static string ToApiEstado(EstadoRubrica estado)
        => estado switch
        {
            EstadoRubrica.Activa => "activa",
            EstadoRubrica.Archivada => "archivada",
            EstadoRubrica.Borrador => "borrador",
            _ => estado.ToString().ToLowerInvariant(),
        };

    private static EstadoPrompt? ParseEstadoPromptOpcional(StringValues valor)
        => ParseEstadoPromptOpcional(valor.ToString());

    private static EstadoPrompt? ParseEstadoPromptOpcional(string? valor)
        => string.IsNullOrWhiteSpace(valor) ? null : ParseEstadoPrompt(valor);

    private static EstadoPrompt ParseEstadoPrompt(string? valor)
        => (valor ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "borrador" => EstadoPrompt.Borrador,
            "activo" => EstadoPrompt.Activo,
            "inactivo" => EstadoPrompt.Inactivo,
            _ => throw new ErrorValidacion("El estado de prompt no es valido.", new[] { new DetalleError("estado", "valor_invalido") }),
        };

    private static TipoArtefactoMarkdown ParseTipoArtefacto(string valor)
        => valor.Trim().ToLowerInvariant() switch
        {
            "respuesta" => TipoArtefactoMarkdown.Respuesta,
            "participante" => TipoArtefactoMarkdown.Participante,
            "campania" => TipoArtefactoMarkdown.Campania,
            "entidad" => TipoArtefactoMarkdown.Entidad,
            "capitulo" => TipoArtefactoMarkdown.Capitulo,
            _ => throw new ErrorValidacion("El tipo de artefacto no es valido.", new[] { new DetalleError("tipoArtefacto", "valor_invalido") }),
        };

    private static string ToApiEstado(EstadoCampania estado)
        => estado.ToString().ToLowerInvariant();

    private static string ToApiEstado(EstadoRegistro estado)
        => estado.ToString().ToLowerInvariant();

    private static string ToApiEstado(EstadoPrompt estado)
        => estado.ToString().ToLowerInvariant();

    private static string ToApiTipoArtefacto(TipoArtefactoMarkdown tipo)
        => tipo.ToString().ToLowerInvariant();

    private sealed record CambiarEstadoRequest(string? Estado);
    private sealed record CampaniaRequest(string? Nombre, string? Descripcion, string? Objetivo, string? RubricaRef, IReadOnlyDictionary<string, string>? PromptRefs, string? ConfigLlmRef, ConfigMarkdownRequest? ConfigMarkdown, ConfigConversacionalRequest? ConfigConversacional, LimitesSeguridadRequest? ConfigSeguridad);
    private sealed record CampaniaPatchRequest(string? Nombre, string? Descripcion, string? Objetivo, string? RubricaRef, IReadOnlyDictionary<string, string>? PromptRefs, string? ConfigLlmRef, ConfigMarkdownRequest? ConfigMarkdown, ConfigConversacionalRequest? ConfigConversacional, LimitesSeguridadRequest? ConfigSeguridad);
    private sealed record ConfigMarkdownRequest(string? TipoArtefacto);
    private sealed record ConfigConversacionalRequest(int? MaxRepreguntas, string? MensajeCierre);
    private sealed record LimitesSeguridadRequest(int? MaxCaracteresMensaje, int? MaxMensajesPorUsuario, int? MaxLlamadasLlmPorUsuario, int? PresupuestoTokensCampania);
    private sealed record LimitesSeguridadPreguntaRequest(int? MaxCaracteresMensaje, int? MaxLlamadasLlm);
    private sealed record PlantillaWhatsAppRequest(string? Nombre, string? Idioma, IReadOnlyCollection<string>? Componentes);
    private sealed record MensajeInicialRequest(string? NombreInterno, string? Texto, int? Orden, IReadOnlyCollection<string>? VariablesDinamicas, string? Estado, PlantillaWhatsAppRequest? PlantillaWhatsApp);
    private sealed record MensajeInicialPatchRequest(string? NombreInterno, string? Texto, int? Orden, IReadOnlyCollection<string>? VariablesDinamicas, string? Estado, PlantillaWhatsAppRequest? PlantillaWhatsApp);
    private sealed record PreguntaRequest(string? Texto, string? Instruccion, string? Categoria, int? Orden, string? Estado, string? RubricaRef, int? VersionRubrica, IReadOnlyDictionary<string, string>? PromptRefs, int? MaxRepreguntas, LimitesSeguridadPreguntaRequest? LimitesSeguridad, ConfigMarkdownRequest? ConfigMarkdown);
    private sealed record PreguntaPatchRequest(string? Texto, string? Instruccion, string? Categoria, int? Orden, string? Estado, string? RubricaRef, int? VersionRubrica, IReadOnlyDictionary<string, string>? PromptRefs, int? MaxRepreguntas, LimitesSeguridadPreguntaRequest? LimitesSeguridad, ConfigMarkdownRequest? ConfigMarkdown);
    private sealed record AsociarParticipantesRequest(IReadOnlyCollection<string>? UsuarioIds, FiltroParticipantesRequest? Filtro);
    private sealed record ReiniciarParticipanteRequest(bool? ReiniciarEnvios);
    private sealed record ReiniciarCampaniaRequest(IReadOnlyCollection<string>? UsuarioIds, bool? ReiniciarEnvios);
    private sealed record FiltroParticipantesRequest(string? Area, string? Empresa, IReadOnlyCollection<string>? Tags, string? Busqueda);
    private sealed record RubricaRequest(string? Id, string? Nombre, string? Descripcion, string? ContenidoMarkdown, EscalaRequest? Escala, IReadOnlyCollection<CriterioRequest>? Criterios, string? Estado);
    private sealed record EscalaRequest(int Min, int Max);
    private sealed record CriterioRequest(string? Nombre, decimal Peso);
    private sealed record PromptRequest(string? Id, string? Nombre, string? TipoPrompt, string? Contenido, string? Estado);
    private sealed record AprobarPromptRequest(string? AprobadoPor);
    // `apiKey` ya no se acepta: solo `apiKeyRef` (nombre de un secreto que ya existe en Key Vault).
    private sealed record ConfigLlmRequest(string? Nombre, string? Proveedor, string? Modelo, string? Endpoint, string? ApiKeyRef, IReadOnlyDictionary<string, object?>? Parametros, LimitesTokensRequest? LimitesTokens, int? TimeoutSegundos, int? MaxReintentos, string? Estado);
    private sealed record ConfigLlmPatchRequest(string? Nombre, string? Proveedor, string? Modelo, string? Endpoint, string? ApiKeyRef, IReadOnlyDictionary<string, object?>? Parametros, LimitesTokensRequest? LimitesTokens, int? TimeoutSegundos, int? MaxReintentos, string? Estado);
    private sealed record LimitesTokensRequest(int MaxPrompt, int MaxCompletion);
    private sealed record RespuestaPaginada<T>(IReadOnlyCollection<T> Items, int Page, int PageSize, int Total);
}
