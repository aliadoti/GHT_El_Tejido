using ElTejido.Application.Respuestas;
using ElTejido.Application.Seguridad;
using ElTejido.Domain.Campanas;

namespace ElTejido.Application.Conversacion;

/// <summary>
/// P-10/DT-P33-01: única fórmula para el cupo de llamadas y presupuesto de tokens LLM. Evaluación,
/// consolidación y clasificación comparten los mismos rastros persistidos y la misma ventana móvil.
/// </summary>
public sealed class GuardaCuposLlm
{
    private static readonly TimeSpan VentanaContinua = TimeSpan.FromHours(24);
    private readonly IRepositorioRespuestas _respuestas;
    private readonly IRepositorioLogSeguridad _logSeguridad;

    public GuardaCuposLlm(IRepositorioRespuestas respuestas, IRepositorioLogSeguridad logSeguridad)
    {
        _respuestas = respuestas;
        _logSeguridad = logSeguridad;
    }

    public async Task<string?> MotivoAsync(
        Campania campania,
        string usuarioId,
        DateTimeOffset ahora,
        bool incluirConsolidaciones,
        CancellationToken cancellationToken)
    {
        if (await CupoLlamadasExcedidoAsync(
                campania, usuarioId, ahora, incluirConsolidaciones, cancellationToken))
        {
            return "cupo_llamadas_llm_usuario";
        }

        return await PresupuestoTokensExcedidoAsync(campania, cancellationToken)
            ? "presupuesto_tokens_campania"
            : null;
    }

    public async Task<bool> CupoLlamadasExcedidoAsync(
        Campania campania,
        string usuarioId,
        DateTimeOffset ahora,
        bool incluirConsolidaciones,
        CancellationToken cancellationToken)
    {
        var maximo = campania.ConfigSeguridad.MaxLlamadasLlmPorUsuario;
        if (maximo <= 0)
        {
            return false;
        }

        var desde = campania.ConfigConversacional.ParticipacionContinua
            ? ahora.ToUniversalTime().Subtract(VentanaContinua)
            : (DateTimeOffset?)null;
        var evaluaciones = desde is null
            ? await _respuestas.ContarEvaluacionesUsuarioAsync(campania.Id, usuarioId, cancellationToken)
            : await _respuestas.ContarEvaluacionesUsuarioAsync(campania.Id, usuarioId, desde.Value, cancellationToken);
        var consolidaciones = !incluirConsolidaciones
            ? 0
            : desde is null
                ? await _respuestas.ContarConsolidacionesUsuarioAsync(campania.Id, usuarioId, cancellationToken)
                : await _respuestas.ContarConsolidacionesUsuarioAsync(campania.Id, usuarioId, desde.Value, cancellationToken);
        var clasificaciones = desde is null
            ? await _logSeguridad.ContarClasificacionesIntencionControlUsuarioAsync(
                campania.Id, usuarioId, cancellationToken)
            : await _logSeguridad.ContarClasificacionesIntencionControlUsuarioAsync(
                campania.Id, usuarioId, desde.Value, cancellationToken);
        return evaluaciones + consolidaciones + clasificaciones >= maximo;
    }

    public async Task<bool> PresupuestoTokensExcedidoAsync(
        Campania campania,
        CancellationToken cancellationToken)
    {
        var presupuesto = campania.ConfigSeguridad.PresupuestoTokensCampania;
        if (presupuesto <= 0)
        {
            return false;
        }

        var consumidos = await _respuestas.SumarTokensCampaniaAsync(campania.Id, cancellationToken)
            + await _logSeguridad.SumarTokensClasificacionesIntencionControlCampaniaAsync(
                campania.Id, cancellationToken);
        return consumidos >= presupuesto;
    }
}
