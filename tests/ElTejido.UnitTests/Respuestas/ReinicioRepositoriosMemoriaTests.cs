using ElTejido.Domain.Campanas;
using ElTejido.Domain.Conversaciones;
using ElTejido.Domain.Evaluacion;
using ElTejido.Domain.Respuestas;
using ElTejido.Infrastructure.Persistencia.Memoria;
using FluentAssertions;
using DominioConversacion = ElTejido.Domain.Conversaciones.Conversacion;
using DominioEvaluacion = ElTejido.Domain.Evaluacion.Evaluacion;

namespace ElTejido.UnitTests.Respuestas;

/// <summary>
/// P-03 (reinicio de datos): las eliminaciones de los repos in-memory deben estar acotadas al
/// alcance (campaniaId[, usuarioId]), devolver conteos correctos y ser idempotentes.
/// </summary>
public sealed class ReinicioRepositoriosMemoriaTests
{
    private static readonly DateTimeOffset Epoca = DateTimeOffset.UnixEpoch;

    [Fact]
    public async Task Respuestas_EliminarPorUsuario_SoloBorraElAlcanceYDevuelveConteos()
    {
        var repo = new RepositorioRespuestasMemoria();
        await SembrarRespuestaCompletaAsync(repo, "c_1", "u_1", "resp_a", "eval_a", "md_a", "campanias/c_1/respuesta/resp_a.md");
        await SembrarRespuestaCompletaAsync(repo, "c_1", "u_2", "resp_b", "eval_b", "md_b", "campanias/c_1/respuesta/resp_b.md");
        await SembrarRespuestaCompletaAsync(repo, "c_2", "u_1", "resp_c", "eval_c", "md_c", "campanias/c_2/respuesta/resp_c.md");

        var conteo = await repo.EliminarPorUsuarioAsync("c_1", "u_1", CancellationToken.None);

        conteo.Respuestas.Should().Be(1);
        conteo.Evaluaciones.Should().Be(1);
        conteo.Artefactos.Should().Be(1);
        conteo.RutasBlob.Should().ContainSingle().Which.Should().Be("campanias/c_1/respuesta/resp_a.md");

        // El otro usuario de la misma campania y la otra campania quedan intactos.
        (await repo.ObtenerRespuestaAsync("c_1", "resp_b", CancellationToken.None)).Should().NotBeNull();
        (await repo.ObtenerRespuestaAsync("c_2", "resp_c", CancellationToken.None)).Should().NotBeNull();
        (await repo.ObtenerRespuestaAsync("c_1", "resp_a", CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task Respuestas_EliminarSinUsuario_BorraTodaLaCampaniaNoLasOtras()
    {
        var repo = new RepositorioRespuestasMemoria();
        await SembrarRespuestaCompletaAsync(repo, "c_1", "u_1", "resp_a", "eval_a", "md_a", "campanias/c_1/respuesta/resp_a.md");
        await SembrarRespuestaCompletaAsync(repo, "c_1", "u_2", "resp_b", "eval_b", "md_b", "campanias/c_1/respuesta/resp_b.md");
        await SembrarRespuestaCompletaAsync(repo, "c_2", "u_1", "resp_c", "eval_c", "md_c", "campanias/c_2/respuesta/resp_c.md");

        var conteo = await repo.EliminarPorUsuarioAsync("c_1", usuarioId: null, CancellationToken.None);

        conteo.Respuestas.Should().Be(2);
        conteo.Evaluaciones.Should().Be(2);
        conteo.Artefactos.Should().Be(2);
        (await repo.ListarRespuestasAsync("c_1", CancellationToken.None)).Should().BeEmpty();
        (await repo.ListarRespuestasAsync("c_2", CancellationToken.None)).Should().ContainSingle();
    }

    [Fact]
    public async Task Respuestas_EliminarDosVeces_EsIdempotente()
    {
        var repo = new RepositorioRespuestasMemoria();
        await SembrarRespuestaCompletaAsync(repo, "c_1", "u_1", "resp_a", "eval_a", "md_a", "campanias/c_1/respuesta/resp_a.md");

        await repo.EliminarPorUsuarioAsync("c_1", "u_1", CancellationToken.None);
        var segunda = await repo.EliminarPorUsuarioAsync("c_1", "u_1", CancellationToken.None);

        segunda.Respuestas.Should().Be(0);
        segunda.Evaluaciones.Should().Be(0);
        segunda.Artefactos.Should().Be(0);
        segunda.RutasBlob.Should().BeEmpty();
    }

    [Fact]
    public async Task Conversaciones_EliminarPorUsuario_BorraHiloYMensajesDelAlcance()
    {
        var repo = new RepositorioConversacionesMemoria();
        await SembrarConversacionAsync(repo, "c_1", "u_1", "conv_a", mensajes: 2);
        await SembrarConversacionAsync(repo, "c_1", "u_2", "conv_b", mensajes: 1);
        await SembrarConversacionAsync(repo, "c_2", "u_1", "conv_c", mensajes: 3);

        var conteo = await repo.EliminarPorUsuarioAsync("c_1", "u_1", CancellationToken.None);

        conteo.Conversaciones.Should().Be(1);
        conteo.Mensajes.Should().Be(2);
        (await repo.ObtenerConversacionAsync("c_1", "conv_a", CancellationToken.None)).Should().BeNull();
        (await repo.ObtenerConversacionAsync("c_1", "conv_b", CancellationToken.None)).Should().NotBeNull();
        (await repo.ObtenerConversacionAsync("c_2", "conv_c", CancellationToken.None)).Should().NotBeNull();
        (await repo.ListarMensajesAsync("c_1", "conv_a", CancellationToken.None)).Should().BeEmpty();
        (await repo.ListarMensajesAsync("c_2", "conv_c", CancellationToken.None)).Should().HaveCount(3);
    }

    [Fact]
    public async Task Conversaciones_EliminarSinUsuario_EsIdempotente()
    {
        var repo = new RepositorioConversacionesMemoria();
        await SembrarConversacionAsync(repo, "c_1", "u_1", "conv_a", mensajes: 2);

        await repo.EliminarPorUsuarioAsync("c_1", usuarioId: null, CancellationToken.None);
        var segunda = await repo.EliminarPorUsuarioAsync("c_1", usuarioId: null, CancellationToken.None);

        segunda.Conversaciones.Should().Be(0);
        segunda.Mensajes.Should().Be(0);
    }

    private static async Task SembrarRespuestaCompletaAsync(
        RepositorioRespuestasMemoria repo,
        string campaniaId,
        string usuarioId,
        string respuestaId,
        string evaluacionId,
        string artefactoId,
        string blobPath)
    {
        await repo.GuardarRespuestaAsync(
            Respuesta.Crear(respuestaId, campaniaId, usuarioId, "p_1", "conv_1", "Idea", "whatsapp", false, EstadoRespuesta.Recibida, Epoca, null),
            CancellationToken.None);
        await repo.GuardarEvaluacionAsync(
            DominioEvaluacion.Crear(
                evaluacionId, campaniaId, respuestaId, usuarioId, "p_1", "r_general", 1, "pr_eval", 1, "llm_default",
                new ConfigLlmSnapshot("AzureOpenAI", "gpt-4o-mini", "https://x", new Dictionary<string, object?>()),
                null, null, 3m, "ok", "Bien", RecomendacionEvaluacion.Cerrar, null, null, null, false, Epoca),
            CancellationToken.None);
        await repo.GuardarArtefactoAsync(
            ArtefactoMarkdown.Crear(
                artefactoId, campaniaId, TipoArtefactoMarkdown.Respuesta, usuarioId, "p_1", respuestaId, evaluacionId,
                "# md", blobPath, EstadoArtefacto.Generado, 1, Epoca, Epoca),
            CancellationToken.None);
    }

    private static async Task SembrarConversacionAsync(
        RepositorioConversacionesMemoria repo,
        string campaniaId,
        string usuarioId,
        string conversacionId,
        int mensajes)
    {
        await repo.GuardarConversacionAsync(
            DominioConversacion.Iniciar(conversacionId, campaniaId, usuarioId, "p_1", "whatsapp", null, Epoca),
            CancellationToken.None);
        for (var i = 0; i < mensajes; i++)
        {
            await repo.GuardarMensajeAsync(
                Mensaje.Crear($"{conversacionId}_m{i}", campaniaId, conversacionId, DireccionMensaje.In, $"msg {i}", null, Epoca),
                CancellationToken.None);
        }
    }
}
