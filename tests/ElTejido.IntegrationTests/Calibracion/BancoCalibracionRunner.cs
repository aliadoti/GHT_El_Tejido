using System.Text.Json;
using ElTejido.Application.Common;
using ElTejido.Application.Evaluacion;
using ElTejido.Application.Seguridad;
using ElTejido.Calibracion;
using ElTejido.Domain.Campanas;
using ElTejido.Domain.Common;
using ElTejido.Domain.Configuracion;
using ElTejido.Domain.Identidad;
using ElTejido.Domain.Usuarios;

namespace ElTejido.IntegrationTests.Calibracion;

/// <summary>
/// Infraestructura del runner opt-in del banco de calibración (D5 §3.4). Arma la tripleta de staging
/// (rúbrica+prompt+ConfigLLM) desde un JSON <b>versionado</b> apuntado por variable de entorno y expone
/// un <see cref="IEvaluadorEntrada"/> que evalúa cada entrada del golden set contra el LLM real. La API
/// key NO vive en el JSON: se resuelve por <see cref="ISecretProvider"/> desde una variable de entorno
/// (equivalente local de Key Vault); nunca se persiste en disco.
/// </summary>
internal static class RunnerCalibracion
{
    /// <summary>Ruta al JSON de la tripleta de staging (opt-in). Si falta, el runner es no-op.</summary>
    public const string VarConfig = "CALIBRACION_CONFIG";

    /// <summary>Valor de la API key de staging (equivalente local de Key Vault). Si falta, no-op.</summary>
    public const string VarApiKey = "CALIBRACION_API_KEY";

    /// <summary>Directorio de salida de reportes (default: tests/Calibracion/salida, gitignoreado).</summary>
    public const string VarSalida = "CALIBRACION_OUT";

    public static bool EstaConfigurado()
        => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(VarConfig))
           && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(VarApiKey));
}

/// <summary>Configuración versionada de la tripleta de staging (dato, no secreto).</summary>
internal sealed record ConfiguracionRunner(
    string Proveedor,
    string Endpoint,
    string Modelo,
    string ApiKeyRef,
    Dictionary<string, JsonElement>? Parametros,
    int MaxPromptTokens,
    int MaxCompletionTokens,
    int TimeoutSegundos,
    int MaxReintentos,
    int N,
    PrecioConfig? Precio,
    CampaniaConfig Campania,
    PreguntaConfig Pregunta,
    RubricaConfig Rubrica,
    PromptConfig Prompt)
{
    private static readonly JsonSerializerOptions Opciones = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static ConfiguracionRunner CargarDesdeArchivo(string ruta)
    {
        if (!File.Exists(ruta))
        {
            throw new CalibracionException($"No se encontró el config del runner en '{ruta}'.");
        }

        var config = JsonSerializer.Deserialize<ConfiguracionRunner>(File.ReadAllText(ruta), Opciones)
            ?? throw new CalibracionException("El config del runner deserializó a null.");
        return config;
    }
}

internal sealed record PrecioConfig(decimal PorMilPrompt, decimal PorMilCompletion);

internal sealed record CampaniaConfig(string Id, string Nombre, string Objetivo);

internal sealed record PreguntaConfig(string Id, string Texto);

internal sealed record RubricaConfig(string Id, int Version, int EscalaMin, int EscalaMax, string Markdown, CriterioConfig[] Criterios);

internal sealed record CriterioConfig(string Nombre, decimal Peso);

internal sealed record PromptConfig(string Id, int Version, string Contenido);

/// <summary>ISecretProvider local: devuelve la API key desde la variable de entorno. No la persiste.</summary>
internal sealed class SecretProviderEntorno : ISecretProvider
{
    public Task<string> ObtenerSecretoAsync(string nombre, CancellationToken cancellationToken)
    {
        var valor = Environment.GetEnvironmentVariable(RunnerCalibracion.VarApiKey);
        if (string.IsNullOrWhiteSpace(valor))
        {
            throw new CalibracionException($"Falta la API key de staging en la variable {RunnerCalibracion.VarApiKey}.");
        }

        return Task.FromResult(valor);
    }
}

/// <summary>Correlación fija para el runner fuera de una petición HTTP.</summary>
internal sealed class CorrelacionFija : IProveedorCorrelacion
{
    public string? CorrelationIdActual => "calibracion";
}

/// <summary>
/// Evalúa cada entrada del golden set contra el LLM real construyendo un <see cref="ContextoEvaluacion"/>
/// con la tripleta de staging (misma ruta que la producción, 08 §2-§4): así el reporte refleja el
/// comportamiento real del modelo con esa rúbrica/prompt/ConfigLLM versionados.
/// </summary>
internal sealed class EvaluadorEntradaLlmReal : IEvaluadorEntrada
{
    private readonly IEvaluadorLlm _evaluador;
    private readonly ConfiguracionRunner _config;
    private readonly Campania _campania;
    private readonly Pregunta _pregunta;
    private readonly Usuario _usuario;
    private readonly Rubrica _rubrica;
    private readonly Prompt _prompt;
    private readonly ConfigLlm _configLlm;

    public EvaluadorEntradaLlmReal(IEvaluadorLlm evaluador, ConfiguracionRunner config)
    {
        _evaluador = evaluador;
        _config = config;
        (_campania, _pregunta, _usuario, _rubrica, _prompt, _configLlm) = ConstruirTripleta(config);
    }

    public Task<ResultadoEvaluacion> EvaluarAsync(EntradaGoldenSet entrada, CancellationToken cancellationToken)
    {
        var contexto = new ContextoEvaluacion(
            _campania,
            _pregunta,
            _usuario,
            entrada.Id,
            entrada.TextoRespuesta,
            Array.Empty<string>(),
            _rubrica,
            _prompt,
            _configLlm);

        return _evaluador.EvaluarAsync(contexto, cancellationToken);
    }

    public MetadatosCorrido Metadatos(DateTimeOffset ahora)
        => new(
            _campania.Id,
            _rubrica.Id,
            _rubrica.Version,
            _prompt.Id,
            _prompt.Version,
            _configLlm.Id,
            _configLlm.Modelo,
            Math.Max(1, _config.N),
            ahora,
            _config.Precio is null ? null : new PrecioTokens(_config.Precio.PorMilPrompt, _config.Precio.PorMilCompletion));

    private static (Campania, Pregunta, Usuario, Rubrica, Prompt, ConfigLlm) ConstruirTripleta(ConfiguracionRunner config)
    {
        var epoca = DateTimeOffset.UnixEpoch;

        var pregunta = Pregunta.Crear(
            config.Pregunta.Id,
            config.Pregunta.Texto,
            "Instruccion",
            "categoria",
            1,
            EstadoRegistro.Activo,
            rubricaRef: config.Rubrica.Id,
            versionRubrica: config.Rubrica.Version,
            promptRefs: null,
            maxRepreguntas: 1,
            LimitesSeguridad.ParaPregunta(1500, 2),
            ConfigMarkdown.Crear(TipoArtefactoMarkdown.Respuesta));

        var campania = Campania.Crear(
            config.Campania.Id,
            config.Campania.Nombre,
            "Descripcion",
            config.Campania.Objetivo,
            EstadoCampania.Activa,
            mensajesIniciales: null,
            new[] { pregunta },
            rubricaRef: config.Rubrica.Id,
            promptRefs: null,
            configLlmRef: config.ConfigLlmId(),
            ConfigMarkdown.Crear(TipoArtefactoMarkdown.Campania),
            ConfigConversacional.Crear(1, "Gracias por participar."),
            LimitesSeguridad.Crear(1500, 10, 2),
            usuariosHabilitados: null,
            epoca,
            epoca);

        var usuario = Usuario.Crear(
            "u_calibracion",
            "Participante",
            NumeroWhatsApp.FromNormalized("573000000000"),
            RolUsuario.Participante,
            EstadoRegistro.Activo,
            "Operaciones",
            "GHT",
            tags: null,
            propiedadesDinamicas: null,
            epoca,
            epoca);

        var rubrica = Rubrica.Crear(
            config.Rubrica.Id,
            "Rubrica staging",
            "Rubrica de calibracion",
            config.Rubrica.Markdown,
            EscalaRubrica.Crear(config.Rubrica.EscalaMin, config.Rubrica.EscalaMax),
            config.Rubrica.Criterios.Select(c => CriterioRubrica.Crear(c.Nombre, c.Peso)).ToArray(),
            config.Rubrica.Version,
            EstadoRubrica.Activa,
            epoca,
            epoca);

        var prompt = Prompt.Crear(
            config.Prompt.Id,
            "Prompt staging",
            "Prompt de calibracion",
            config.Prompt.Contenido,
            config.Prompt.Version,
            EstadoPrompt.Activo,
            "u_admin",
            epoca,
            epoca,
            epoca);

        var configLlm = ConfigLlm.Crear(
            config.ConfigLlmId(),
            "ConfigLLM staging",
            config.Proveedor,
            config.Modelo,
            config.Endpoint,
            config.ApiKeyRef,
            ConvertirParametros(config.Parametros),
            LimitesTokensLlm.Crear(config.MaxPromptTokens, config.MaxCompletionTokens),
            config.TimeoutSegundos,
            config.MaxReintentos,
            EstadoRegistro.Activo,
            epoca,
            epoca);

        return (campania, pregunta, usuario, rubrica, prompt, configLlm);
    }

    private static IReadOnlyDictionary<string, object?>? ConvertirParametros(Dictionary<string, JsonElement>? parametros)
    {
        if (parametros is null || parametros.Count == 0)
        {
            return null;
        }

        var resultado = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (clave, valor) in parametros)
        {
            resultado[clave] = valor.ValueKind switch
            {
                JsonValueKind.Number => valor.TryGetInt64(out var entero) ? entero : valor.GetDouble(),
                JsonValueKind.String => valor.GetString(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null,
            };
        }

        return resultado;
    }
}

internal static class ConfiguracionRunnerExtensiones
{
    /// <summary>La ConfigLLM y la campaña comparten ref lógica; derivada del id de rúbrica no es necesaria.</summary>
    public static string ConfigLlmId(this ConfiguracionRunner config) => "llm_calibracion";
}
