using ElTejido.Application.Common;
using ElTejido.Application.Mantenimiento;
using ElTejido.Application.Seguridad;
using ElTejido.Domain.Campanas;
using ElTejido.Domain.Common;
using ElTejido.Domain.Conversaciones;
using ElTejido.Domain.Evaluacion;
using ElTejido.Domain.Identidad;
using ElTejido.Domain.Participantes;
using ElTejido.Domain.Respuestas;
using ElTejido.Domain.Seguridad;
using ElTejido.Domain.Usuarios;
using ElTejido.Infrastructure.Blob;
using ElTejido.Infrastructure.Persistencia.Memoria;
using ElTejido.UnitTests.Soporte;
using FluentAssertions;
using NSubstitute;
using DominioConversacion = ElTejido.Domain.Conversaciones.Conversacion;
using DominioEvaluacion = ElTejido.Domain.Evaluacion.Evaluacion;

namespace ElTejido.UnitTests.Mantenimiento;

/// <summary>
/// P-15 — casos de uso de <see cref="ServicioPurgaCampanias"/>: borra todas las campañas y su flujo,
/// elimina los usuarios no administrativos y conserva Admin/Visor. Idempotente y auditado. Usa los
/// repos in-memory reales para ejercer el borrado de punta a punta.
/// </summary>
public sealed class ServicioPurgaCampaniasTests
{
    private static readonly DateTimeOffset Epoca = DateTimeOffset.UnixEpoch;

    private readonly RepositorioCampaniasMemoria _campanias = new();
    private readonly RepositorioConversacionesMemoria _conversaciones = new();
    private readonly RepositorioRespuestasMemoria _respuestas = new();
    private readonly RepositorioParticipantesMemoria _participantes = new();
    private readonly RepositorioUsuariosMemoria _usuarios = new();
    private readonly AlmacenBlobMemoria _blob = new();
    private readonly IRepositorioLogSeguridad _log = Substitute.For<IRepositorioLogSeguridad>();
    private readonly IProveedorCorrelacion _correlacion = Substitute.For<IProveedorCorrelacion>();

    public ServicioPurgaCampaniasTests()
    {
        _correlacion.CorrelationIdActual.Returns("corr_test");
    }

    private ServicioPurgaCampanias CrearServicio()
        => new(_campanias, _participantes, _conversaciones, _respuestas, _usuarios, _blob, _log, _correlacion, TimeProvider.System);

    [Fact]
    public async Task PurgarTodo_BorraCampaniasYFlujoYUsuariosNoAdmin_ConservaAdminYVisor()
    {
        await _campanias.GuardarCampaniaAsync(FabricasDominio.CrearCampania("c_1", EstadoCampania.Activa), CancellationToken.None);
        await _campanias.GuardarCampaniaAsync(FabricasDominio.CrearCampania("c_2", EstadoCampania.Borrador), CancellationToken.None);
        await SembrarDatosAsync("c_1", "u_1");
        await SembrarDatosAsync("c_2", "u_2");
        await SembrarParticipanteAsync("c_1", "u_1");
        await SembrarParticipanteAsync("c_2", "u_2");
        await SembrarUsuarioAsync("u_1", "573000000001", RolUsuario.Participante);
        await SembrarUsuarioAsync("adm_1", "573000000009", RolUsuario.Admin);
        await SembrarUsuarioAsync("vis_1", "573000000008", RolUsuario.Visor);

        var reporte = await CrearServicio().PurgarTodoAsync(CancellationToken.None);

        reporte.Campanias.Should().Be(2);
        reporte.Respuestas.Should().Be(2);
        reporte.Conversaciones.Should().Be(2);
        reporte.Evaluaciones.Should().Be(2);
        reporte.Artefactos.Should().Be(2);
        reporte.BlobsBorrados.Should().Be(2);
        reporte.Participantes.Should().Be(2);
        reporte.UsuariosBorrados.Should().Be(1);

        (await _campanias.BuscarCampaniasAsync(new ElTejido.Application.Campanas.FiltroCampanias(), CancellationToken.None)).Should().BeEmpty();
        (await _respuestas.ListarRespuestasAsync("c_1", CancellationToken.None)).Should().BeEmpty();
        (await _conversaciones.ListarConversacionesAsync("c_1", CancellationToken.None)).Should().BeEmpty();
        (await _participantes.ListarParticipantesAsync("c_1", CancellationToken.None)).Should().BeEmpty();
        (await _usuarios.ObtenerUsuarioPorIdAsync("u_1", CancellationToken.None)).Should().BeNull();
        (await _usuarios.ObtenerUsuarioPorIdAsync("adm_1", CancellationToken.None)).Should().NotBeNull();
        (await _usuarios.ObtenerUsuarioPorIdAsync("vis_1", CancellationToken.None)).Should().NotBeNull();
    }

    [Fact]
    public async Task PurgarTodo_SegundaLlamada_EsIdempotente()
    {
        await _campanias.GuardarCampaniaAsync(FabricasDominio.CrearCampania("c_1", EstadoCampania.Activa), CancellationToken.None);
        await SembrarDatosAsync("c_1", "u_1");
        await SembrarUsuarioAsync("u_1", "573000000001", RolUsuario.Participante);
        var servicio = CrearServicio();

        await servicio.PurgarTodoAsync(CancellationToken.None);
        var segunda = await servicio.PurgarTodoAsync(CancellationToken.None);

        segunda.Should().BeEquivalentTo(ReportePurgaCampanias.Vacio);
    }

    [Fact]
    public async Task PurgarTodo_RegistraAccionAdministrativaConConteos()
    {
        await _campanias.GuardarCampaniaAsync(FabricasDominio.CrearCampania("c_1", EstadoCampania.Activa), CancellationToken.None);
        await SembrarDatosAsync("c_1", "u_1");
        LogSeguridad? capturado = null;
        _log.When(x => x.RegistrarAsync(Arg.Any<LogSeguridad>(), Arg.Any<CancellationToken>()))
            .Do(ci => capturado = ci.Arg<LogSeguridad>());

        await CrearServicio().PurgarTodoAsync(CancellationToken.None);

        capturado.Should().NotBeNull();
        capturado!.TipoEvento.Should().Be(TipoEventoSeguridad.AccionAdministrativa);
        capturado.Detalle.Should().Contain("purga_total");
        capturado.CorrelationId.Should().Be("corr_test");
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

    private Task SembrarParticipanteAsync(string campaniaId, string usuarioId)
        => _participantes.GuardarParticipanteAsync(
            ParticipanteCampania.Crear(
                $"part_{campaniaId}_{usuarioId}",
                campaniaId,
                usuarioId,
                NumeroWhatsApp.FromNormalized("573000000001"),
                EstadoRegistro.Activo,
                EstadoEnvio.Enviado,
                EstadoRespuestaParticipante.Respondio,
                Epoca,
                null,
                null),
            CancellationToken.None);

    private Task SembrarUsuarioAsync(string id, string numero, RolUsuario rol)
        => _usuarios.GuardarUsuarioAsync(FabricasDominio.CrearUsuario(id, numero, rol, nombre: id), CancellationToken.None);
}
