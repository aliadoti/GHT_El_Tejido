using ElTejido.Application.Campanas;
using ElTejido.Application.Markdown;
using ElTejido.Application.Respuestas;
using ElTejido.Application.Usuarios;
using ElTejido.Domain.Campanas;
using ElTejido.Domain.Common;
using ElTejido.Domain.Evaluacion;
using ElTejido.Domain.Respuestas;
using ElTejido.Domain.Usuarios;
using ElTejido.Infrastructure.Blob;
using ElTejido.UnitTests.Soporte;
using FluentAssertions;
using NSubstitute;
using DominioEvaluacion = ElTejido.Domain.Evaluacion.Evaluacion;

namespace ElTejido.UnitTests.Markdown;

public sealed class CompiladorMarkdownTests
{
    private static readonly DateTimeOffset Epoca = DateTimeOffset.UnixEpoch;

    private readonly RepositorioRespuestasFake _respuestas = new();
    private readonly IRepositorioUsuarios _usuarios = Substitute.For<IRepositorioUsuarios>();
    private readonly IRepositorioCampanias _campanias = Substitute.For<IRepositorioCampanias>();
    private readonly AlmacenBlobMemoria _blob = new();
    private readonly RelojFijo _reloj = new(Epoca);

    public CompiladorMarkdownTests()
    {
        _usuarios.ObtenerUsuarioPorIdAsync("u_1", Arg.Any<CancellationToken>())
            .Returns(FabricasDominio.CrearUsuario("u_1", "573001112233", RolUsuario.Participante, EstadoRegistro.Activo, "Ana"));
        _campanias.ObtenerCampaniaPorIdAsync("c_1", Arg.Any<CancellationToken>())
            .Returns(FabricasDominio.CrearCampania("c_1", EstadoCampania.Activa, new[] { FabricasDominio.CrearPregunta("p_1", 1) }));
    }

    [Fact]
    public async Task Compilar_GeneraArtefactoConMetadatosYTrazabilidad()
    {
        Sembrar();

        var artefacto = await Construir().CompilarAsync(Solicitud(), CancellationToken.None);

        artefacto.Version.Should().Be(1);
        artefacto.BlobPath.Should().Be("campanias/c_1/respuesta/resp_1.md");
        var contenido = artefacto.ContenidoMarkdown;
        contenido.Should().Contain("# Aporte de Ana");
        contenido.Should().Contain("Campaña: Campania c_1");
        contenido.Should().Contain("Pregunta: Pregunta 1");
        contenido.Should().Contain("Mi idea de mejora");
        contenido.Should().Contain("Buena idea");
        contenido.Should().Contain("| claridad | 4 | clara |");
        contenido.Should().Contain("eficiencia");
        contenido.Should().Contain("ID de respuesta: resp_1");
        contenido.Should().Contain("ID de evaluación: eval_1");

        // Se persiste en Blob y en Cosmos (responses).
        _blob.Leer(artefacto.BlobPath).Should().Be(contenido);
        (await _respuestas.ObtenerArtefactoAsync("c_1", "md_resp_1", CancellationToken.None)).Should().NotBeNull();
    }

    [Fact]
    public async Task Compilar_UsaUltimaEvaluacionValidaDeLaRespuesta()
    {
        SembrarRespuesta();
        await _respuestas.GuardarEvaluacionAsync(CrearEvaluacion("eval_vieja", 2m, Epoca), CancellationToken.None);
        await _respuestas.GuardarEvaluacionAsync(CrearEvaluacion("eval_nueva", 5m, Epoca.AddMinutes(10)), CancellationToken.None);

        var artefacto = await Construir().CompilarAsync(Solicitud(), CancellationToken.None);

        artefacto.EvaluacionRef.Should().Be("eval_nueva");
        artefacto.ContenidoMarkdown.Should().Contain("- Calificación total: 5");
        artefacto.ContenidoMarkdown.Should().Contain("ID de evaluación: eval_nueva");
        artefacto.ContenidoMarkdown.Should().NotContain("- Calificación total: 2");
    }

    [Fact]
    public async Task Compilar_NoFiltraSecretos()
    {
        Sembrar();

        var artefacto = await Construir().CompilarAsync(Solicitud(), CancellationToken.None);

        artefacto.ContenidoMarkdown.Should().NotContain("llm-key");
        artefacto.ContenidoMarkdown.Should().NotContain("apiKey");
    }

    [Fact]
    public async Task Compilar_Regenerar_IncrementaVersionYConservaCreadoEn()
    {
        Sembrar();
        var compilador = Construir();

        var v1 = await compilador.CompilarAsync(Solicitud(), CancellationToken.None);
        _reloj.Avanzar(TimeSpan.FromMinutes(5));
        var v2 = await compilador.CompilarAsync(Solicitud(), CancellationToken.None);

        v1.Version.Should().Be(1);
        v2.Version.Should().Be(2);
        v2.Id.Should().Be(v1.Id);
        v2.CreadoEn.Should().Be(v1.CreadoEn);
        v2.ActualizadoEn.Should().BeAfter(v1.ActualizadoEn);
    }

    [Fact]
    public async Task Compilar_RespuestaInexistente_LanzaNoEncontrado()
    {
        var accion = () => Construir().CompilarAsync(Solicitud(), CancellationToken.None);

        await accion.Should().ThrowAsync<ElTejido.Application.Common.ErrorNoEncontrado>();
    }

    private CompiladorMarkdown Construir()
        => new(_respuestas, _usuarios, _campanias, _blob, _reloj);

    private static SolicitudCompilacion Solicitud()
        => new("c_1", TipoArtefactoMarkdown.Respuesta, "resp_1", "u_1", "p_1");

    private void Sembrar()
    {
        SembrarRespuesta();
        _respuestas.GuardarEvaluacionAsync(CrearEvaluacion(), CancellationToken.None).GetAwaiter().GetResult();
    }

    private void SembrarRespuesta()
    {
        _respuestas.GuardarRespuestaAsync(
            Respuesta.Crear("resp_1", "c_1", "u_1", "p_1", "conv_1", "Mi idea de mejora", "whatsapp", false, EstadoRespuesta.Evaluada, Epoca, new[] { "t_oper" }),
            CancellationToken.None).GetAwaiter().GetResult();
    }

    private static DominioEvaluacion CrearEvaluacion()
        => CrearEvaluacion("eval_1", 4m, Epoca);

    private static DominioEvaluacion CrearEvaluacion(string id, decimal calificacionTotal, DateTimeOffset fecha)
        => DominioEvaluacion.Crear(
            id,
            "c_1",
            "resp_1",
            "u_1",
            "p_1",
            "r_general",
            3,
            "pr_eval",
            5,
            "llm_default",
            new ConfigLlmSnapshot("AzureOpenAI", "gpt-4o-mini", "https://example.openai.azure.com/", new Dictionary<string, object?>()),
            new Dictionary<string, decimal> { ["claridad"] = 1m },
            new[] { CalificacionCriterio.Crear("claridad", calificacionTotal, "clara") },
            calificacionTotal,
            "buena idea",
            "Buena idea",
            RecomendacionEvaluacion.Cerrar,
            null,
            new[] { "eficiencia" },
            new[] { "bodega" },
            anomaliaSeguridad: false,
            fecha);

    private sealed class RepositorioRespuestasFake : IRepositorioRespuestas
    {
        private readonly Dictionary<string, Respuesta> _respuestas = new(StringComparer.Ordinal);
        private readonly List<DominioEvaluacion> _evaluaciones = new();
        private readonly Dictionary<string, ArtefactoMarkdown> _artefactos = new(StringComparer.Ordinal);

        public Task GuardarRespuestaAsync(Respuesta respuesta, CancellationToken cancellationToken)
        {
            _respuestas[respuesta.Id] = respuesta;
            return Task.CompletedTask;
        }

        public Task<Respuesta?> ObtenerRespuestaAsync(string campaniaId, string respuestaId, CancellationToken cancellationToken)
            => Task.FromResult(_respuestas.GetValueOrDefault(respuestaId));

        public Task GuardarEvaluacionAsync(DominioEvaluacion evaluacion, CancellationToken cancellationToken)
        {
            _evaluaciones.Add(evaluacion);
            return Task.CompletedTask;
        }

        public Task<DominioEvaluacion?> ObtenerEvaluacionPorRespuestaAsync(string campaniaId, string respuestaId, CancellationToken cancellationToken)
            => Task.FromResult(_evaluaciones
                .Where(e => e.CampaniaId == campaniaId && e.RespuestaId == respuestaId)
                .OrderByDescending(e => e.Fecha)
                .FirstOrDefault());

        public Task<DominioEvaluacion?> ObtenerEvaluacionPorIdAsync(string campaniaId, string evaluacionId, CancellationToken cancellationToken)
            => Task.FromResult(_evaluaciones.FirstOrDefault(e => e.CampaniaId == campaniaId && e.Id == evaluacionId));

        public Task<IReadOnlyCollection<Respuesta>> ListarRespuestasAsync(string campaniaId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<Respuesta>>(_respuestas.Values.Where(r => r.CampaniaId == campaniaId).ToArray());

        public Task<int> ContarEvaluacionesUsuarioAsync(string campaniaId, string usuarioId, CancellationToken cancellationToken)
            => Task.FromResult(_evaluaciones.Count(e => e.CampaniaId == campaniaId && e.UsuarioId == usuarioId));

        public Task<long> SumarTokensCampaniaAsync(string campaniaId, CancellationToken cancellationToken)
            => Task.FromResult(_evaluaciones.Where(e => e.CampaniaId == campaniaId).Sum(e => (long)(e.UsoTokens?.Total ?? 0)));

        public Task GuardarArtefactoAsync(ArtefactoMarkdown artefacto, CancellationToken cancellationToken)
        {
            _artefactos[artefacto.Id] = artefacto;
            return Task.CompletedTask;
        }

        public Task<ArtefactoMarkdown?> ObtenerArtefactoAsync(string campaniaId, string artefactoId, CancellationToken cancellationToken)
            => Task.FromResult(_artefactos.GetValueOrDefault(artefactoId));

        public Task<IReadOnlyCollection<ArtefactoMarkdown>> ListarArtefactosAsync(string campaniaId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<ArtefactoMarkdown>>(_artefactos.Values.Where(a => a.CampaniaId == campaniaId).ToArray());

        public Task<ConteoBorradoRespuestas> EliminarPorUsuarioAsync(string campaniaId, string? usuarioId, CancellationToken cancellationToken)
        {
            var respuestas = _respuestas.Values.Where(r => r.CampaniaId == campaniaId && (usuarioId is null || r.UsuarioId == usuarioId)).ToArray();
            var evaluaciones = _evaluaciones.Where(e => e.CampaniaId == campaniaId && (usuarioId is null || e.UsuarioId == usuarioId)).ToArray();
            var artefactos = _artefactos.Values.Where(a => a.CampaniaId == campaniaId && (usuarioId is null || a.UsuarioId == usuarioId)).ToArray();
            foreach (var r in respuestas)
            {
                _respuestas.Remove(r.Id);
            }

            foreach (var e in evaluaciones)
            {
                _evaluaciones.Remove(e);
            }

            foreach (var a in artefactos)
            {
                _artefactos.Remove(a.Id);
            }

            return Task.FromResult(new ConteoBorradoRespuestas(
                respuestas.Length,
                evaluaciones.Length,
                artefactos.Length,
                artefactos.Select(a => a.BlobPath).ToArray()));
        }
    }
}
