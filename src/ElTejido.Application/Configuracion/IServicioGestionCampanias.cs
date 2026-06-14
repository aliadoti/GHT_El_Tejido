using ElTejido.Application.Campanas;
using ElTejido.Application.Usuarios;
using ElTejido.Domain.Campanas;
using ElTejido.Domain.Participantes;

namespace ElTejido.Application.Configuracion;

public interface IServicioGestionCampanias
{
    Task<IReadOnlyCollection<Campania>> BuscarCampaniasAsync(FiltroCampanias filtro, CancellationToken cancellationToken);

    Task<Campania> ObtenerCampaniaAsync(string id, CancellationToken cancellationToken);

    Task<Campania> CrearCampaniaAsync(SolicitudGuardarCampania solicitud, CancellationToken cancellationToken);

    Task<Campania> ActualizarCampaniaAsync(
        string id,
        SolicitudActualizarCampania solicitud,
        CancellationToken cancellationToken);

    Task<Campania> CambiarEstadoCampaniaAsync(
        string id,
        EstadoCampania estado,
        CancellationToken cancellationToken);

    Task<Campania> DuplicarCampaniaAsync(string id, CancellationToken cancellationToken);

    Task<MensajeInicial> AgregarMensajeInicialAsync(
        string campaniaId,
        SolicitudGuardarMensajeInicial solicitud,
        CancellationToken cancellationToken);

    Task<MensajeInicial> ActualizarMensajeInicialAsync(
        string campaniaId,
        string mensajeId,
        SolicitudActualizarMensajeInicial solicitud,
        CancellationToken cancellationToken);

    Task EliminarMensajeInicialAsync(string campaniaId, string mensajeId, CancellationToken cancellationToken);

    Task<Pregunta> AgregarPreguntaAsync(
        string campaniaId,
        SolicitudGuardarPregunta solicitud,
        CancellationToken cancellationToken);

    Task<Pregunta> ActualizarPreguntaAsync(
        string campaniaId,
        string preguntaId,
        SolicitudActualizarPregunta solicitud,
        CancellationToken cancellationToken);

    Task EliminarPreguntaAsync(string campaniaId, string preguntaId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ParticipanteCampania>> ListarParticipantesAsync(
        string campaniaId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ParticipanteCampania>> AsociarParticipantesAsync(
        string campaniaId,
        SolicitudAsociarParticipantes solicitud,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ParticipantePreview>> PreviewParticipantesAsync(
        SolicitudFiltroParticipantes solicitud,
        CancellationToken cancellationToken);

    Task DesasociarParticipanteAsync(string campaniaId, string usuarioId, CancellationToken cancellationToken);
}

public sealed record SolicitudGuardarCampania(
    string Nombre,
    string Descripcion,
    string Objetivo,
    string RubricaRef,
    IReadOnlyDictionary<string, string>? PromptRefs,
    string ConfigLlmRef,
    ConfigMarkdown ConfigMarkdown,
    ConfigConversacional ConfigConversacional,
    LimitesSeguridad ConfigSeguridad);

public sealed record SolicitudActualizarCampania(
    string? Nombre,
    string? Descripcion,
    string? Objetivo,
    string? RubricaRef,
    IReadOnlyDictionary<string, string>? PromptRefs,
    string? ConfigLlmRef,
    ConfigMarkdown? ConfigMarkdown,
    ConfigConversacional? ConfigConversacional,
    LimitesSeguridad? ConfigSeguridad);

public sealed record SolicitudGuardarMensajeInicial(
    string NombreInterno,
    string Texto,
    int Orden,
    IEnumerable<string>? VariablesDinamicas,
    ElTejido.Domain.Common.EstadoRegistro Estado,
    PlantillaWhatsApp? PlantillaWhatsApp);

public sealed record SolicitudActualizarMensajeInicial(
    string? NombreInterno,
    string? Texto,
    int? Orden,
    IEnumerable<string>? VariablesDinamicas,
    ElTejido.Domain.Common.EstadoRegistro? Estado,
    PlantillaWhatsApp? PlantillaWhatsApp);

public sealed record SolicitudGuardarPregunta(
    string Texto,
    string Instruccion,
    string Categoria,
    int Orden,
    ElTejido.Domain.Common.EstadoRegistro Estado,
    string? RubricaRef,
    int? VersionRubrica,
    IReadOnlyDictionary<string, string>? PromptRefs,
    int MaxRepreguntas,
    LimitesSeguridad LimitesSeguridad,
    ConfigMarkdown ConfigMarkdown);

public sealed record SolicitudActualizarPregunta(
    string? Texto,
    string? Instruccion,
    string? Categoria,
    int? Orden,
    ElTejido.Domain.Common.EstadoRegistro? Estado,
    string? RubricaRef,
    int? VersionRubrica,
    IReadOnlyDictionary<string, string>? PromptRefs,
    int? MaxRepreguntas,
    LimitesSeguridad? LimitesSeguridad,
    ConfigMarkdown? ConfigMarkdown);

public sealed record SolicitudAsociarParticipantes(
    IReadOnlyCollection<string>? UsuarioIds,
    SolicitudFiltroParticipantes? Filtro);

public sealed record SolicitudFiltroParticipantes(
    string? Area,
    string? Empresa,
    IReadOnlyCollection<string>? Tags,
    string? Busqueda);

public sealed record ParticipantePreview(
    string UsuarioId,
    string Nombre,
    string WhatsappNormalizado,
    string Area,
    string Empresa,
    IReadOnlyCollection<string> Tags);
