using ElTejido.Domain.Campanas;
using ElTejido.Domain.Configuracion;
using ElTejido.Domain.Evaluacion;
using ElTejido.Domain.Respuestas;

namespace ElTejido.Application.Evaluacion;

/// <summary>Puerto I-19 para proponer una versión completa antes de que el participante la confirme.</summary>
public interface IConsolidadorIdeas
{
    Task<ResultadoConsolidacionIdeas> ConsolidarAsync(
        ContextoConsolidacionIdeas contexto,
        CancellationToken cancellationToken);
}

public sealed record ContextoConsolidacionIdeas(
    Campania Campania,
    Pregunta Pregunta,
    string? TextoConfirmadoAnterior,
    string NuevoAporte,
    ConfigLlm ConfigLlmSnapshot,
    int MaxCaracteresPropuesta,
    int MaxIdeasPorMensaje);

public sealed record NuevaIdeaDetectada(string Texto);

public abstract record ResultadoConsolidacionIdeas(UsoTokensLlm? Uso)
{
    public sealed record Exito(
        string TextoConsolidado,
        TipoAporteIdea TipoCambio,
        IReadOnlyList<NuevaIdeaDetectada> NuevasIdeas,
        bool RequiereAclaracion,
        string? PreguntaAclaracion,
        bool AnomaliaSeguridad,
        UsoTokensLlm? Uso) : ResultadoConsolidacionIdeas(Uso);

    /// <summary>El texto es conservador: no es una paráfrasis del modelo ni se evalúa sin confirmación.</summary>
    public sealed record Fallback(string TextoConservador, string Motivo, UsoTokensLlm? Uso)
        : ResultadoConsolidacionIdeas(Uso);
}
