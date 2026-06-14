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
        _respuestas.GuardarRespuestaAsync(
            Respuesta.Crear("resp_1", "c_1", "u_1", "p_1", "conv_1", "Mi idea de mejora", "whatsapp", false, EstadoRespuesta.Evaluada, Epoca, new[] { "t_oper" }),
            CancellationToken.None).GetAwaiter().GetResult();
        _respuestas.GuardarEvaluacionAsync(CrearEvaluacion(), CancellationToken.None).GetAwaiter().GetResult();
    }

    private static DominioEvaluacion CrearEvaluacion()
        => DominioEvaluacion.Crear(
            "eval_1",
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
            new[] { CalificacionCriterio.Crear("claridad", 4m, "clara") },
            4m,
            "buena idea",
            "Buena idea",
            RecomendacionEvaluacion.Cerrar,
            null,
            new[] { "eficiencia" },
            new[] { "bodega" },
            anomaliaSeguridad: false,
            Epoca);

    private sealed class RepositorioRespuestasFake : IRepositorioRespuestas
    {
        private readonly Dictionary<string, Respuesta> _respuestas = new(StringComparer.Ordinal);
        private readonly Dictionary<string, DominioEvaluacion> _evaluaciones = new(StringComparer.Ordinal);
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
            _evaluaciones[evaluacion.RespuestaId] = evaluacion;
            return Task.CompletedTask;
        }

        public Task<DominioEvaluacion?> ObtenerEvaluacionPorRespuestaAsync(string campaniaId, string respuestaId, CancellationToken cancellationToken)
            => Task.FromResult(_evaluaciones.GetValueOrDefault(respuestaId));

        public Task GuardarArtefactoAsync(ArtefactoMarkdown artefacto, CancellationToken cancellationToken)
        {
            _artefactos[artefacto.Id] = artefacto;
            return Task.CompletedTask;
        }

        public Task<ArtefactoMarkdown?> ObtenerArtefactoAsync(string campaniaId, string artefactoId, CancellationToken cancellationToken)
            => Task.FromResult(_artefactos.GetValueOrDefault(artefactoId));

        public Task<IReadOnlyCollection<ArtefactoMarkdown>> ListarArtefactosAsync(string campaniaId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<ArtefactoMarkdown>>(_artefactos.Values.Where(a => a.CampaniaId == campaniaId).ToArray());
    }
}
