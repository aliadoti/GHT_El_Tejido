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

    /// <summary>
    /// Edita en sitio la version vigente de una rubrica. Solo permitido si esta en
    /// <see cref="EstadoRubrica.Borrador"/> (no comprometida); en cualquier otro estado lanza
    /// conflicto y debe usarse <see cref="CrearVersionRubricaAsync"/> para conservar snapshots.
    /// </summary>
    Task<Rubrica> ActualizarRubricaAsync(string id, SolicitudGuardarRubrica solicitud, CancellationToken cancellationToken);

    Task<Rubrica> CambiarEstadoRubricaAsync(string id, EstadoRubrica estado, CancellationToken cancellationToken);

    /// <summary>
    /// DT-RUB-01 (04 §5.5): ejecuta el mismo validador y compilador que la escritura real
    /// <b>sin escribir nada</b> y devuelve los motivos tipificados junto con el Markdown derivado.
    /// Es la fuente del preview del portal; no constituye prueba de activacion.
    /// </summary>
    ResultadoPrevalidacionRubrica PrevalidarRubrica(SolicitudGuardarRubrica solicitud);

    Task<IReadOnlyCollection<Prompt>> BuscarPromptsAsync(string? tipoPrompt, EstadoPrompt? estado, CancellationToken cancellationToken);

    Task<Prompt> ObtenerPromptAsync(string id, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Prompt>> ListarVersionesPromptAsync(string id, CancellationToken cancellationToken);

    Task<Prompt> CrearPromptAsync(SolicitudGuardarPrompt solicitud, CancellationToken cancellationToken);

    Task<Prompt> CrearVersionPromptAsync(string id, SolicitudGuardarPrompt solicitud, CancellationToken cancellationToken);

    /// <summary>
    /// Edita en sitio la version vigente de un prompt. Solo permitido si esta en
    /// <see cref="EstadoPrompt.Borrador"/> (sin aprobar, nunca usado para evaluar); en cualquier otro
    /// estado lanza conflicto y debe usarse <see cref="CrearVersionPromptAsync"/>.
    /// </summary>
    Task<Prompt> ActualizarPromptAsync(string id, SolicitudGuardarPrompt solicitud, CancellationToken cancellationToken);

    Task<Prompt> AprobarPromptAsync(string id, string usuarioId, CancellationToken cancellationToken);

    Task<Prompt> CambiarEstadoPromptAsync(string id, EstadoPrompt estado, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ConfigLlm>> BuscarConfigsLlmAsync(EstadoRegistro? estado, CancellationToken cancellationToken);

    Task<ConfigLlm> ObtenerConfigLlmAsync(string id, CancellationToken cancellationToken);

    Task<ConfigLlm> CrearConfigLlmAsync(SolicitudGuardarConfigLlm solicitud, CancellationToken cancellationToken);

    Task<ConfigLlm> ActualizarConfigLlmAsync(string id, SolicitudActualizarConfigLlm solicitud, CancellationToken cancellationToken);

    Task<ConfigLlm> CambiarEstadoConfigLlmAsync(string id, EstadoRegistro estado, CancellationToken cancellationToken);
}

/// <summary>
/// Estructura canonica de una version de rubrica (03 §3.11, 04 §5.5). DT-RUB-01 elimina
/// <c>ContenidoMarkdown</c> del cuerpo: la proyeccion la compila el servidor desde estos campos.
/// </summary>
public sealed record SolicitudGuardarRubrica(
    string Id,
    string Nombre,
    string Descripcion,
    string? InstruccionesGenerales,
    EscalaRubrica Escala,
    IEnumerable<CriterioRubrica> Criterios,
    EstadoRubrica Estado);

/// <summary>
/// Resultado de <c>POST /api/admin/rubricas/prevalidar</c> (04 §5.5). Cuando
/// <paramref name="Valido"/> es <c>false</c>, <paramref name="ContenidoMarkdown"/> queda vacio: no se
/// publica un preview de una estructura que el servidor no aceptaria.
/// </summary>
public sealed record ResultadoPrevalidacionRubrica(
    bool Valido,
    IReadOnlyList<ErrorRubrica> Errores,
    string ContenidoMarkdown,
    string HashEstructura);

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
