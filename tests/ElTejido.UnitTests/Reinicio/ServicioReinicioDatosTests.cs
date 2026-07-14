using ElTejido.Application.Campanas;
using ElTejido.Application.Common;
using ElTejido.Application.Reinicio;
using ElTejido.Application.Seguridad;
using ElTejido.Domain.Campanas;
using ElTejido.Domain.Common;
using ElTejido.Domain.Conversaciones;
using ElTejido.Domain.Evaluacion;
using ElTejido.Domain.Identidad;
using ElTejido.Domain.Participantes;
using ElTejido.Domain.Respuestas;
using ElTejido.Domain.Seguridad;
using ElTejido.Infrastructure.Blob;
using ElTejido.Infrastructure.Persistencia.Memoria;
using ElTejido.UnitTests.Soporte;
using FluentAssertions;
using NSubstitute;
using DominioConversacion = ElTejido.Domain.Conversaciones.Conversacion;
using DominioEvaluacion = ElTejido.Domain.Evaluacion.Evaluacion;

namespace ElTejido.UnitTests.Reinicio;

/// <summary>
/// P-03 — casos de uso de <see cref="ServicioReinicioDatos"/>: alcance correcto, conteos,
/// idempotencia, reset de participantes (con y sin <c>reiniciarEnvios</c>) y conservacion de la
/// campania. Usa los repos in-memory reales para ejercer el borrado de punta a punta.
/// </summary>
public sealed class ServicioReinicioDatosTests
{
    private static readonly DateTimeOffset Epoca = DateTimeOffset.UnixEpoch;

    private readonly RepositorioConversacionesMemoria _conversaciones = new();
    private readonly RepositorioRespuestasMemoria _respuestas = new();
    private readonly RepositorioParticipantesMemoria _participantes = new();
    private readonly AlmacenBlobMemoria _blob = new();
    private readonly IRepositorioCampanias _campanias = Substitute.For<IRepositorioCampanias>();
    private readonly IRepositorioLogSeguridad _log = Substitute.For<IRepositorioLogSeguridad>();
    private readonly IProveedorCorrelacion _correlacion = Substitute.For<IProveedorCorrelacion>();

    public ServicioReinicioDatosTests()
    {
        _campanias.ObtenerCampaniaPorIdAsync("c_1", Arg.Any<CancellationToken>())
            .Returns(FabricasDominio.CrearCampania("c_1", EstadoCampania.Activa));
        _correlacion.CorrelationIdActual.Returns("corr_test");
    }

    private ServicioReinicioDatos CrearServicio()
        => new(_campanias, _participantes, _conversaciones, _respuestas, _blob, _log, _correlacion, TimeProvider.System);

    [Fact]
    public async Task ReiniciarParticipante_BorraSoloElAlcanceYConservaLosDemas()
    {
        await SembrarDatosAsync("c_1", "u_1");
        await SembrarDatosAsync("c_1", "u_2");
        await SembrarParticipanteAsync("c_1", "u_1", EstadoEnvio.Enviado);

        var reporte = await CrearServicio().ReiniciarParticipanteAsync("c_1", "u_1", reiniciarEnvios: false, CancellationToken.None);

        reporte.Conversaciones.Should().Be(1);
        reporte.Mensajes.Should().Be(1);
        reporte.Respuestas.Should().Be(1);
        reporte.Evaluaciones.Should().Be(1);
        reporte.Artefactos.Should().Be(1);
        reporte.BlobsBorrados.Should().Be(1);
        reporte.BlobsFallidos.Should().Be(0);
        reporte.ParticipantesReseteados.Should().Be(1);

        (await _respuestas.ListarRespuestasAsync("c_1", CancellationToken.None)).Should().ContainSingle()
            .Which.UsuarioId.Should().Be("u_2");
        (await _conversaciones.ListarConversacionesAsync("c_1", CancellationToken.None)).Should().ContainSingle()
            .Which.UsuarioId.Should().Be("u_2");
    }

    [Fact]
    public async Task ReiniciarParticipante_SinReiniciarEnvios_ConservaEstadoEnvio()
    {
        await SembrarParticipanteAsync("c_1", "u_1", EstadoEnvio.Enviado, conFechas: true);

        await CrearServicio().ReiniciarParticipanteAsync("c_1", "u_1", reiniciarEnvios: false, CancellationToken.None);

        var participante = await _participantes.ObtenerParticipantePorUsuarioAsync("c_1", "u_1", CancellationToken.None);
        participante!.EstadoEnvio.Should().Be(EstadoEnvio.Enviado);
        participante.FechaPrimerEnvio.Should().NotBeNull();
        participante.EstadoRespuesta.Should().Be(EstadoRespuestaParticipante.SinRespuesta);
        participante.FechaUltimaRespuesta.Should().BeNull();
    }

    [Fact]
    public async Task ReiniciarParticipante_ConReiniciarEnvios_ReseteaEstadoEnvio()
    {
        await SembrarParticipanteAsync("c_1", "u_1", EstadoEnvio.Enviado, conFechas: true);

        await CrearServicio().ReiniciarParticipanteAsync("c_1", "u_1", reiniciarEnvios: true, CancellationToken.None);

        var participante = await _participantes.ObtenerParticipantePorUsuarioAsync("c_1", "u_1", CancellationToken.None);
        participante!.EstadoEnvio.Should().Be(EstadoEnvio.Pendiente);
        participante.FechaPrimerEnvio.Should().BeNull();
    }

    [Fact]
    public async Task ReiniciarParticipante_SegundaLlamada_EsIdempotente()
    {
        await SembrarDatosAsync("c_1", "u_1");
        await SembrarParticipanteAsync("c_1", "u_1", EstadoEnvio.Enviado);
        var servicio = CrearServicio();

        await servicio.ReiniciarParticipanteAsync("c_1", "u_1", reiniciarEnvios: false, CancellationToken.None);
        var segunda = await servicio.ReiniciarParticipanteAsync("c_1", "u_1", reiniciarEnvios: false, CancellationToken.None);

        segunda.Conversaciones.Should().Be(0);
        segunda.Respuestas.Should().Be(0);
        segunda.Evaluaciones.Should().Be(0);
        segunda.Artefactos.Should().Be(0);
        // El participante sigue existiendo y se cuenta como reseteado.
        segunda.ParticipantesReseteados.Should().Be(1);
    }

    [Fact]
    public async Task ReiniciarCampania_SinUsuarios_BorraTodosYResetea()
    {
        await SembrarDatosAsync("c_1", "u_1");
        await SembrarDatosAsync("c_1", "u_2");
        await SembrarParticipanteAsync("c_1", "u_1", EstadoEnvio.Enviado);
        await SembrarParticipanteAsync("c_1", "u_2", EstadoEnvio.Enviado);

        var reporte = await CrearServicio().ReiniciarCampaniaAsync("c_1", usuarioIds: null, reiniciarEnvios: false, CancellationToken.None);

        reporte.Respuestas.Should().Be(2);
        reporte.Conversaciones.Should().Be(2);
        reporte.ParticipantesReseteados.Should().Be(2);
        (await _respuestas.ListarRespuestasAsync("c_1", CancellationToken.None)).Should().BeEmpty();
        (await _conversaciones.ListarConversacionesAsync("c_1", CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task ReiniciarCampania_ConSubconjunto_SoloAfectaEsosUsuarios()
    {
        await SembrarDatosAsync("c_1", "u_1");
        await SembrarDatosAsync("c_1", "u_2");

        var reporte = await CrearServicio().ReiniciarCampaniaAsync("c_1", new[] { "u_1" }, reiniciarEnvios: false, CancellationToken.None);

        reporte.Respuestas.Should().Be(1);
        (await _respuestas.ListarRespuestasAsync("c_1", CancellationToken.None)).Should().ContainSingle()
            .Which.UsuarioId.Should().Be("u_2");
    }

    [Fact]
    public async Task Reiniciar_RegistraAccionAdministrativaConConteos()
    {
        await SembrarDatosAsync("c_1", "u_1");
        LogSeguridad? capturado = null;
        _log.When(x => x.RegistrarAsync(Arg.Any<LogSeguridad>(), Arg.Any<CancellationToken>()))
            .Do(ci => capturado = ci.Arg<LogSeguridad>());

        await CrearServicio().ReiniciarParticipanteAsync("c_1", "u_1", reiniciarEnvios: false, CancellationToken.None);

        capturado.Should().NotBeNull();
        capturado!.TipoEvento.Should().Be(TipoEventoSeguridad.AccionAdministrativa);
        capturado.Detalle.Should().Contain("reinicio_datos:c_1:u_1");
        capturado.CorrelationId.Should().Be("corr_test");
    }

    [Fact]
    public async Task Reiniciar_CampaniaInexistente_LanzaNoEncontrado()
    {
        _campanias.ObtenerCampaniaPorIdAsync("c_zzz", Arg.Any<CancellationToken>()).Returns((Campania?)null);

        var acto = () => CrearServicio().ReiniciarParticipanteAsync("c_zzz", "u_1", reiniciarEnvios: false, CancellationToken.None);

        await acto.Should().ThrowAsync<ErrorNoEncontrado>();
    }

    private async Task SembrarDatosAsync(string campaniaId, string usuarioId)
    {
        var respuestaId = $"resp_{campaniaId}_{usuarioId}";
        var conversacionId = $"conv_{campaniaId}_{usuarioId}";
        var blobPath = $"campanias/{campaniaId}/respuesta/{respuestaId}.md";

        await _conversaciones.GuardarConversacionAsync(
            DominioConversacion.Iniciar(conversacionId, campaniaId, usuarioId, "p_1", "whatsapp", null, Epoca),
            CancellationToken.None);
        await _conversaciones.GuardarMensajeAsync(
            Mensaje.Crear($"{conversacionId}_m0", campaniaId, conversacionId, DireccionMensaje.In, "hola", null, Epoca),
            CancellationToken.None);
        await _respuestas.GuardarRespuestaAsync(
            Respuesta.Crear(respuestaId, campaniaId, usuarioId, "p_1", conversacionId, "Idea", "whatsapp", false, EstadoRespuesta.Recibida, Epoca, null),
            CancellationToken.None);
        await _respuestas.GuardarEvaluacionAsync(
            DominioEvaluacion.Crear(
                $"eval_{respuestaId}", campaniaId, respuestaId, usuarioId, "p_1", "r_general", 1, "pr_eval", 1, "llm_default",
                new ConfigLlmSnapshot("AzureOpenAI", "gpt-4o-mini", "https://x", new Dictionary<string, object?>()),
                null, null, 3m, "ok", "Bien", RecomendacionEvaluacion.Cerrar, null, null, null, false, Epoca),
            CancellationToken.None);
        await _respuestas.GuardarArtefactoAsync(
            ArtefactoMarkdown.Crear(
                $"md_{respuestaId}", campaniaId, TipoArtefactoMarkdown.Respuesta, usuarioId, "p_1", respuestaId, $"eval_{respuestaId}",
                "# md", blobPath, EstadoArtefacto.Generado, 1, Epoca, Epoca),
            CancellationToken.None);
        await _blob.GuardarTextoAsync(blobPath, "# md", CancellationToken.None);
    }

    private Task SembrarParticipanteAsync(string campaniaId, string usuarioId, EstadoEnvio estadoEnvio, bool conFechas = false)
        => _participantes.GuardarParticipanteAsync(
            ParticipanteCampania.Crear(
                $"part_{campaniaId}_{usuarioId}",
                campaniaId,
                usuarioId,
                NumeroWhatsApp.FromNormalized("573000000001"),
                EstadoRegistro.Activo,
                estadoEnvio,
                EstadoRespuestaParticipante.Respondio,
                Epoca,
                conFechas ? Epoca : null,
                conFechas ? Epoca : null),
            CancellationToken.None);
}
