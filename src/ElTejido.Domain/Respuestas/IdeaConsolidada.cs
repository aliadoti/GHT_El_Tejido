using ElTejido.Domain.Common;

namespace ElTejido.Domain.Respuestas;

/// <summary>
/// Unidad lógica de I-19. Los aportes y las versiones permanecen inmutables; este documento solo
/// mantiene los punteros y el estado vigente que permiten evaluar una idea completa una sola vez.
/// </summary>
public sealed class IdeaConsolidada
{
    private IdeaConsolidada(
        string id,
        string campaniaId,
        string usuarioId,
        string preguntaId,
        string conversacionId,
        string respuestaRaizId,
        int ideaIndice,
        string? versionConfirmadaRef,
        string? versionPropuestaRef,
        string? evaluacionVigenteRef,
        EstadoFlujoIdeaConsolidada estadoFlujo,
        EstadoResultadoIdeaConsolidada? estadoResultado,
        NivelMadurez nivelMadurez,
        EstadoCuraduriaIdea? estadoCuraduria,
        string? motivoCierre,
        DateTimeOffset creadaEn,
        DateTimeOffset actualizadaEn)
    {
        Id = id;
        CampaniaId = campaniaId;
        UsuarioId = usuarioId;
        PreguntaId = preguntaId;
        ConversacionId = conversacionId;
        RespuestaRaizId = respuestaRaizId;
        IdeaIndice = ideaIndice;
        VersionConfirmadaRef = versionConfirmadaRef;
        VersionPropuestaRef = versionPropuestaRef;
        EvaluacionVigenteRef = evaluacionVigenteRef;
        EstadoFlujo = estadoFlujo;
        EstadoResultado = estadoResultado;
        NivelMadurez = nivelMadurez;
        EstadoCuraduria = estadoCuraduria;
        MotivoCierre = motivoCierre;
        CreadaEn = creadaEn;
        ActualizadaEn = actualizadaEn;
    }

    public string Id { get; }
    public string CampaniaId { get; }
    public string UsuarioId { get; }
    public string PreguntaId { get; }
    public string ConversacionId { get; }
    public string RespuestaRaizId { get; }
    public int IdeaIndice { get; }
    public string? VersionConfirmadaRef { get; }
    public string? VersionPropuestaRef { get; }
    public string? EvaluacionVigenteRef { get; }
    public EstadoFlujoIdeaConsolidada EstadoFlujo { get; }
    public EstadoResultadoIdeaConsolidada? EstadoResultado { get; }
    public NivelMadurez NivelMadurez { get; }
    public EstadoCuraduriaIdea? EstadoCuraduria { get; }
    public string? MotivoCierre { get; }
    public DateTimeOffset CreadaEn { get; }
    public DateTimeOffset ActualizadaEn { get; }

    public static IdeaConsolidada Crear(
        string id, string campaniaId, string usuarioId, string preguntaId, string conversacionId,
        string respuestaRaizId, int ideaIndice, DateTimeOffset creadaEn)
        => CrearEstado(
            id, campaniaId, usuarioId, preguntaId, conversacionId, respuestaRaizId, ideaIndice,
            null, null, null, EstadoFlujoIdeaConsolidada.PendienteConfirmacion, null,
            NivelMadurez.Incubacion, null, null, creadaEn, creadaEn);

    public static IdeaConsolidada Restaurar(
        string id, string campaniaId, string usuarioId, string preguntaId, string conversacionId,
        string respuestaRaizId, int ideaIndice, string? versionConfirmadaRef, string? versionPropuestaRef,
        string? evaluacionVigenteRef, EstadoFlujoIdeaConsolidada estadoFlujo,
        EstadoResultadoIdeaConsolidada? estadoResultado, NivelMadurez nivelMadurez,
        EstadoCuraduriaIdea? estadoCuraduria, string? motivoCierre, DateTimeOffset creadaEn,
        DateTimeOffset actualizadaEn)
        => CrearEstado(id, campaniaId, usuarioId, preguntaId, conversacionId, respuestaRaizId, ideaIndice,
            versionConfirmadaRef, versionPropuestaRef, evaluacionVigenteRef, estadoFlujo, estadoResultado,
            nivelMadurez, estadoCuraduria, motivoCierre, creadaEn, actualizadaEn);

    public IdeaConsolidada ConPropuesta(string versionId, DateTimeOffset ahora)
        => CrearEstado(Id, CampaniaId, UsuarioId, PreguntaId, ConversacionId, RespuestaRaizId, IdeaIndice,
            VersionConfirmadaRef, DomainGuards.Required(versionId, nameof(versionId)), EvaluacionVigenteRef,
            EstadoFlujoIdeaConsolidada.PendienteConfirmacion, EstadoResultado, NivelMadurez,
            EstadoCuraduria, MotivoCierre, CreadaEn, ahora);

    public IdeaConsolidada ConfirmarVersion(string versionId, DateTimeOffset ahora)
        => CrearEstado(Id, CampaniaId, UsuarioId, PreguntaId, ConversacionId, RespuestaRaizId, IdeaIndice,
            DomainGuards.Required(versionId, nameof(versionId)), null, null,
            EstadoFlujoIdeaConsolidada.EnMejora, null, NivelMadurez.Incubacion, null, null, CreadaEn, ahora);

    public IdeaConsolidada Cerrar(
        EstadoResultadoIdeaConsolidada resultado, string? evaluacionId, string motivo, DateTimeOffset ahora)
    {
        var esMadura = resultado == EstadoResultadoIdeaConsolidada.Madura;
        return CrearEstado(Id, CampaniaId, UsuarioId, PreguntaId, ConversacionId, RespuestaRaizId, IdeaIndice,
            VersionConfirmadaRef, null, evaluacionId, EstadoFlujoIdeaConsolidada.Cerrada, resultado,
            esMadura ? NivelMadurez.Maduro : NivelMadurez.Incubacion,
            esMadura ? EstadoCuraduriaIdea.Pendiente : null, DomainGuards.Required(motivo, nameof(motivo)),
            CreadaEn, ahora);
    }

    public IdeaConsolidada Reabrir(DateTimeOffset ahora)
        => CrearEstado(Id, CampaniaId, UsuarioId, PreguntaId, ConversacionId, RespuestaRaizId, IdeaIndice,
            VersionConfirmadaRef, null, null, EstadoFlujoIdeaConsolidada.EnRevision, null,
            NivelMadurez.Incubacion, null, null, CreadaEn, ahora);

    private static IdeaConsolidada CrearEstado(
        string id, string campaniaId, string usuarioId, string preguntaId, string conversacionId,
        string respuestaRaizId, int ideaIndice, string? versionConfirmadaRef, string? versionPropuestaRef,
        string? evaluacionVigenteRef, EstadoFlujoIdeaConsolidada estadoFlujo,
        EstadoResultadoIdeaConsolidada? estadoResultado, NivelMadurez nivelMadurez,
        EstadoCuraduriaIdea? estadoCuraduria, string? motivoCierre, DateTimeOffset creadaEn,
        DateTimeOffset actualizadaEn)
    {
        if (ideaIndice <= 0)
        {
            throw new DomainValidationException("IDEA_INDICE_INVALIDO", "El índice de idea debe ser mayor que cero.");
        }

        if (estadoResultado == EstadoResultadoIdeaConsolidada.Madura
            && (nivelMadurez != NivelMadurez.Maduro || estadoCuraduria != EstadoCuraduriaIdea.Pendiente))
        {
            throw new DomainValidationException("IDEA_MADURA_INVALIDA", "Una idea madura debe quedar pendiente de curaduría.");
        }

        if (estadoResultado is not null && estadoFlujo != EstadoFlujoIdeaConsolidada.Cerrada)
        {
            throw new DomainValidationException("RESULTADO_IDEA_SIN_CIERRE", "El resultado de una idea solo se asigna al cerrarla.");
        }

        return new IdeaConsolidada(
            DomainGuards.Required(id, nameof(id)), DomainGuards.Required(campaniaId, nameof(campaniaId)),
            DomainGuards.Required(usuarioId, nameof(usuarioId)), DomainGuards.Required(preguntaId, nameof(preguntaId)),
            DomainGuards.Required(conversacionId, nameof(conversacionId)),
            DomainGuards.Required(respuestaRaizId, nameof(respuestaRaizId)), ideaIndice,
            Normalizar(versionConfirmadaRef), Normalizar(versionPropuestaRef), Normalizar(evaluacionVigenteRef),
            estadoFlujo, estadoResultado, nivelMadurez, estadoCuraduria, Normalizar(motivoCierre),
            creadaEn.ToUniversalTime(), actualizadaEn.ToUniversalTime());
    }

    private static string? Normalizar(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
