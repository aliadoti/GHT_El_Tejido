using System.Text.Json;
using ElTejido.Application.Common;
using ElTejido.Application.Seguridad;
using ElTejido.Domain.Configuracion;
using ElTejido.Domain.Seguridad;
using DominioEvaluacion = ElTejido.Domain.Evaluacion.Evaluacion;
using ElTejido.Domain.Evaluacion;

namespace ElTejido.Application.Evaluacion;

/// <summary>
/// Evaluador con LLM (08 §3): construye el contexto, llama al proveedor por <see cref="ILlmClient"/>,
/// valida la salida contra el esquema (08 §4) y devuelve una evaluacion normalizada o un fallback
/// seguro (08 §6). La defensa anti prompt-injection es arquitectonica (separacion instruccion/dato,
/// salida tratada como dato no confiable); las anomalias reportadas se registran en LogSeguridad.
/// </summary>
public sealed class EvaluadorLlm : IEvaluadorLlm
{
    /// <summary>Retro neutra que se envia cuando la evaluacion cae en fallback (08 §6, REQ §20.3.10).</summary>
    public const string RetroNeutra = "Gracias, registramos tu aporte.";

    /// <summary>
    /// I-03: repregunta de respaldo cuando la sugerida por el LLM revela la rubrica. El dominio
    /// (<c>Evaluacion.Crear</c>, "REPREGUNTA_REQUERIDA") exige una repregunta no vacia siempre que
    /// <see cref="RecomendacionEvaluacion.Repreguntar"/>, asi que no se puede degradar a <c>null</c>:
    /// se usa este texto generico y seguro (sin nombrar rubrica/criterios/puntajes) en su lugar.
    /// </summary>
    public const string RepreguntaNeutra = "Cuentame un poco mas sobre esa idea, ¿que le agregarias?";

    private const int MaxCaracteresRetro = 600;

    /// <summary>
    /// DT-I20-02 §4.2: la repregunta es una sola pregunta conversacional. Se acota con el mismo
    /// maximo que la retroalimentacion; un exceso no se recorta, cae a <see cref="RepreguntaNeutra"/>.
    /// </summary>
    private const int MaxCaracteresRepregunta = 600;

    private static readonly JsonSerializerOptions OpcionesJson = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString,
    };

    private readonly ILlmClient _client;
    private readonly IRepositorioLogSeguridad _logSeguridad;
    private readonly IProveedorCorrelacion _correlacion;
    private readonly TimeProvider _tiempo;

    public EvaluadorLlm(
        ILlmClient client,
        IRepositorioLogSeguridad logSeguridad,
        IProveedorCorrelacion correlacion,
        TimeProvider tiempo)
    {
        _client = client;
        _logSeguridad = logSeguridad;
        _correlacion = correlacion;
        _tiempo = tiempo;
    }

    public async Task<ResultadoEvaluacion> EvaluarAsync(ContextoEvaluacion contexto, CancellationToken cancellationToken)
    {
        var request = ConstruirRequest(contexto);

        LlmRespuesta respuesta;
        try
        {
            respuesta = await _client.CompletarJsonAsync(request, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // Timeout/5xx tras reintentos o cualquier fallo del proveedor: fallback seguro (08 §6).
            // Sin respuesta no hay uso de tokens que contabilizar.
            return await FallbackAsync(contexto, "error_proveedor", uso: null, cancellationToken);
        }

        // Aunque la salida sea invalida, el proveedor ya cobro los tokens: se contabiliza el uso (P-10).
        var uso = respuesta.Uso;

        SalidaLlmEvaluacion? salida = null;
        try
        {
            salida = JsonSerializer.Deserialize<SalidaLlmEvaluacion>(respuesta.Texto, OpcionesJson);
        }
        catch (JsonException)
        {
            // salida no parseable -> fallback.
        }

        if (salida is null)
        {
            return await FallbackAsync(contexto, "salida_invalida:no_json", uso, cancellationToken);
        }

        if (!EsSalidaValida(salida, contexto.RubricaSnapshot.Escala, out var recomendacion, out var razonInvalida))
        {
            return await FallbackAsync(contexto, "salida_invalida:" + razonInvalida, uso, cancellationToken);
        }

        // I-03: el eje mas debil se calcula SIEMPRE server-side (nunca por el LLM), tras validar la
        // salida (08 §3.4). Solo alimenta el registro de la salvaguarda de fuga; no se persiste ni se
        // muestra al participante.
        var calificaciones = MapearCalificaciones(salida);
        var ejeDebil = CalculadorEjeDebil.Determinar(calificaciones, contexto.RubricaSnapshot.Criterios);
        salida = await AplicarFiltroRubricaAsync(contexto, salida, recomendacion, ejeDebil, cancellationToken);

        // DT-I20-02 §5.2: ultima guarda antes de persistir. Solo toca los campos visibles generados por
        // el LLM; puntajes, recomendacion, idea/version y el arbitraje server-side quedan intactos.
        salida = await AplicarContratoVisibleAsync(contexto, salida, recomendacion, cancellationToken);

        if (salida.AnomaliaSeguridad)
        {
            await RegistrarAnomaliaAsync(contexto, cancellationToken);
        }

        return new ResultadoEvaluacion.Exito(ConstruirEvaluacion(contexto, salida, recomendacion, uso, calificaciones));
    }

    /// <summary>
    /// I-03 capa 2: si la retro o la repregunta sugerida revelan la rubrica (nombre de criterio,
    /// patron de puntaje o palabras que delatan el mecanismo), se descartan y se registra la anomalia
    /// (08 §5 regla 10). La retro cae a <see cref="RetroNeutra"/>; la repregunta cae a
    /// <see cref="RepreguntaNeutra"/> (el dominio exige una repregunta no vacia si la recomendacion
    /// es repreguntar; ver su comentario).
    /// </summary>
    private async Task<SalidaLlmEvaluacion> AplicarFiltroRubricaAsync(
        ContextoEvaluacion contexto,
        SalidaLlmEvaluacion salida,
        RecomendacionEvaluacion recomendacion,
        Domain.Configuracion.CriterioRubrica? ejeDebil,
        CancellationToken cancellationToken)
    {
        var rubrica = contexto.RubricaSnapshot;
        var fugaRetro = FiltroSalidaRubrica.ContieneFuga(salida.RetroalimentacionUsuario, rubrica);
        var fugaRepregunta = recomendacion == RecomendacionEvaluacion.Repreguntar
            && FiltroSalidaRubrica.ContieneFuga(salida.RepreguntaSugerida, rubrica);

        if (!fugaRetro && !fugaRepregunta)
        {
            return salida;
        }

        await RegistrarFugaRubricaAsync(contexto, fugaRetro, fugaRepregunta, ejeDebil, cancellationToken);

        return salida with
        {
            RetroalimentacionUsuario = fugaRetro ? RetroNeutra : salida.RetroalimentacionUsuario,
            RepreguntaSugerida = fugaRepregunta ? RepreguntaNeutra : salida.RepreguntaSugerida,
        };
    }

    /// <summary>
    /// DT-I20-02 §5.2: contrato visible en texto plano. Un incumplimiento de presentacion se resuelve
    /// <b>por campo</b> —nunca descartando una evaluacion de fondo valida (§3)—: la retroalimentacion
    /// invalida cae a <see cref="RetroNeutra"/> y la repregunta invalida a
    /// <see cref="RepreguntaNeutra"/>, con el motivo fijo en auditoria y sin registrar el texto.
    /// <para>
    /// El exceso de longitud ya no se corta a ciegas: se recorta en frontera de oracion y, si no hay
    /// ninguna dentro del maximo, se usa el respaldo. Nunca se persiste una palabra partida (§5.2.7).
    /// </para>
    /// </summary>
    private async Task<SalidaLlmEvaluacion> AplicarContratoVisibleAsync(
        ContextoEvaluacion contexto,
        SalidaLlmEvaluacion salida,
        RecomendacionEvaluacion recomendacion,
        CancellationToken cancellationToken)
    {
        var exigeRepregunta = recomendacion == RecomendacionEvaluacion.Repreguntar;

        var retro = RecortarEnFronteraDeOracion(salida.RetroalimentacionUsuario, MaxCaracteresRetro);
        var motivoRetro = ValidadorFragmentoVisibleLlm.Validar(
            retro,
            new ContextoFragmentoVisible(TipoFragmentoVisible.Retroalimentacion, MaxCaracteresRetro)
            {
                // Con repregunta separada el turno ya lleva su unica pregunta (I-18): la retro no puede
                // agregar otra.
                AdmitePregunta = !exigeRepregunta,
            }).Motivo;

        var repregunta = salida.RepreguntaSugerida?.Trim();
        var motivoRepregunta = exigeRepregunta
            ? ValidadorFragmentoVisibleLlm.Validar(
                repregunta,
                new ContextoFragmentoVisible(TipoFragmentoVisible.Repregunta, MaxCaracteresRepregunta)
                {
                    AdmitePregunta = true,
                }).Motivo
            : null;

        if (motivoRetro is not null || motivoRepregunta is not null)
        {
            await RegistrarContratoVisibleAsync(contexto, motivoRetro, motivoRepregunta, exigeRepregunta, cancellationToken);
        }

        return salida with
        {
            RetroalimentacionUsuario = motivoRetro is null ? retro : RetroNeutra,
            RepreguntaSugerida = motivoRepregunta is null ? repregunta : RepreguntaNeutra,
        };
    }

    private static IReadOnlyList<CalificacionCriterio> MapearCalificaciones(SalidaLlmEvaluacion salida)
        => (salida.CalificacionPorCriterio ?? Array.Empty<SalidaCalificacionCriterio>())
            .Select(c => CalificacionCriterio.Crear(c.Criterio ?? "criterio", c.Puntaje, c.Justificacion ?? string.Empty))
            .ToArray();

    private LlmRequest ConstruirRequest(ContextoEvaluacion contexto)
    {
        var config = contexto.ConfigLlmSnapshot;
        return new LlmRequest(
            config.Proveedor,
            config.Endpoint,
            config.Modelo,
            config.ApiKeyRef,
            ConstructorMensajesEvaluacion.Construir(contexto),
            config.Parametros,
            config.LimitesTokens.MaxCompletion,
            config.TimeoutSegundos,
            config.MaxReintentos,
            contexto.Campania.Id);
    }

    private static bool EsSalidaValida(
        SalidaLlmEvaluacion salida,
        EscalaRubrica escala,
        out RecomendacionEvaluacion recomendacion,
        out string razon)
    {
        recomendacion = RecomendacionEvaluacion.Cerrar;
        razon = string.Empty;

        if (string.IsNullOrWhiteSpace(salida.RetroalimentacionUsuario))
        {
            razon = "retro_vacia";
            return false;
        }

        if (!TryMapearRecomendacion(salida.Recomendacion, out recomendacion))
        {
            razon = "recomendacion_invalida";
            return false;
        }

        if (recomendacion == RecomendacionEvaluacion.Repreguntar
            && string.IsNullOrWhiteSpace(salida.RepreguntaSugerida))
        {
            razon = "repregunta_vacia";
            return false;
        }

        if (!EnEscala(salida.CalificacionTotal, escala))
        {
            razon = "calificacion_fuera_de_escala";
            return false;
        }

        if (salida.CalificacionPorCriterio is not null
            && !salida.CalificacionPorCriterio.All(c => EnEscala(c.Puntaje, escala)))
        {
            razon = "criterio_fuera_de_escala";
            return false;
        }

        return true;
    }

    private DominioEvaluacion ConstruirEvaluacion(
        ContextoEvaluacion contexto,
        SalidaLlmEvaluacion salida,
        RecomendacionEvaluacion recomendacion,
        UsoTokensLlm? uso,
        IReadOnlyList<CalificacionCriterio> calificaciones)
    {
        return DominioEvaluacion.Crear(
            "eval_" + Guid.NewGuid().ToString("N"),
            contexto.Campania.Id,
            contexto.RespuestaId,
            contexto.Usuario.Id,
            contexto.Pregunta.Id,
            contexto.RubricaSnapshot.Id,
            contexto.RubricaSnapshot.Version,
            contexto.PromptSnapshot.Id,
            contexto.PromptSnapshot.Version,
            contexto.ConfigLlmSnapshot.Id,
            CrearSnapshotConfig(contexto.ConfigLlmSnapshot),
            CrearPesos(contexto.RubricaSnapshot),
            calificaciones,
            salida.CalificacionTotal,
            string.IsNullOrWhiteSpace(salida.Explicacion) ? "Sin explicacion." : salida.Explicacion!.Trim(),
            // DT-I20-02 §5.2.7: ya viene validada y acotada en frontera de oracion por
            // AplicarContratoVisibleAsync; aqui no se recorta nada a ciegas.
            salida.RetroalimentacionUsuario!.Trim(),
            recomendacion,
            recomendacion == RecomendacionEvaluacion.Repreguntar ? salida.RepreguntaSugerida : null,
            salida.Temas,
            salida.Entidades,
            salida.AnomaliaSeguridad,
            _tiempo.GetUtcNow(),
            uso,
            contexto.SolicitarParafraseo
                ? AcotarEnFronteraDeFrase(salida.ParafraseoDevuelto, contexto.MaxCaracteresParafraseo)
                : null,
            contexto.IdeaId,
            contexto.VersionIdeaId,
            contexto.IdeaId is null ? null : "ideaConsolidada");
    }

    private async Task<ResultadoEvaluacion> FallbackAsync(
        ContextoEvaluacion contexto,
        string motivo,
        UsoTokensLlm? uso,
        CancellationToken cancellationToken)
    {
        await RegistrarFallbackAsync(contexto, motivo, cancellationToken);

        var evaluacion = DominioEvaluacion.Crear(
            "eval_" + Guid.NewGuid().ToString("N"),
            contexto.Campania.Id,
            contexto.RespuestaId,
            contexto.Usuario.Id,
            contexto.Pregunta.Id,
            contexto.RubricaSnapshot.Id,
            contexto.RubricaSnapshot.Version,
            contexto.PromptSnapshot.Id,
            contexto.PromptSnapshot.Version,
            contexto.ConfigLlmSnapshot.Id,
            CrearSnapshotConfig(contexto.ConfigLlmSnapshot),
            CrearPesos(contexto.RubricaSnapshot),
            Array.Empty<CalificacionCriterio>(),
            0m,
            "Evaluacion en fallback: " + motivo,
            RetroNeutra,
            RecomendacionEvaluacion.Cerrar,
            null,
            null,
            null,
            anomaliaSeguridad: false,
            _tiempo.GetUtcNow(),
            uso,
            ideaId: contexto.IdeaId,
            versionIdeaId: contexto.VersionIdeaId,
            origenTextoEvaluado: contexto.IdeaId is null ? null : "ideaConsolidada");

        return new ResultadoEvaluacion.Fallback(evaluacion, motivo);
    }

    private Task RegistrarAnomaliaAsync(ContextoEvaluacion contexto, CancellationToken cancellationToken)
        => _logSeguridad.RegistrarAsync(
            LogSeguridad.Crear(
                "log_" + Guid.NewGuid().ToString("N"),
                TipoEventoSeguridad.AnomaliaLlm,
                contexto.Usuario.Id,
                numero: null,
                "anomalia",
                "anomalia_seguridad_reportada",
                _correlacion.CorrelationIdActual,
                _tiempo.GetUtcNow()),
            cancellationToken);

    private Task RegistrarFugaRubricaAsync(
        ContextoEvaluacion contexto,
        bool fugaRetro,
        bool fugaRepregunta,
        Domain.Configuracion.CriterioRubrica? ejeDebil,
        CancellationToken cancellationToken)
    {
        var campos = string.Join(
            "+",
            new[] { fugaRetro ? "retro" : null, fugaRepregunta ? "repregunta" : null }.Where(c => c is not null));

        return _logSeguridad.RegistrarAsync(
            LogSeguridad.Crear(
                "log_" + Guid.NewGuid().ToString("N"),
                TipoEventoSeguridad.AnomaliaLlm,
                contexto.Usuario.Id,
                numero: null,
                "fuga_rubrica",
                $"campos={campos};eje_debil={ejeDebil?.Nombre ?? "desconocido"}",
                _correlacion.CorrelationIdActual,
                _tiempo.GetUtcNow()),
            cancellationToken);
    }

    /// <summary>
    /// DT-I20-02 §8: solo metadatos de baja cardinalidad —componente, campo, resultado y motivo fijo—.
    /// Nunca el texto generado, la respuesta del participante, la idea consolidada ni el prompt.
    /// </summary>
    private Task RegistrarContratoVisibleAsync(
        ContextoEvaluacion contexto,
        string? motivoRetro,
        string? motivoRepregunta,
        bool exigeRepregunta,
        CancellationToken cancellationToken)
    {
        var repregunta = exigeRepregunta ? motivoRepregunta ?? "aceptado" : "no_aplica";
        var detalle = FormattableString.Invariant(
            $"componente=evaluador;retroalimentacion={motivoRetro ?? "aceptado"};repregunta={repregunta}");

        return _logSeguridad.RegistrarAsync(
            LogSeguridad.Crear(
                "log_" + Guid.NewGuid().ToString("N"),
                TipoEventoSeguridad.AnomaliaLlm,
                contexto.Usuario.Id,
                numero: null,
                "contrato_visible",
                detalle,
                _correlacion.CorrelationIdActual,
                _tiempo.GetUtcNow()),
            cancellationToken);
    }

    private Task RegistrarFallbackAsync(ContextoEvaluacion contexto, string motivo, CancellationToken cancellationToken)
        => _logSeguridad.RegistrarAsync(
            LogSeguridad.Crear(
                "log_" + Guid.NewGuid().ToString("N"),
                TipoEventoSeguridad.AnomaliaLlm,
                contexto.Usuario.Id,
                numero: null,
                "fallback",
                motivo,
                _correlacion.CorrelationIdActual,
                _tiempo.GetUtcNow()),
            cancellationToken);

    private static ConfigLlmSnapshot CrearSnapshotConfig(ConfigLlm config)
        => new(config.Proveedor, config.Modelo, config.Endpoint, config.Parametros);

    private static IReadOnlyDictionary<string, decimal> CrearPesos(Rubrica rubrica)
        => rubrica.Criterios.ToDictionary(c => c.Nombre, c => c.Peso, StringComparer.Ordinal);

    private static bool TryMapearRecomendacion(string? valor, out RecomendacionEvaluacion recomendacion)
    {
        switch ((valor ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "cerrar":
                recomendacion = RecomendacionEvaluacion.Cerrar;
                return true;
            case "repreguntar":
                recomendacion = RecomendacionEvaluacion.Repreguntar;
                return true;
            default:
                recomendacion = RecomendacionEvaluacion.Cerrar;
                return false;
        }
    }

    private static bool EnEscala(decimal valor, EscalaRubrica escala)
        => valor >= escala.Min && valor <= escala.Max;

    /// <summary>I-05: sin maximo utilizable el parafraseo se descarta como degradacion segura.</summary>
    private static string? AcotarEnFronteraDeFrase(string? texto, int maximo)
        => maximo <= 0 ? null : RecortarEnFronteraDeOracion(texto, maximo);

    /// <summary>
    /// Devuelve el texto tal cual si cabe en <paramref name="maximo"/>; si no, lo recorta en el ultimo
    /// cierre de oracion que quepa. Devuelve <c>null</c> cuando no hay ninguno: DT-I20-02 §5.2.7
    /// prohibe persistir una palabra u oracion partida, asi que el llamador usa su respaldo.
    /// </summary>
    private static string? RecortarEnFronteraDeOracion(string? texto, int maximo)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        var normalizado = texto.Trim();
        if (maximo <= 0 || normalizado.Length <= maximo)
        {
            return normalizado;
        }

        var finFrase = normalizado.LastIndexOfAny(['.', '!', '?'], maximo - 1);
        return finFrase < 0 ? null : normalizado[..(finFrase + 1)].Trim();
    }
}
