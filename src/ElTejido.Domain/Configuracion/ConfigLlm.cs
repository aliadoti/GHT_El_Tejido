using ElTejido.Domain.Common;

namespace ElTejido.Domain.Configuracion;

/// <summary>
/// Configuracion segura del proveedor LLM. Persiste solo apiKeyRef, nunca la clave. Cubre REQ 19 y 29.10.
/// </summary>
public sealed class ConfigLlm
{
    private ConfigLlm(
        string id,
        string nombre,
        string proveedor,
        string modelo,
        string endpoint,
        string apiKeyRef,
        IReadOnlyDictionary<string, object?> parametros,
        LimitesTokensLlm limitesTokens,
        int timeoutSegundos,
        int maxReintentos,
        EstadoRegistro estado,
        DateTimeOffset creadoEn,
        DateTimeOffset actualizadoEn)
    {
        Id = id;
        Nombre = nombre;
        Proveedor = proveedor;
        Modelo = modelo;
        Endpoint = endpoint;
        ApiKeyRef = apiKeyRef;
        Parametros = parametros;
        LimitesTokens = limitesTokens;
        TimeoutSegundos = timeoutSegundos;
        MaxReintentos = maxReintentos;
        Estado = estado;
        CreadoEn = creadoEn;
        ActualizadoEn = actualizadoEn;
    }

    public string Id { get; }

    public string Nombre { get; }

    public string Proveedor { get; }

    public string Modelo { get; }

    public string Endpoint { get; }

    public string ApiKeyRef { get; }

    public IReadOnlyDictionary<string, object?> Parametros { get; }

    public LimitesTokensLlm LimitesTokens { get; }

    public int TimeoutSegundos { get; }

    public int MaxReintentos { get; }

    public EstadoRegistro Estado { get; }

    public DateTimeOffset CreadoEn { get; }

    public DateTimeOffset ActualizadoEn { get; }

    public static ConfigLlm Crear(
        string id,
        string nombre,
        string proveedor,
        string modelo,
        string endpoint,
        string apiKeyRef,
        IReadOnlyDictionary<string, object?>? parametros,
        LimitesTokensLlm limitesTokens,
        int timeoutSegundos,
        int maxReintentos,
        EstadoRegistro estado,
        DateTimeOffset creadoEn,
        DateTimeOffset actualizadoEn)
    {
        if (timeoutSegundos <= 0 || maxReintentos < 0)
        {
            throw new DomainValidationException(
                "CONFIG_LLM_LIMITES_INVALIDOS",
                "Timeout debe ser mayor que cero y maxReintentos no puede ser negativo.");
        }

        var fechaCreacionUtc = creadoEn.ToUniversalTime();
        var fechaActualizacionUtc = actualizadoEn.ToUniversalTime();
        if (fechaActualizacionUtc < fechaCreacionUtc)
        {
            throw new DomainValidationException(
                "FECHA_ACTUALIZACION_INVALIDA",
                "La fecha de actualizacion no puede ser anterior a la fecha de creacion.");
        }

        return new ConfigLlm(
            DomainGuards.Required(id, nameof(id)),
            DomainGuards.Required(nombre, nameof(nombre)),
            DomainGuards.Required(proveedor, nameof(proveedor)),
            DomainGuards.Required(modelo, nameof(modelo)),
            DomainGuards.Required(endpoint, nameof(endpoint)),
            DomainGuards.Required(apiKeyRef, nameof(apiKeyRef)),
            parametros is null ? new Dictionary<string, object?>() : new Dictionary<string, object?>(parametros),
            limitesTokens,
            timeoutSegundos,
            maxReintentos,
            estado,
            fechaCreacionUtc,
            fechaActualizacionUtc);
    }
}
