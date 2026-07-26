using ElTejido.Domain.Common;

namespace ElTejido.Domain.Conversaciones;

/// <summary>
/// I-18: estado server-side de la cola de coaching de un mensaje multi-idea (03 §3.6).
/// Es inmutable y solo permite una idea activa.
/// </summary>
public sealed class CoachingIdeas
{
    private CoachingIdeas(
        EstadoCoachingIdeas estado,
        string respuestaPadreId,
        int? ideaActivaIndice,
        IReadOnlyList<IdeaCoaching> ideas)
    {
        Estado = estado;
        RespuestaPadreId = respuestaPadreId;
        IdeaActivaIndice = ideaActivaIndice;
        Ideas = ideas;
    }

    public EstadoCoachingIdeas Estado { get; }

    public string RespuestaPadreId { get; }

    public int? IdeaActivaIndice { get; }

    public IReadOnlyList<IdeaCoaching> Ideas { get; }

    public IdeaCoaching? IdeaActiva =>
        IdeaActivaIndice is null ? null : Ideas.SingleOrDefault(idea => idea.IdeaIndice == IdeaActivaIndice);

    public static CoachingIdeas Crear(
        EstadoCoachingIdeas estado,
        string respuestaPadreId,
        int? ideaActivaIndice,
        IEnumerable<IdeaCoaching> ideas)
    {
        var lista = ideas?.OrderBy(idea => idea.IdeaIndice).ToArray()
            ?? throw new DomainValidationException("COACHING_IDEAS_INVALIDO", "La cola de ideas es obligatoria.");

        if (lista.Length == 0 || lista.Select(idea => idea.IdeaIndice).Distinct().Count() != lista.Length)
        {
            throw new DomainValidationException(
                "COACHING_IDEAS_INVALIDO",
                "La cola debe contener ideas con indices unicos.");
        }

        var activas = lista.Where(idea => idea.Estado == EstadoIdeaCoaching.Activa).ToArray();
        if (activas.Length > 1
            || (ideaActivaIndice is null) != (activas.Length == 0)
            || (ideaActivaIndice is not null && activas.Single().IdeaIndice != ideaActivaIndice))
        {
            throw new DomainValidationException(
                "COACHING_IDEA_ACTIVA_INVALIDA",
                "La cola debe tener como maximo una idea activa y su indice debe coincidir.");
        }

        if (estado == EstadoCoachingIdeas.Finalizado
            && (activas.Length > 0 || lista.Any(idea => idea.Estado != EstadoIdeaCoaching.Finalizada)))
        {
            throw new DomainValidationException(
                "COACHING_FINALIZADO_INVALIDO",
                "Una cola finalizada no puede conservar ideas activas o pendientes.");
        }

        return new CoachingIdeas(
            estado,
            DomainGuards.Required(respuestaPadreId, nameof(respuestaPadreId)),
            ideaActivaIndice,
            lista);
    }
}

public sealed record IdeaCoaching(
    int IdeaIndice,
    string RespuestaRaizId,
    string RespuestaVigenteId,
    EstadoIdeaCoaching Estado,
    MotivoFinalizacionIdea? MotivoFinalizacion,
    int RepreguntasUsadas,
    DateTimeOffset? IniciadaEn,
    DateTimeOffset? FinalizadaEn)
{
    public static IdeaCoaching Crear(
        int ideaIndice,
        string respuestaRaizId,
        string respuestaVigenteId,
        EstadoIdeaCoaching estado,
        MotivoFinalizacionIdea? motivoFinalizacion = null,
        int repreguntasUsadas = 0,
        DateTimeOffset? iniciadaEn = null,
        DateTimeOffset? finalizadaEn = null)
    {
        if (ideaIndice <= 0 || repreguntasUsadas < 0)
        {
            throw new DomainValidationException(
                "COACHING_IDEA_INVALIDA",
                "El indice debe ser positivo y las repreguntas no pueden ser negativas.");
        }

        if ((estado == EstadoIdeaCoaching.Finalizada) != motivoFinalizacion.HasValue)
        {
            throw new DomainValidationException(
                "COACHING_FINALIZACION_INVALIDA",
                "Una idea finalizada requiere motivo y una idea abierta no puede tenerlo.");
        }

        return new IdeaCoaching(
            ideaIndice,
            DomainGuards.Required(respuestaRaizId, nameof(respuestaRaizId)),
            DomainGuards.Required(respuestaVigenteId, nameof(respuestaVigenteId)),
            estado,
            motivoFinalizacion,
            repreguntasUsadas,
            iniciadaEn?.ToUniversalTime(),
            finalizadaEn?.ToUniversalTime());
    }
}

public enum EstadoCoachingIdeas
{
    Activo,
    Finalizado,
}

public enum EstadoIdeaCoaching
{
    Pendiente,
    Activa,
    Finalizada,
}

public enum MotivoFinalizacionIdea
{
    Umbral,
    Participante,
    Rechazo,
    MaxRevisiones,
    Tiempo,
    Fallback,
    Desactivacion,
}
