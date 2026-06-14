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

    private const int MaxCaracteresRetro = 600;

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

        string crudo;
        try
        {
            crudo = await _client.CompletarJsonAsync(request, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // Timeout/5xx tras reintentos o cualquier fallo del proveedor: fallback seguro (08 §6).
            return await FallbackAsync(contexto, "error_proveedor", cancellationToken);
        }

        SalidaLlmEvaluacion? salida = null;
        try
        {
            salida = JsonSerializer.Deserialize<SalidaLlmEvaluacion>(crudo, OpcionesJson);
        }
        catch (JsonException)
        {
            // salida no parseable -> fallback.
        }

        if (salida is null || !EsSalidaValida(salida, contexto.RubricaSnapshot.Escala, out var recomendacion))
        {
            return await FallbackAsync(contexto, "salida_invalida", cancellationToken);
        }

        if (salida.AnomaliaSeguridad)
        {
            await RegistrarAnomaliaAsync(contexto, cancellationToken);
        }

        return new ResultadoEvaluacion.Exito(ConstruirEvaluacion(contexto, salida, recomendacion));
    }

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
            config.MaxReintentos);
    }

    private static bool EsSalidaValida(
        SalidaLlmEvaluacion salida,
        EscalaRubrica escala,
        out RecomendacionEvaluacion recomendacion)
    {
        recomendacion = RecomendacionEvaluacion.Cerrar;

        if (string.IsNullOrWhiteSpace(salida.RetroalimentacionUsuario))
        {
            return false;
        }

        if (!TryMapearRecomendacion(salida.Recomendacion, out recomendacion))
        {
            return false;
        }

        if (recomendacion == RecomendacionEvaluacion.Repreguntar
            && string.IsNullOrWhiteSpace(salida.RepreguntaSugerida))
        {
            return false;
        }

        if (!EnEscala(salida.CalificacionTotal, escala))
        {
            return false;
        }

        return salida.CalificacionPorCriterio is null
            || salida.CalificacionPorCriterio.All(c => EnEscala(c.Puntaje, escala));
    }

    private DominioEvaluacion ConstruirEvaluacion(
        ContextoEvaluacion contexto,
        SalidaLlmEvaluacion salida,
        RecomendacionEvaluacion recomendacion)
    {
        var calificaciones = (salida.CalificacionPorCriterio ?? Array.Empty<SalidaCalificacionCriterio>())
            .Select(c => CalificacionCriterio.Crear(c.Criterio ?? "criterio", c.Puntaje, c.Justificacion ?? string.Empty))
            .ToArray();

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
            Acotar(salida.RetroalimentacionUsuario!.Trim(), MaxCaracteresRetro),
            recomendacion,
            recomendacion == RecomendacionEvaluacion.Repreguntar ? salida.RepreguntaSugerida : null,
            salida.Temas,
            salida.Entidades,
            salida.AnomaliaSeguridad,
            _tiempo.GetUtcNow());
    }

    private async Task<ResultadoEvaluacion> FallbackAsync(
        ContextoEvaluacion contexto,
        string motivo,
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
            _tiempo.GetUtcNow());

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

    private static string Acotar(string texto, int maximo)
        => texto.Length > maximo ? texto[..maximo] : texto;
}
