using ElTejido.Domain.Configuracion;

namespace ElTejido.Application.Configuracion;

/// <summary>
/// Resultado de resolver el prompt que usará una conversación en curso (DT-I20-02 §5.4).
/// <see cref="Motivo"/> es un código fijo de baja cardinalidad para la telemetría existente; nunca
/// contiene contenido del prompt.
/// </summary>
public sealed record ResolucionPromptRuntime(Prompt? Prompt, string? Motivo)
{
    /// <summary>No existe ninguna version de esa familia.</summary>
    public const string MotivoNoEncontrado = "prompt_no_encontrado";

    /// <summary>Hay versiones, pero la mas nueva no esta activa y ninguna anterior es vigente.</summary>
    public const string MotivoNoActivo = "prompt_no_activo";

    /// <summary>Hay una version activa, pero sin aprobar, y ninguna anterior es vigente.</summary>
    public const string MotivoNoAprobado = "prompt_no_aprobado";

    public static ResolucionPromptRuntime Exito(Prompt prompt) => new(prompt, null);

    public static ResolucionPromptRuntime Fallo(string motivo) => new(null, motivo);
}

/// <summary>
/// DT-I20-02 §5.4: política <b>pura</b> de selección del prompt de runtime. Devuelve la versión
/// <b>más nueva</b> de la familia que sea simultáneamente <b>activa y aprobada</b>.
/// <para>
/// Antes se tomaba la versión numéricamente mayor y solo después se comprobaba su estado, de modo que
/// inactivar la última versión dejaba la familia sin prompt utilizable en vez de volver a la anterior:
/// «inactivar la última» no era un rollback confiable (§2). La consulta administrativa de «última
/// versión» (<c>ObtenerUltimoPromptAsync</c>) conserva su semántica y sigue mostrando la más nueva,
/// sea cual sea su estado.
/// </para>
/// </summary>
public static class ResolutorPromptRuntime
{
    /// <summary>
    /// Elige la versión vigente más nueva. Si no hay ninguna, devuelve el motivo diagnóstico de la
    /// versión más nueva —la que el administrador ve en el portal—, conservando los códigos que ya
    /// usaba el orquestador.
    /// </summary>
    public static ResolucionPromptRuntime Resolver(IEnumerable<Prompt>? versiones)
    {
        // No se confía en el orden de la fuente: la política ordena por versión descendente.
        var ordenadas = (versiones ?? Array.Empty<Prompt>()).OrderByDescending(p => p.Version).ToArray();
        if (ordenadas.Length == 0)
        {
            return ResolucionPromptRuntime.Fallo(ResolucionPromptRuntime.MotivoNoEncontrado);
        }

        var vigente = Array.Find(ordenadas, p => p.EsVigenteParaRuntime);
        if (vigente is not null)
        {
            return ResolucionPromptRuntime.Exito(vigente);
        }

        // Sin ninguna vigente, el diagnóstico describe la más nueva: es la que el operador acaba de
        // tocar y la que explica por qué la familia entera quedó sin prompt utilizable.
        var masNueva = ordenadas[0];
        return ResolucionPromptRuntime.Fallo(
            masNueva.Estado == EstadoPrompt.Activo
                ? ResolucionPromptRuntime.MotivoNoAprobado
                : ResolucionPromptRuntime.MotivoNoActivo);
    }
}
