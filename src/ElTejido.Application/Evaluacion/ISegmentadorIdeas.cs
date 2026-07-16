using ElTejido.Domain.Campanas;
using ElTejido.Domain.Configuracion;
using ElTejido.Domain.Evaluacion;

namespace ElTejido.Application.Evaluacion;

/// <summary>
/// I-06: puerto previo a la evaluacion que separa ideas explicitas de un mensaje. Su salida sigue
/// siendo dato no confiable: el orquestador aplica limites, deduplicacion y fallback deterministas.
/// </summary>
public interface ISegmentadorIdeas
{
    Task<ResultadoSegmentacionIdeas> SegmentarAsync(
        ContextoSegmentacionIdeas contexto,
        CancellationToken cancellationToken);
}

public sealed record ContextoSegmentacionIdeas(
    Campania Campania,
    Pregunta Pregunta,
    string Texto,
    IReadOnlyList<string> HistorialReciente,
    ConfigLlm ConfigLlmSnapshot);

public sealed record IdeaSegmentada(int Indice, string Texto, string? Resumen);

public abstract record ResultadoSegmentacionIdeas(UsoTokensLlm? Uso)
{
    public sealed record Exito(IReadOnlyList<IdeaSegmentada> Ideas, UsoTokensLlm? Uso)
        : ResultadoSegmentacionIdeas(Uso);

    public sealed record Fallback(string Motivo, UsoTokensLlm? Uso)
        : ResultadoSegmentacionIdeas(Uso);
}
