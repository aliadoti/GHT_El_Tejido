using ElTejido.Domain.Campanas;
using ElTejido.Domain.Common;
using ElTejido.Domain.Identidad;
using ElTejido.Domain.Participantes;
using ElTejido.Domain.Usuarios;

namespace ElTejido.UnitTests.Soporte;

/// <summary>
/// Fabricas minimas de entidades de dominio para pruebas de identidad y autenticacion.
/// </summary>
internal static class FabricasDominio
{
    private static readonly DateTimeOffset Epoca = DateTimeOffset.UnixEpoch;

    public static Usuario CrearUsuario(
        string id,
        string numero,
        RolUsuario rol = RolUsuario.Admin,
        EstadoRegistro estado = EstadoRegistro.Activo,
        string nombre = "Admin")
        => Usuario.Crear(
            id,
            nombre,
            NumeroWhatsApp.FromNormalized(numero),
            rol,
            estado,
            "Operaciones",
            "GHT",
            tags: null,
            propiedadesDinamicas: null,
            Epoca,
            Epoca);

    public static Pregunta CrearPregunta(string id, int orden, EstadoRegistro estado = EstadoRegistro.Activo)
        => Pregunta.Crear(
            id,
            $"Pregunta {orden}",
            "Instruccion",
            "categoria",
            orden,
            estado,
            rubricaRef: null,
            versionRubrica: null,
            promptRefs: null,
            maxRepreguntas: 1,
            LimitesSeguridad.ParaPregunta(1500, 2),
            ConfigMarkdown.Crear(TipoArtefactoMarkdown.Respuesta));

    public static Campania CrearCampania(
        string id,
        EstadoCampania estado,
        IEnumerable<Pregunta>? preguntas = null)
        => Campania.Crear(
            id,
            $"Campania {id}",
            "Descripcion",
            "Objetivo",
            estado,
            mensajesIniciales: null,
            preguntas ?? new[] { CrearPregunta("p_1", 1) },
            rubricaRef: "rub_1",
            promptRefs: null,
            configLlmRef: "llm_1",
            ConfigMarkdown.Crear(TipoArtefactoMarkdown.Campania),
            ConfigConversacional.Crear(1, "Gracias por participar."),
            LimitesSeguridad.Crear(1500, 10, 2),
            usuariosHabilitados: null,
            Epoca,
            Epoca);

    public static ParticipanteCampania CrearParticipante(
        string id,
        string campaniaId,
        string usuarioId,
        string numero,
        EstadoRegistro estado = EstadoRegistro.Activo,
        DateTimeOffset? fechaInclusion = null,
        DateTimeOffset? fechaUltimaRespuesta = null)
        => ParticipanteCampania.Crear(
            id,
            campaniaId,
            usuarioId,
            NumeroWhatsApp.FromNormalized(numero),
            estado,
            EstadoEnvio.Enviado,
            EstadoRespuestaParticipante.SinRespuesta,
            fechaInclusion ?? Epoca,
            fechaPrimerEnvio: null,
            fechaUltimaRespuesta);
}
