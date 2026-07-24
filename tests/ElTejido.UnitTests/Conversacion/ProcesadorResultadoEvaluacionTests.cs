using ElTejido.Application.Common;
using ElTejido.Application.Conversacion;
using ElTejido.Application.Evaluacion;
using ElTejido.Application.Markdown;
using ElTejido.Application.Respuestas;
using ElTejido.Application.Seguridad;
using ElTejido.Domain.Campanas;
using ElTejido.Domain.Common;
using ElTejido.Domain.Configuracion;
using ElTejido.Domain.Evaluacion;
using ElTejido.Domain.Respuestas;
using ElTejido.Domain.Seguridad;
using ElTejido.Domain.Usuarios;
using ElTejido.UnitTests.Soporte;
using FluentAssertions;
using NSubstitute;
using DominioEvaluacion = ElTejido.Domain.Evaluacion.Evaluacion;

namespace ElTejido.UnitTests.Conversacion;

/// <summary>
/// P-15 (CAL-001) — pruebas del procesador de efectos posteriores a la evaluación (Corte 3). Verifican el
/// orden y las ramas de la persistencia + sellado de madurez + Markdown + telemetría que antes vivían inline
/// en <see cref="OrquestadorConversacion"/>: éxito vs fallback, sellado maduro/incubación y la degradación
/// por rechazo del guardado (I-17 §5.4).
/// </summary>
public sealed class ProcesadorResultadoEvaluacionTests
{
    private static readonly DateTimeOffset Epoca = DateTimeOffset.UnixEpoch;
    private static readonly EscalaRubrica Escala1a5 = new(1, 5);

    private readonly IRepositorioRespuestas _respuestas = Substitute.For<IRepositorioRespuestas>();
    private readonly ICompiladorMarkdown _compilador = Substitute.For<ICompiladorMarkdown>();
    private readonly IRepositorioLogSeguridad _logSeguridad = Substitute.For<IRepositorioLogSeguridad>();
    private readonly IProveedorCorrelacion _correlacion = Substitute.For<IProveedorCorrelacion>();

    public ProcesadorResultadoEvaluacionTests() => _correlacion.CorrelationIdActual.Returns("corr_test");

    private ProcesadorResultadoEvaluacion Crear(double umbralGlobal = 0.6)
        => new(_respuestas, _compilador, _logSeguridad, _correlacion,
            new PoliticaLimitesConversacion(umbralGlobal, cierreAnticipadoHabilitado: false));

    [Fact]
    public async Task PersistirRespuestaEvaluada_Exito_SellaMaduroPersisteYCompila()
    {
        // Escala 1..5, umbral 0.6 -> corte 3.4; score 4 -> maduro.
        var resultado = new ResultadoEvaluacion.Exito(CrearEvaluacion(calificacionTotal: 4m));

        var nivel = await Crear().PersistirRespuestaEvaluadaAsync(
            resultado, CrearCampania(), CrearPregunta(), CrearUsuario(), "conv_1", "resp_1", "Mi idea",
            esRepregunta: false, Escala1a5, Epoca, CancellationToken.None);

        nivel.Should().Be(NivelMadurez.Maduro);
        await _respuestas.Received(1).GuardarEvaluacionAsync(resultado.Evaluacion, Arg.Any<CancellationToken>());
        await _respuestas.Received(1).GuardarRespuestaAsync(
            Arg.Is<Respuesta>(r => r.Id == "resp_1" && r.Estado == EstadoRespuesta.Evaluada && r.NivelMadurez == NivelMadurez.Maduro),
            Arg.Any<CancellationToken>());
        await _compilador.Received(1).CompilarAsync(Arg.Any<SolicitudCompilacion>(), Arg.Any<CancellationToken>());
        await _logSeguridad.Received(1).RegistrarAsync(
            Arg.Is<LogSeguridad>(l => l.TipoEvento == TipoEventoSeguridad.ClasificacionMadurez && l.Resultado == "maduro"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PersistirRespuestaEvaluada_CalificacionBaja_SellaIncubacion()
    {
        var resultado = new ResultadoEvaluacion.Exito(CrearEvaluacion(calificacionTotal: 2m));

        var nivel = await Crear().PersistirRespuestaEvaluadaAsync(
            resultado, CrearCampania(), CrearPregunta(), CrearUsuario(), "conv_1", "resp_1", "Mi idea",
            esRepregunta: false, Escala1a5, Epoca, CancellationToken.None);

        nivel.Should().Be(NivelMadurez.Incubacion);
        await _respuestas.Received(1).GuardarRespuestaAsync(
            Arg.Is<Respuesta>(r => r.NivelMadurez == NivelMadurez.Incubacion && r.Estado == EstadoRespuesta.Evaluada),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PersistirRespuestaEvaluada_Fallback_QuedaPendienteIncubacionYNoCompila()
    {
        // En fallback la evaluacion no es confiable: nunca es maduro y no se compila Markdown (08 §6).
        var resultado = new ResultadoEvaluacion.Fallback(
            CrearEvaluacion(calificacionTotal: 5m, retro: EvaluadorLlm.RetroNeutra), "error_proveedor");

        var nivel = await Crear().PersistirRespuestaEvaluadaAsync(
            resultado, CrearCampania(), CrearPregunta(), CrearUsuario(), "conv_1", "resp_1", "Mi idea",
            esRepregunta: false, Escala1a5, Epoca, CancellationToken.None);

        nivel.Should().Be(NivelMadurez.Incubacion);
        await _respuestas.Received(1).GuardarRespuestaAsync(
            Arg.Is<Respuesta>(r => r.Estado == EstadoRespuesta.EvaluacionPendiente && r.NivelMadurez == NivelMadurez.Incubacion),
            Arg.Any<CancellationToken>());
        await _compilador.DidNotReceiveWithAnyArgs().CompilarAsync(default!, default);
    }

    [Fact]
    public async Task PersistirRespuestaEvaluada_Segmentada_PropagaIndiceYPadre()
    {
        var resultado = new ResultadoEvaluacion.Exito(CrearEvaluacion(calificacionTotal: 4m));

        await Crear().PersistirRespuestaEvaluadaAsync(
            resultado, CrearCampania(), CrearPregunta(), CrearUsuario(), "conv_1", "resp_1_2", "Idea 2",
            esRepregunta: false, Escala1a5, Epoca, CancellationToken.None, ideaIndice: 2, respuestaPadreId: "wamid.padre");

        await _respuestas.Received(1).GuardarRespuestaAsync(
            Arg.Is<Respuesta>(r => r.IdeaIndice == 2 && r.RespuestaPadreId == "wamid.padre"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReclasificarComoIncubacion_DegradaGuardaCompilaYRegistra()
    {
        var madura = Respuesta.Crear(
            "resp_1", "c_1", "u_1", "p_1", "conv_1", "Idea madura", "whatsapp", esRepregunta: false,
            EstadoRespuesta.Evaluada, Epoca, tagsSnapshot: null, ideaIndice: null, respuestaPadreId: null, NivelMadurez.Maduro);

        await Crear().ReclasificarComoIncubacionAsync(
            CrearCampania(), CrearUsuario(), CrearPregunta(), new[] { madura }, Epoca, CancellationToken.None);

        madura.NivelMadurez.Should().Be(NivelMadurez.Incubacion);
        await _respuestas.Received(1).GuardarRespuestaAsync(madura, Arg.Any<CancellationToken>());
        await _compilador.Received(1).CompilarAsync(Arg.Any<SolicitudCompilacion>(), Arg.Any<CancellationToken>());
        await _logSeguridad.Received(1).RegistrarAsync(
            Arg.Is<LogSeguridad>(l => l.TipoEvento == TipoEventoSeguridad.ClasificacionMadurez
                && l.Detalle!.Contains("motivo:rechazo_guardado", StringComparison.Ordinal)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegistrarCierreUmbral_EmiteTelemetriaConScoreYValor()
    {
        await Crear().RegistrarCierreUmbralAsync(
            CrearUsuario(), calificacionTotal: 5m, valorUmbral: 4.4m, Escala1a5, umbralEfectivo: 0.85,
            origen: "global", Epoca, CancellationToken.None);

        await _logSeguridad.Received(1).RegistrarAsync(
            Arg.Is<LogSeguridad>(l => l.TipoEvento == TipoEventoSeguridad.CierreUmbralAnticipado
                && l.Resultado == "cierre_anticipado"
                && l.Detalle!.Contains("score:5", StringComparison.Ordinal)
                && l.Detalle!.Contains("valor:4.4", StringComparison.Ordinal)),
            Arg.Any<CancellationToken>());
    }

    // ---- Fábricas locales --------------------------------------------------------------------------

    private static Usuario CrearUsuario() => FabricasDominio.CrearUsuario("u_1", "573001112233", RolUsuario.Participante);

    private static Pregunta CrearPregunta()
        => Pregunta.Crear(
            "p_1", "Pregunta 1", "Instruccion", "categoria", 1, EstadoRegistro.Activo,
            rubricaRef: null, versionRubrica: null, promptRefs: null, maxRepreguntas: 1,
            LimitesSeguridad.ParaPregunta(1500, 2), ConfigMarkdown.Crear(TipoArtefactoMarkdown.Respuesta));

    private static Campania CrearCampania()
        => Campania.Crear(
            "c_1", "Campania c_1", "Descripcion", "Objetivo", EstadoCampania.Activa,
            mensajesIniciales: null, new[] { CrearPregunta() }, "rub_1", promptRefs: null, "llm_1",
            ConfigMarkdown.Crear(TipoArtefactoMarkdown.Campania),
            ConfigConversacional.Crear(1, "Gracias por participar."),
            LimitesSeguridad.Crear(1500, 10, 2), usuariosHabilitados: null, Epoca, Epoca);

    private static DominioEvaluacion CrearEvaluacion(decimal calificacionTotal, string retro = "Buena idea")
        => DominioEvaluacion.Crear(
            "eval_1", "c_1", "resp_1", "u_1", "p_1", "rub_1", 1, "pr_eval", 1, "llm_1",
            new ConfigLlmSnapshot("AzureOpenAI", "gpt-4o-mini", "https://x", new Dictionary<string, object?>()),
            new Dictionary<string, decimal> { ["claridad"] = 1m },
            new[] { CalificacionCriterio.Crear("claridad", 4m, "clara") },
            calificacionTotal, "explica", retro, RecomendacionEvaluacion.Cerrar, null,
            new[] { "tema" }, new[] { "ent" }, false, Epoca);
}
