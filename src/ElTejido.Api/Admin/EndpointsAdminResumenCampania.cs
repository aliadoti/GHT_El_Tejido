using ElTejido.Application.Campanas;
using ElTejido.Application.Common;
using ElTejido.Application.Conversacion;
using ElTejido.Application.Participantes;
using ElTejido.Application.Respuestas;
using ElTejido.Domain.Campanas;
using ElTejido.Domain.Configuracion;
using DominioEvaluacion = ElTejido.Domain.Evaluacion.Evaluacion;

namespace ElTejido.Api.Admin;

/// <summary>
/// P-34 §4.6 (04 §5.8): resumen de campaña. Calcularlo en el navegador obligaría a descargar las
/// 1.000 ideas previstas (D5), así que se resuelve en el servidor sobre el <b>mismo conjunto
/// filtrado</b> que la tabla: `totalIdeas` coincide siempre con el `total` del listado (§8.9).
/// </summary>
internal static class EndpointsAdminResumenCampania
{
    public static IEndpointRouteBuilder MapearEndpointsAdminResumenCampania(this IEndpointRouteBuilder app)
    {
        app.MapGroup("/api/admin")
            .AddEndpointFilter<AutorizacionAdminEndpointFilter>()
            .MapGet("/campanias/{campaniaId}/resumen", ObtenerResumenAsync);

        return app;
    }

    private static async Task<IResult> ObtenerResumenAsync(
        string campaniaId, HttpContext contexto, CancellationToken ct)
    {
        var query = contexto.Request.Query;
        var criterios = ConsultaIdeasResultados.Interpretar(LeerCriterios(query));
        var repo = contexto.RequestServices.GetRequiredService<IRepositorioRespuestas>();

        var alcance = await ConsultaResultadosCompartida.ResolverAsync(contexto, campaniaId, criterios, query, ct);
        var evaluaciones = alcance.Evaluaciones.Count > 0
            ? alcance.Evaluaciones
            : await ConsultaResultadosCompartida.EvaluacionesDeAsync(repo, campaniaId, alcance.Ideas, ct);

        var campania = await contexto.RequestServices.GetRequiredService<IRepositorioCampanias>()
            .ObtenerCampaniaPorIdAsync(campaniaId, ct)
            ?? throw new ErrorNoEncontrado("La campania no existe.");

        // La convocatoria es la de la campaña completa: es el denominador de «¿cuánta gente participó?».
        var convocados = (await contexto.RequestServices.GetRequiredService<IRepositorioParticipantes>()
            .ListarParticipantesAsync(campaniaId, ct)).Count;

        var (umbral, uniforme, escala) = ResolverUmbral(contexto, campania, evaluaciones);
        var resumen = ResumenCampaniaResultados.Construir(
            alcance.Ideas, evaluaciones, convocados, umbral, uniforme, escala);

        return Results.Ok(new
        {
            resumen.TotalIdeas,
            participacion = new
            {
                resumen.Participacion.Convocados,
                resumen.Participacion.ConIdeas,
                resumen.Participacion.PromedioIdeasPorActivo,
            },
            embudo = new
            {
                resumen.Embudo.Iniciadas,
                resumen.Embudo.Confirmadas,
                resumen.Embudo.ConEvaluacion,
                resumen.Embudo.Maduras,
            },
            calificaciones = new
            {
                resumen.Calificaciones.Evaluadas,
                resumen.Calificaciones.Mediana,
                resumen.Calificaciones.Minima,
                resumen.Calificaciones.Maxima,
                resumen.Calificaciones.UmbralMadurez,
                resumen.Calificaciones.UmbralUniforme,
                escala = resumen.Calificaciones.Escala is null
                    ? null
                    : new { resumen.Calificaciones.Escala.Min, resumen.Calificaciones.Escala.Max },
                tramos = resumen.Calificaciones.Tramos.Select(tramo => new
                {
                    tramo.Desde,
                    tramo.Hasta,
                    tramo.Conteo,
                }),
            },
            coberturaPorPregunta = resumen.CoberturaPorPregunta.Select(cobertura => new
            {
                cobertura.PreguntaId,
                cobertura.Total,
                cobertura.Maduras,
                cobertura.Pendientes,
                cobertura.Rechazadas,
                cobertura.EnCurso,
            }),
            temas = resumen.Temas.Select(tema => new { tema.Tema, tema.Conteo }),
        });
    }

    /// <summary>
    /// P-34 §4.6: valor absoluto del umbral de madurez sobre la escala de la rúbrica. Solo es
    /// <b>uniforme</b> —y por tanto dibujable— cuando ninguna pregunta sobrescribe el umbral de la
    /// campaña y todas las evaluaciones comparten la misma escala; si no, la marca no aplicaría a
    /// todas las barras y es preferible no dibujarla a dibujar una línea que miente.
    /// </summary>
    private static (decimal? Umbral, bool Uniforme, EscalaRubrica? Escala) ResolverUmbral(
        HttpContext contexto,
        Campania campania,
        IReadOnlyDictionary<string, DominioEvaluacion> evaluaciones)
    {
        var escalas = evaluaciones.Values
            .Select(evaluacion => evaluacion.RubricaSnapshot?.Escala)
            .Where(escala => escala is not null)
            .Distinct()
            .ToArray();
        var escala = escalas.Length == 1 ? escalas[0] : null;
        var mismaEscala = escalas.Length <= 1 && evaluaciones.Values.All(e => e.RubricaSnapshot is not null);

        var configuracion = contexto.RequestServices.GetRequiredService<IConfiguration>();
        var umbralGlobal = configuracion.GetValue("Conversacion:UmbralCierreAnticipado", 0.6);
        var politica = new PoliticaLimitesConversacion(umbralGlobal, cierreAnticipadoHabilitado: true);
        var umbralCampania = campania.ConfigConversacional.UmbralCierreAnticipado ?? umbralGlobal;
        var sinOverridesDePregunta = campania.Preguntas.All(pregunta =>
            pregunta.UmbralCierreAnticipado is null
            || Math.Abs(pregunta.UmbralCierreAnticipado.Value - umbralCampania) < 0.0001);

        if (escala is null)
        {
            return (null, false, null);
        }

        return (politica.ValorUmbral(escala, umbralCampania), mismaEscala && sinOverridesDePregunta, escala);
    }

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
}
