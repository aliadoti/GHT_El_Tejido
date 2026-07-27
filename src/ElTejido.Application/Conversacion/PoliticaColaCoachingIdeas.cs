using ElTejido.Domain.Conversaciones;

namespace ElTejido.Application.Conversacion;

/// <summary>
/// I-18: transiciones puras de la cola de coaching. No realiza E/S ni decide calidad.
/// </summary>
public sealed class PoliticaColaCoachingIdeas
{
    public CoachingIdeas Crear(
        string respuestaPadreId,
        IEnumerable<RaizIdeaCoaching> raices,
        DateTimeOffset ahora)
    {
        var ideas = raices
            .OrderBy(raiz => raiz.IdeaIndice)
            .Select(raiz => IdeaCoaching.Crear(
                raiz.IdeaIndice,
                raiz.RespuestaId,
                raiz.RespuestaId,
                raiz.MotivoFinalizacion.HasValue ? EstadoIdeaCoaching.Finalizada : EstadoIdeaCoaching.Pendiente,
                raiz.MotivoFinalizacion,
                finalizadaEn: raiz.MotivoFinalizacion.HasValue ? ahora : null,
                ideaId: raiz.IdeaId,
                versionIdeaVigenteId: raiz.VersionIdeaVigenteId))
            .ToArray();

        var cola = CoachingIdeas.Crear(EstadoCoachingIdeas.Activo, respuestaPadreId, null, ideas);
        return ActivarSiguiente(cola, ahora);
    }

    public CoachingIdeas ActivarSiguiente(CoachingIdeas cola, DateTimeOffset ahora)
    {
        var siguiente = cola.Ideas.FirstOrDefault(idea => idea.Estado == EstadoIdeaCoaching.Pendiente);
        if (siguiente is null)
        {
            return CoachingIdeas.Crear(EstadoCoachingIdeas.Finalizado, cola.RespuestaPadreId, null, cola.Ideas);
        }

        var ideas = cola.Ideas
            .Select(idea => idea.IdeaIndice == siguiente.IdeaIndice
                ? idea with { Estado = EstadoIdeaCoaching.Activa, IniciadaEn = ahora.ToUniversalTime() }
                : idea)
            .ToArray();
        return CoachingIdeas.Crear(EstadoCoachingIdeas.Activo, cola.RespuestaPadreId, siguiente.IdeaIndice, ideas);
    }

    public CoachingIdeas RegistrarRepregunta(CoachingIdeas cola)
    {
        var activa = RequerirActiva(cola);
        var ideas = cola.Ideas
            .Select(idea => idea.IdeaIndice == activa.IdeaIndice
                ? idea with { RepreguntasUsadas = idea.RepreguntasUsadas + 1 }
                : idea)
            .ToArray();
        return CoachingIdeas.Crear(cola.Estado, cola.RespuestaPadreId, cola.IdeaActivaIndice, ideas);
    }

    public CoachingIdeas ActualizarRespuestaVigente(CoachingIdeas cola, string respuestaVigenteId)
    {
        var activa = RequerirActiva(cola);
        var ideas = cola.Ideas
            .Select(idea => idea.IdeaIndice == activa.IdeaIndice
                ? idea with { RespuestaVigenteId = respuestaVigenteId }
                : idea)
            .ToArray();
        return CoachingIdeas.Crear(cola.Estado, cola.RespuestaPadreId, cola.IdeaActivaIndice, ideas);
    }

    /// <summary>
    /// I-19: actualiza de forma atómica la versión consolidada que gobierna las decisiones
    /// de la idea activa. La respuesta vigente conserva exclusivamente el último aporte.
    /// </summary>
    public CoachingIdeas ActualizarVersionIdeaVigente(
        CoachingIdeas cola,
        string ideaId,
        string versionIdeaVigenteId)
    {
        var activa = RequerirActiva(cola);
        if (!string.IsNullOrWhiteSpace(activa.IdeaId)
            && !string.Equals(activa.IdeaId, ideaId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("La versión consolidada no pertenece a la idea activa.");
        }

        var ideaIdRequerido = RequerirTexto(ideaId, nameof(ideaId));
        var versionIdRequerida = RequerirTexto(versionIdeaVigenteId, nameof(versionIdeaVigenteId));

        var ideas = cola.Ideas
            .Select(idea => idea.IdeaIndice == activa.IdeaIndice
                ? idea with
                {
                    IdeaId = ideaIdRequerido,
                    VersionIdeaVigenteId = versionIdRequerida,
                }
                : idea)
            .ToArray();
        return CoachingIdeas.Crear(cola.Estado, cola.RespuestaPadreId, cola.IdeaActivaIndice, ideas);
    }

    /// <summary>
    /// I-19 §4.6: ¿cabe otra idea en la cola? El tope y el estado los impone el servidor, no el LLM.
    /// </summary>
    public bool PuedeAgregarIdea(CoachingIdeas cola, int maxIdeas)
        => cola.Estado == EstadoCoachingIdeas.Activo && cola.Ideas.Count < Math.Max(1, maxIdeas);

    /// <summary>Siguiente índice libre al final de la cola (el orden es de llegada).</summary>
    public int SiguienteIndice(CoachingIdeas cola)
        => cola.Ideas.Max(idea => idea.IdeaIndice) + 1;

    /// <summary>
    /// I-19 §4.6: encola al final una idea nueva detectada durante el coaching, sin tocar la activa.
    /// Es idempotente: una idea ya encolada (mismo <c>ideaId</c> o misma raíz), una cola llena o una
    /// cola finalizada devuelven la cola sin cambios.
    /// </summary>
    public CoachingIdeas AgregarIdeaPendiente(CoachingIdeas cola, RaizIdeaCoaching raiz, int maxIdeas)
    {
        var yaEncolada = cola.Ideas.Any(idea =>
            string.Equals(idea.RespuestaRaizId, raiz.RespuestaId, StringComparison.Ordinal)
            || (idea.IdeaId is not null && string.Equals(idea.IdeaId, raiz.IdeaId, StringComparison.Ordinal)));
        if (yaEncolada || !PuedeAgregarIdea(cola, maxIdeas))
        {
            return cola;
        }

        var ideas = cola.Ideas
            .Append(IdeaCoaching.Crear(
                SiguienteIndice(cola),
                raiz.RespuestaId,
                raiz.RespuestaId,
                EstadoIdeaCoaching.Pendiente,
                ideaId: raiz.IdeaId,
                versionIdeaVigenteId: raiz.VersionIdeaVigenteId))
            .ToArray();
        return CoachingIdeas.Crear(cola.Estado, cola.RespuestaPadreId, cola.IdeaActivaIndice, ideas);
    }

    public CoachingIdeas FinalizarActiva(
        CoachingIdeas cola,
        MotivoFinalizacionIdea motivo,
        DateTimeOffset ahora)
    {
        var activa = RequerirActiva(cola);
        var ideas = cola.Ideas
            .Select(idea => idea.IdeaIndice == activa.IdeaIndice
                ? idea with
                {
                    Estado = EstadoIdeaCoaching.Finalizada,
                    MotivoFinalizacion = motivo,
                    FinalizadaEn = ahora.ToUniversalTime(),
                }
                : idea)
            .ToArray();
        return ActivarSiguiente(
            CoachingIdeas.Crear(EstadoCoachingIdeas.Activo, cola.RespuestaPadreId, null, ideas),
            ahora);
    }

    public CoachingIdeas FinalizarTodasAbiertas(
        CoachingIdeas cola,
        MotivoFinalizacionIdea motivo,
        DateTimeOffset ahora)
    {
        var ideas = cola.Ideas
            .Select(idea => idea.Estado == EstadoIdeaCoaching.Finalizada
                ? idea
                : idea with
                {
                    Estado = EstadoIdeaCoaching.Finalizada,
                    MotivoFinalizacion = motivo,
                    FinalizadaEn = ahora.ToUniversalTime(),
                })
            .ToArray();
        return CoachingIdeas.Crear(EstadoCoachingIdeas.Finalizado, cola.RespuestaPadreId, null, ideas);
    }

    private static IdeaCoaching RequerirActiva(CoachingIdeas cola)
        => cola.IdeaActiva ?? throw new InvalidOperationException("La cola no tiene una idea activa.");

    private static string RequerirTexto(string? valor, string nombre)
        => !string.IsNullOrWhiteSpace(valor)
            ? valor
            : throw new ArgumentException("El valor es obligatorio.", nombre);
}

public sealed record RaizIdeaCoaching(
    int IdeaIndice,
    string RespuestaId,
    MotivoFinalizacionIdea? MotivoFinalizacion,
    string? IdeaId = null,
    string? VersionIdeaVigenteId = null);
