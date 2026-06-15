using ElTejido.Domain.Common;
using ElTejido.Domain.Configuracion;

namespace ElTejido.Application.Configuracion;

public interface IServicioGestionConfiguracion
{
    Task<IReadOnlyCollection<Rubrica>> BuscarRubricasAsync(EstadoRubrica? estado, CancellationToken cancellationToken);

    Task<Rubrica> ObtenerRubricaAsync(string id, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Rubrica>> ListarVersionesRubricaAsync(string id, CancellationToken cancellationToken);

    Task<Rubrica> CrearRubricaAsync(SolicitudGuardarRubrica solicitud, CancellationToken cancellationToken);

    Task<Rubrica> CrearVersionRubricaAsync(string id, SolicitudGuardarRubrica solicitud, CancellationToken cancellationToken);

    Task<Rubrica> CambiarEstadoRubricaAsync(string id, EstadoRubrica estado, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Prompt>> BuscarPromptsAsync(string? tipoPrompt, EstadoPrompt? estado, CancellationToken cancellationToken);

    Task<Prompt> ObtenerPromptAsync(string id, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Prompt>> ListarVersionesPromptAsync(string id, CancellationToken cancellationToken);

    Task<Prompt> CrearPromptAsync(SolicitudGuardarPrompt solicitud, CancellationToken cancellationToken);

    Task<Prompt> CrearVersionPromptAsync(string id, SolicitudGuardarPrompt solicitud, CancellationToken cancellationToken);

    Task<Prompt> AprobarPromptAsync(string id, string usuarioId, CancellationToken cancellationToken);

    Task<Prompt> CambiarEstadoPromptAsync(string id, EstadoPrompt estado, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ConfigLlm>> BuscarConfigsLlmAsync(EstadoRegistro? estado, CancellationToken cancellationToken);

    Task<ConfigLlm> ObtenerConfigLlmAsync(string id, CancellationToken cancellationToken);

    Task<ConfigLlm> CrearConfigLlmAsync(SolicitudGuardarConfigLlm solicitud, CancellationToken cancellationToken);

    Task<ConfigLlm> ActualizarConfigLlmAsync(string id, SolicitudActualizarConfigLlm solicitud, CancellationToken cancellationToken);

    Task<ConfigLlm> CambiarEstadoConfigLlmAsync(string id, EstadoRegistro estado, CancellationToken cancellationToken);
}

public sealed record SolicitudGuardarRubrica(
    string Id,
    string Nombre,
    string Descripcion,
    string ContenidoMarkdown,
    EscalaRubrica Escala,
    IEnumerable<CriterioRubrica> Criterios,
    EstadoRubrica Estado);

public sealed record SolicitudGuardarPrompt(
    string Id,
    string Nombre,
    string TipoPrompt,
    string Contenido,
    EstadoPrompt Estado);

// Nota seguridad (10 §4): la app NO recibe ni escribe la API key. Solo guarda `ApiKeyRef`, el nombre
// de un secreto que YA debe existir en Key Vault con la API key real (lo carga un humano/operacion).
public sealed record SolicitudGuardarConfigLlm(
    string Nombre,
    string Proveedor,
    string Modelo,
    string Endpoint,
    string ApiKeyRef,
    IReadOnlyDictionary<string, object?>? Parametros,
    LimitesTokensLlm LimitesTokens,
    int TimeoutSegundos,
    int MaxReintentos,
    EstadoRegistro Estado);

public sealed record SolicitudActualizarConfigLlm(
    string? Nombre,
    string? Proveedor,
    string? Modelo,
    string? Endpoint,
    string? ApiKeyRef,
    IReadOnlyDictionary<string, object?>? Parametros,
    LimitesTokensLlm? LimitesTokens,
    int? TimeoutSegundos,
    int? MaxReintentos,
    EstadoRegistro? Estado);
