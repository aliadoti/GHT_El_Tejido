using ElTejido.Domain.Configuracion;
using ElTejido.Domain.Conversaciones;
using ElTejido.Domain.Evaluacion;

namespace ElTejido.Application.Conversacion;

/// <summary>
/// P-27: puerto interno para interpretar un mensaje corto de control. Solo devuelve un candidato
/// cerrado; la política y el orquestador conservan la autoridad de validar y ejecutar transiciones.
/// </summary>
public interface IClasificadorIntencionControl
{
    Task<ResultadoClasificacionIntencionControl> ClasificarAsync(
        ContextoClasificacionIntencionControl contexto,
        CancellationToken cancellationToken);
}

public enum ActoPrevioIntencionControl
{
    Mejorar,
    Aclarar,
    Confirmar,
}

public enum IntencionControl
{
    Aportar,
    FinalizarIdea,
    FinalizarParticipacion,
    Ambigua,
}

/// <summary>
/// Contexto mínimo para P-27. No lleva rúbrica, versiones de ideas, campañas, preguntas ni datos de
/// terceros; el texto entrante siempre se trata como dato no confiable.
/// </summary>
public sealed record ContextoClasificacionIntencionControl(
    EstadoMaquinaConversacion EstadoConversacion,
    ActoPrevioIntencionControl ActoPrevio,
    bool HayIdeaActiva,
    bool QuedanUnidadesPendientes,
    string? Idioma,
    string TextoEntrante,
    ConfigLlm? ConfigLlmSnapshot);

public abstract record ResultadoClasificacionIntencionControl(UsoTokensLlm? Uso)
{
    public sealed record Exito(IntencionControl Intencion, UsoTokensLlm? Uso)
        : ResultadoClasificacionIntencionControl(Uso);

    public sealed record Fallback(string Motivo, UsoTokensLlm? Uso)
        : ResultadoClasificacionIntencionControl(Uso);
}
