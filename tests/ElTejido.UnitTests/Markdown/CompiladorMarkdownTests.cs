using ElTejido.Application.Campanas;
using ElTejido.Application.Configuracion;
using ElTejido.Application.Conversacion;
using ElTejido.Application.Markdown;
using ElTejido.Application.Respuestas;
using ElTejido.Application.Usuarios;
using ElTejido.Domain.Campanas;
using ElTejido.Domain.Common;
using ElTejido.Domain.Configuracion;
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
    private readonly IRepositorioConfiguracion _configuracion = Substitute.For<IRepositorioConfiguracion>();

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
        // I-17: metadato de madurez (default seguro incubacion para una respuesta sin sellar maduro).
        contenido.Should().Contain("- Nivel de madurez: incubacion");

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
    public async Task Compilar_RegistraNivelMadurezMaduroEnElMetadato()
    {
        SembrarRespuesta(NivelMadurez.Maduro);
        await _respuestas.GuardarEvaluacionAsync(CrearEvaluacion(), CancellationToken.None);

        var artefacto = await Construir().CompilarAsync(Solicitud(), CancellationToken.None);

        artefacto.ContenidoMarkdown.Should().Contain("- Nivel de madurez: maduro");
    }

    [Fact]
    public async Task Compilar_MuestraUmbralConOrigenYNotaEnLaEscalaDeLaRubrica()
    {
        Sembrar();
        SembrarRubrica();

        var artefacto = await Construir().CompilarAsync(Solicitud(), CancellationToken.None);

        // I-20 §6.2: corte = min + umbral × (max − min) = 1 + 0,6 × 4 = 3,4 sobre una escala 1-5.
        artefacto.ContenidoMarkdown.Should().Contain("- Umbral de madurez: 3,4 de 5 puntos (60 %; global)");
        artefacto.ContenidoMarkdown.Should().Contain("- Calificación total: 4 de 5 puntos");
    }

    [Fact]
    public async Task Compilar_UmbralDeLaPregunta_PrevaleceYSeIndicaSuOrigen()
    {
        var pregunta = Pregunta.Crear(
            "p_1", "Pregunta 1", "Instruccion", "categoria", 1, EstadoRegistro.Activo,
            rubricaRef: null, versionRubrica: null, promptRefs: null, maxRepreguntas: 1,
            LimitesSeguridad.ParaPregunta(1500, 2), ConfigMarkdown.Crear(TipoArtefactoMarkdown.Respuesta),
            umbralCierreAnticipado: 0.5);
        _campanias.ObtenerCampaniaPorIdAsync("c_1", Arg.Any<CancellationToken>())
            .Returns(FabricasDominio.CrearCampania("c_1", EstadoCampania.Activa, new[] { pregunta }));
        Sembrar();
        SembrarRubrica();

        var artefacto = await Construir().CompilarAsync(Solicitud(), CancellationToken.None);

        // 1 + 0,5 × 4 = 3, sin decimales sobrantes, y el origen deja auditable de dónde salió.
        artefacto.ContenidoMarkdown.Should().Contain("- Umbral de madurez: 3 de 5 puntos (50 %; pregunta)");
    }

    [Fact]
    public async Task Compilar_NotaDecimal_UsaCulturaEsCoSinCerosSobrantes()
    {
        SembrarRespuesta();
        await _respuestas.GuardarEvaluacionAsync(CrearEvaluacion("eval_1", 2.60m, Epoca), CancellationToken.None);
        SembrarRubrica();

        var artefacto = await Construir().CompilarAsync(Solicitud(), CancellationToken.None);

        artefacto.ContenidoMarkdown.Should().Contain("- Calificación total: 2,6 de 5 puntos");
        artefacto.ContenidoMarkdown.Should().NotContain("2,60");
    }

    [Fact]
    public async Task Compilar_IdeaSinEvaluacion_DicePendienteYNoInventaUmbral()
    {
        SembrarIdea(EstadoResultadoIdeaConsolidada.Rechazada, evaluacionId: null);
        SembrarRubrica();

        var artefacto = await Construir().CompilarAsync(SolicitudIdea(), CancellationToken.None);

        artefacto.ContenidoMarkdown.Should().Contain("- Calificación total: pendiente de evaluación");
        artefacto.ContenidoMarkdown.Should().NotContain("- Umbral de madurez:");
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
    public async Task Compilar_Idea_GeneraArtefactoCanonicoDesdeLaVersionConfirmada()
    {
        SembrarIdea(EstadoResultadoIdeaConsolidada.Madura, "eval_1");
        await _respuestas.GuardarEvaluacionAsync(CrearEvaluacion(), CancellationToken.None);

        var artefacto = await Construir().CompilarAsync(SolicitudIdea(), CancellationToken.None);

        artefacto.BlobPath.Should().Be("campanias/c_1/idea/idea_1.md");
        artefacto.Id.Should().Be("md_idea_1");
        artefacto.IdeaRef.Should().Be("idea_1");
        artefacto.VersionIdeaRef.Should().Be("idea_1_v2");
        artefacto.EvaluacionRef.Should().Be("eval_1");
        artefacto.RespuestaRef.Should().BeNull();
        var contenido = artefacto.ContenidoMarkdown;
        contenido.Should().Contain("# Idea de Ana");
        // El cuerpo es la version consolidada completa, no el ultimo aporte suelto (I-19 §10).
        contenido.Should().Contain("Idea completa y confirmada por el participante.");
        contenido.Should().Contain("- Estado de la idea: madura");
        contenido.Should().Contain("- Nivel de madurez: maduro");
        contenido.Should().Contain("- Estado de curaduría: pendiente");
        contenido.Should().Contain("- Motivo de cierre: umbral");
        // El historial deja ver que hubo una propuesta anterior y los aportes originales.
        contenido.Should().Contain("- v1 (descartada): Primera propuesta sin confirmar.");
        contenido.Should().Contain("- v2 (confirmada): Idea completa y confirmada por el participante.");
        contenido.Should().Contain("- inicial: Mi idea de mejora");
        contenido.Should().Contain("ID de idea: idea_1");
        contenido.Should().Contain("ID de versión vigente: idea_1_v2");
        _blob.Leer(artefacto.BlobPath).Should().Be(contenido);
    }

    [Fact]
    public async Task Compilar_IdeaRechazadaSinEvaluacion_ConservaElArtefactoSinCalificacion()
    {
        SembrarIdea(EstadoResultadoIdeaConsolidada.Rechazada, evaluacionId: null);

        var artefacto = await Construir().CompilarAsync(SolicitudIdea(), CancellationToken.None);

        artefacto.EvaluacionRef.Should().BeNull();
        artefacto.ContenidoMarkdown.Should().Contain("- Estado de la idea: rechazada");
        artefacto.ContenidoMarkdown.Should().Contain("- Estado de curaduría: no aplica");
        artefacto.ContenidoMarkdown.Should().Contain("ID de evaluación: sin evaluación");
        artefacto.ContenidoMarkdown.Should().NotContain("## Evaluación");
    }

    [Fact]
    public async Task Compilar_Idea_Regenerar_IncrementaVersionSobreLaMismaRuta()
    {
        SembrarIdea(EstadoResultadoIdeaConsolidada.Madura, "eval_1");
        await _respuestas.GuardarEvaluacionAsync(CrearEvaluacion(), CancellationToken.None);
        var compilador = Construir();

        var v1 = await compilador.CompilarAsync(SolicitudIdea(), CancellationToken.None);
        _reloj.Avanzar(TimeSpan.FromMinutes(5));
        var v2 = await compilador.CompilarAsync(SolicitudIdea(), CancellationToken.None);

        v1.Version.Should().Be(1);
        v2.Version.Should().Be(2);
        v2.Id.Should().Be(v1.Id);
        v2.BlobPath.Should().Be(v1.BlobPath);
        v2.CreadoEn.Should().Be(v1.CreadoEn);
    }

    [Fact]
    public async Task Compilar_IdeaInexistente_LanzaNoEncontrado()
    {
        var accion = () => Construir().CompilarAsync(SolicitudIdea(), CancellationToken.None);

        await accion.Should().ThrowAsync<ElTejido.Application.Common.ErrorNoEncontrado>();
    }

    [Fact]
    public async Task Compilar_RespuestaInexistente_LanzaNoEncontrado()
    {
        var accion = () => Construir().CompilarAsync(Solicitud(), CancellationToken.None);

        await accion.Should().ThrowAsync<ElTejido.Application.Common.ErrorNoEncontrado>();
    }

    private CompiladorMarkdown Construir(OpcionesConversacion? opciones = null)
        => new(_respuestas, _usuarios, _campanias, _blob, _reloj, _configuracion, opciones);

    /// <summary>Rúbrica 1-5 en la versión exacta que la evaluación dice haber usado (ARQ §8.3).</summary>
    private void SembrarRubrica(int version = 3)
        => _configuracion.ListarVersionesRubricaAsync("r_general", Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                Rubrica.Crear(
                    "r_general", "Rubrica", "desc", "# Rubrica", EscalaRubrica.Crear(1, 5),
                    new[] { CriterioRubrica.Crear("claridad", 1m) }, version, EstadoRubrica.Activa, Epoca, Epoca),
            });

    private static SolicitudCompilacion Solicitud()
        => new("c_1", TipoArtefactoMarkdown.Respuesta, "resp_1", "u_1", "p_1");

    private static SolicitudCompilacion SolicitudIdea()
        => new("c_1", TipoArtefactoMarkdown.Idea, null, null, null, "idea_1");

    /// <summary>Idea cerrada con dos versiones (una descartada y una confirmada) y su aporte original.</summary>
    private void SembrarIdea(EstadoResultadoIdeaConsolidada resultado, string? evaluacionId)
    {
        SembrarRespuesta();
        _respuestas.GuardarRespuestaAsync(
            Respuesta.Crear(
                "resp_1", "c_1", "u_1", "p_1", "conv_1", "Mi idea de mejora", "whatsapp", false,
                EstadoRespuesta.Recibida, Epoca, new[] { "t_oper" }, ideaId: "idea_1",
                tipoAporte: TipoAporteIdea.Inicial),
            CancellationToken.None).GetAwaiter().GetResult();

        var v1 = VersionIdeaConsolidada.Crear(
            "idea_1_v1", "c_1", "idea_1", 1, null, "Primera propuesta sin confirmar.", new[] { "resp_1" },
            new[] { "resp_1" }, TipoAporteIdea.Inicial, EstadoConfirmacionVersionIdea.Descartada, null, null, null,
            null, Epoca);
        var v2 = VersionIdeaConsolidada.Crear(
            "idea_1_v2", "c_1", "idea_1", 2, v1.Id, "Idea completa y confirmada por el participante.",
            new[] { "resp_1" }, new[] { "resp_1" }, TipoAporteIdea.Complemento,
            EstadoConfirmacionVersionIdea.Confirmada, null, null, null, null, Epoca, Epoca);
        _respuestas.GuardarVersionIdeaAsync(v1, CancellationToken.None).GetAwaiter().GetResult();
        _respuestas.GuardarVersionIdeaAsync(v2, CancellationToken.None).GetAwaiter().GetResult();

        var idea = IdeaConsolidada.Crear("idea_1", "c_1", "u_1", "p_1", "conv_1", "resp_1", 1, Epoca)
            .ConfirmarVersion(v2.Id, Epoca)
            .Cerrar(resultado, evaluacionId, resultado == EstadoResultadoIdeaConsolidada.Madura ? "umbral" : "rechazoParticipante", Epoca);
        _respuestas.GuardarIdeaConsolidadaAsync(idea, CancellationToken.None).GetAwaiter().GetResult();
    }

    private void Sembrar()
    {
        SembrarRespuesta();
        _respuestas.GuardarEvaluacionAsync(CrearEvaluacion(), CancellationToken.None).GetAwaiter().GetResult();
    }

    private void SembrarRespuesta(NivelMadurez nivelMadurez = NivelMadurez.Incubacion)
    {
        _respuestas.GuardarRespuestaAsync(
            Respuesta.Crear("resp_1", "c_1", "u_1", "p_1", "conv_1", "Mi idea de mejora", "whatsapp", false, EstadoRespuesta.Evaluada, Epoca, new[] { "t_oper" }, nivelMadurez: nivelMadurez),
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
        private readonly Dictionary<string, IdeaConsolidada> _ideas = new(StringComparer.Ordinal);
        private readonly Dictionary<string, VersionIdeaConsolidada> _versiones = new(StringComparer.Ordinal);

        public Task GuardarIdeaConsolidadaAsync(IdeaConsolidada idea, CancellationToken cancellationToken)
        {
            _ideas[idea.Id] = idea;
            return Task.CompletedTask;
        }

        public Task<IdeaConsolidada?> ObtenerIdeaConsolidadaAsync(string campaniaId, string ideaId, CancellationToken cancellationToken)
            => Task.FromResult(_ideas.GetValueOrDefault(ideaId));

        public Task<IReadOnlyCollection<IdeaConsolidada>> ListarIdeasConsolidadasAsync(string campaniaId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<IdeaConsolidada>>(_ideas.Values.Where(i => i.CampaniaId == campaniaId).ToArray());

        public Task GuardarVersionIdeaAsync(VersionIdeaConsolidada version, CancellationToken cancellationToken)
        {
            _versiones[version.Id] = version;
            return Task.CompletedTask;
        }

        public Task<VersionIdeaConsolidada?> ObtenerVersionIdeaAsync(string campaniaId, string versionId, CancellationToken cancellationToken)
            => Task.FromResult(_versiones.GetValueOrDefault(versionId));

        public Task<IReadOnlyCollection<VersionIdeaConsolidada>> ListarVersionesIdeaAsync(string campaniaId, string ideaId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<VersionIdeaConsolidada>>(
                _versiones.Values.Where(v => v.CampaniaId == campaniaId && v.IdeaId == ideaId).ToArray());

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

        public Task<IReadOnlyCollection<DominioEvaluacion>> ListarEvaluacionesAsync(string campaniaId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<DominioEvaluacion>>(_evaluaciones
                .Where(e => e.CampaniaId == campaniaId)
                .OrderByDescending(e => e.Fecha)
                .ToArray());

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
