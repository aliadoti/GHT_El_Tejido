using ElTejido.Application.Conversacion;
using ElTejido.Application.Respuestas;
using ElTejido.Domain.Evaluacion;
using ElTejido.Domain.Respuestas;
using ElTejido.Infrastructure.Conocimiento;
using FluentAssertions;
using NSubstitute;
using DominioEvaluacion = ElTejido.Domain.Evaluacion.Evaluacion;

namespace ElTejido.UnitTests.Conocimiento;

public sealed class RecuperadorLexicoBaseConocimientoTests
{
    private const string Campania = "c_1";
    private static readonly DateTimeOffset Epoca = DateTimeOffset.UnixEpoch;

    private readonly IRepositorioRespuestas _respuestas = Substitute.For<IRepositorioRespuestas>();

    [Fact]
    public async Task Recuperar_ExcluyeAlPropioAutorYLaConversacionEnCurso()
    {
        Sembrar(
            Resp("r_autor", "u_autor", "conv_x", "huerta comunitaria en el barrio", EstadoRespuesta.Evaluada),
            Resp("r_actual", "u_otro", "conv_actual", "huerta comunitaria y compostaje", EstadoRespuesta.Evaluada),
            Resp("r_ok", "u_otro", "conv_y", "una huerta comunitaria con riego", EstadoRespuesta.Evaluada));
        ConEvaluacion("r_ok", temas: new[] { "sostenibilidad" });

        var aportes = await Crear().RecuperarAsync(
            Campania, "quiero una huerta comunitaria", Array.Empty<string>(),
            usuarioIdAutorExcluir: "u_autor", conversacionIdExcluir: "conv_actual", topK: 5, CancellationToken.None);

        aportes.Should().ContainSingle();
        aportes[0].Resumen.Should().Contain("huerta");
    }

    [Fact]
    public async Task Recuperar_IgnoraRespuestasNoEvaluadas()
    {
        Sembrar(
            Resp("r_recibida", "u_otro", "conv_y", "huerta comunitaria", EstadoRespuesta.Recibida),
            Resp("r_pend", "u_otro", "conv_z", "huerta comunitaria", EstadoRespuesta.EvaluacionPendiente));

        var aportes = await Crear().RecuperarAsync(
            Campania, "huerta comunitaria", Array.Empty<string>(),
            "u_yo", null, topK: 5, CancellationToken.None);

        aportes.Should().BeEmpty();
    }

    [Fact]
    public async Task Recuperar_RespetaTopK()
    {
        Sembrar(
            Resp("r1", "u_a", "c_a", "energia solar en el techo", EstadoRespuesta.Evaluada),
            Resp("r2", "u_b", "c_b", "energia solar para la sede", EstadoRespuesta.Evaluada),
            Resp("r3", "u_c", "c_c", "energia solar y baterias", EstadoRespuesta.Evaluada));

        var aportes = await Crear().RecuperarAsync(
            Campania, "energia solar", Array.Empty<string>(),
            "u_yo", null, topK: 2, CancellationToken.None);

        aportes.Should().HaveCount(2);
    }

    [Fact]
    public async Task Recuperar_SinSolapamiento_DevuelveVacio()
    {
        Sembrar(Resp("r1", "u_a", "c_a", "transporte publico electrico", EstadoRespuesta.Evaluada));

        var aportes = await Crear().RecuperarAsync(
            Campania, "recetas de cocina saludable", Array.Empty<string>(),
            "u_yo", null, topK: 5, CancellationToken.None);

        aportes.Should().BeEmpty();
    }

    [Fact]
    public async Task Recuperar_ResumenIncluyeTemasYEntidadesDeLaEvaluacion()
    {
        Sembrar(Resp("r1", "u_a", "c_a", "propongo paneles solares", EstadoRespuesta.Evaluada));
        ConEvaluacion("r1", temas: new[] { "energia" }, entidades: new[] { "paneles" });

        var aportes = await Crear().RecuperarAsync(
            Campania, "paneles solares", Array.Empty<string>(),
            "u_yo", null, topK: 5, CancellationToken.None);

        aportes.Should().ContainSingle();
        aportes[0].Resumen.Should().Contain("energia");
        aportes[0].Resumen.Should().Contain("paneles");
    }

    [Fact]
    public async Task Recuperar_TopKCeroOTextoVacio_NoConsultaElRepositorio()
    {
        var aportes = await Crear().RecuperarAsync(
            Campania, "cualquier cosa", Array.Empty<string>(), "u_yo", null, topK: 0, CancellationToken.None);

        aportes.Should().BeEmpty();
        await _respuestas.DidNotReceiveWithAnyArgs().ListarRespuestasAsync(default!, default);
    }

    private RecuperadorLexicoBaseConocimiento Crear()
        => new(_respuestas);

    private void Sembrar(params Respuesta[] respuestas)
        => _respuestas.ListarRespuestasAsync(Campania, Arg.Any<CancellationToken>()).Returns(respuestas);

    private void ConEvaluacion(string respuestaId, string[]? temas = null, string[]? entidades = null)
        => _respuestas.ObtenerEvaluacionPorRespuestaAsync(Campania, respuestaId, Arg.Any<CancellationToken>())
            .Returns(CrearEvaluacion(respuestaId, temas ?? Array.Empty<string>(), entidades ?? Array.Empty<string>()));

    private static Respuesta Resp(string id, string usuarioId, string conversacionId, string texto, EstadoRespuesta estado)
        => Respuesta.Crear(id, Campania, usuarioId, "p_1", conversacionId, texto, "whatsapp", false, estado, Epoca, null);

    private static DominioEvaluacion CrearEvaluacion(string respuestaId, string[] temas, string[] entidades)
        => DominioEvaluacion.Crear(
            "eval_" + respuestaId,
            Campania,
            respuestaId,
            "u_a",
            "p_1",
            "rub_1",
            1,
            "pr_1",
            1,
            "llm_1",
            new ConfigLlmSnapshot("openai", "gpt", "https://api", new Dictionary<string, object?>()),
            null,
            Array.Empty<CalificacionCriterio>(),
            5m,
            "ok",
            "retro",
            RecomendacionEvaluacion.Cerrar,
            null,
            temas,
            entidades,
            false,
            Epoca);
}
