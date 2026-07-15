using ElTejido.Application.Evaluacion;
using ElTejido.Domain.Evaluacion;
using DominioEvaluacion = ElTejido.Domain.Evaluacion.Evaluacion;

namespace ElTejido.UnitTests.Calibracion;

/// <summary>
/// Fábrica de <see cref="ResultadoEvaluacion"/> para las pruebas del agregador/comparador (D5): arma
/// evaluaciones de dominio deterministas sin LLM real, para verificar la lógica pura en CI.
/// </summary>
internal static class FabricaEvaluacion
{
    private static readonly ConfigLlmSnapshot Snapshot =
        new("OpenAI", "gpt-4o-mini", "https://api.example/v1", new Dictionary<string, object?>());

    public static ResultadoEvaluacion Exito(
        decimal total,
        RecomendacionEvaluacion recomendacion,
        (string Criterio, decimal Puntaje)[]? criterios = null,
        string[]? temas = null,
        string[]? entidades = null,
        UsoTokensLlm? uso = null)
    {
        var calificaciones = (criterios ?? Array.Empty<(string, decimal)>())
            .Select(c => CalificacionCriterio.Crear(c.Criterio, c.Puntaje, "just"))
            .ToArray();

        var evaluacion = DominioEvaluacion.Crear(
            "eval_" + Guid.NewGuid().ToString("N"),
            "camp1",
            "resp_" + Guid.NewGuid().ToString("N"),
            "user1",
            "preg1",
            "r_general",
            1,
            "pr_eval",
            1,
            "llm_default",
            Snapshot,
            null,
            calificaciones,
            total,
            "explicacion",
            "retro",
            recomendacion,
            recomendacion == RecomendacionEvaluacion.Repreguntar ? "¿Puedes concretar?" : null,
            temas,
            entidades,
            anomaliaSeguridad: false,
            DateTimeOffset.UnixEpoch,
            uso);

        return new ResultadoEvaluacion.Exito(evaluacion);
    }

    public static ResultadoEvaluacion Fallback(string motivo, UsoTokensLlm? uso = null)
    {
        var evaluacion = DominioEvaluacion.Crear(
            "eval_" + Guid.NewGuid().ToString("N"),
            "camp1",
            "resp_" + Guid.NewGuid().ToString("N"),
            "user1",
            "preg1",
            "r_general",
            1,
            "pr_eval",
            1,
            "llm_default",
            Snapshot,
            null,
            Array.Empty<CalificacionCriterio>(),
            0m,
            "fallback: " + motivo,
            "Gracias, registramos tu aporte.",
            RecomendacionEvaluacion.Cerrar,
            null,
            null,
            null,
            anomaliaSeguridad: false,
            DateTimeOffset.UnixEpoch,
            uso);

        return new ResultadoEvaluacion.Fallback(evaluacion, motivo);
    }
}
