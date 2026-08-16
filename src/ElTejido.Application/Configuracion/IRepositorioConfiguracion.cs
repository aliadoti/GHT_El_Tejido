using ElTejido.Domain.Common;
using ElTejido.Domain.Configuracion;

namespace ElTejido.Application.Configuracion;

/// <summary>
/// Puerto del contenedor Cosmos config para rubricas, prompts y ConfigLLM.
/// Cubre REQ 17-19 y ARQ 8-10 sin acoplar Application a Cosmos.
/// </summary>
public interface IRepositorioConfiguracion
{
    Task GuardarRubricaAsync(Rubrica rubrica, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Rubrica>> BuscarRubricasAsync(
        EstadoRubrica? estado,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Rubrica>> ListarVersionesRubricaAsync(
        string id,
        CancellationToken cancellationToken);

    Task<Rubrica?> ObtenerUltimaRubricaAsync(string id, CancellationToken cancellationToken);

    Task GuardarPromptAsync(Prompt prompt, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Prompt>> BuscarPromptsAsync(
        string? tipoPrompt,
        EstadoPrompt? estado,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Prompt>> ListarVersionesPromptAsync(
        string id,
        CancellationToken cancellationToken);

    /// <summary>
    /// Consulta administrativa: la version numericamente mas nueva, sea cual sea su estado. No usar
    /// en runtime (ver <see cref="ObtenerPromptVigenteAsync"/>).
    /// </summary>
    Task<Prompt?> ObtenerUltimoPromptAsync(string id, CancellationToken cancellationToken);

    /// <summary>
    /// DT-I20-02 §5.4: resolucion <b>de runtime</b>. Devuelve la version mas nueva de la familia que
    /// sea a la vez activa y aprobada, o el motivo fijo por el que no hay ninguna utilizable. Asi,
    /// inactivar la ultima version devuelve el flujo a la anterior vigente en vez de dejar la familia
    /// sin prompt.
    /// </summary>
    Task<ResolucionPromptRuntime> ObtenerPromptVigenteAsync(string id, CancellationToken cancellationToken);

    Task GuardarConfigLlmAsync(ConfigLlm config, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ConfigLlm>> BuscarConfigsLlmAsync(
        EstadoRegistro? estado,
        CancellationToken cancellationToken);

    Task<ConfigLlm?> ObtenerConfigLlmAsync(string id, CancellationToken cancellationToken);
}
