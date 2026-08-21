using System.Diagnostics;
using System.Net;
using System.Text.Json;
using ElTejido.Application.Auth;
using ElTejido.Application.Respuestas;
using ElTejido.Domain.Common;
using ElTejido.Domain.Respuestas;
using ElTejido.Domain.Usuarios;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;
using DominioEvaluacion = ElTejido.Domain.Evaluacion.Evaluacion;

namespace ElTejido.IntegrationTests;

/// <summary>
/// P-34 §6 (H-10) — escala del listado de ideas a la campaña prevista para la convención (1.000
/// ideas). Mide el <b>costo en operaciones</b> del repositorio, que es lo que gobierna las RU: una
/// lectura puntual por versión y por idea era el problema. La medición de RU y latencia contra Cosmos
/// real sigue siendo una puerta operativa aparte; aquí se fija la propiedad que sí es determinista y
/// verificable en CI: el listado no hace lecturas puntuales por idea.
/// </summary>
public sealed class ListadoIdeasEscalaP34IntegrationTests
{
    private const string CookieSesion = "eltejido_sesion";
    private const string CsrfAdmin = "csrf-admin";
    private const string CampaniaId = "c_escala";
    private const int TotalIdeas = 1000;
    private const int AportesPorIdea = 5;
    private static readonly DateTimeOffset Epoca = DateTimeOffset.UnixEpoch;

    private readonly ITestOutputHelper _salida;

    public ListadoIdeasEscalaP34IntegrationTests(ITestOutputHelper salida)
    {
        _salida = salida;
    }

    [Fact]
    public async Task Listado_De_Mil_Ideas_No_Hace_Lecturas_Puntuales_Por_Idea()
    {
        var repo = SembrarCampania();
        using var fabrica = Construir(repo);
        using var client = ClienteAdmin(fabrica);

        var reloj = Stopwatch.StartNew();
        using var respuesta = await client.GetAsync($"/api/admin/ideas?campaniaId={CampaniaId}&pageSize=100");
        reloj.Stop();

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await respuesta.Content.ReadAsStringAsync();
        using var documento = JsonDocument.Parse(json);
        var raiz = documento.RootElement;

        _salida.WriteLine(
            $"P-34 H-10 · {TotalIdeas} ideas · pageSize=100 · "
            + $"lecturas puntuales de version={repo.LecturasPuntualesVersion} · "
            + $"consultas de versiones en bloque={repo.ConsultasVersionesEnBloque} "
            + $"(ids pedidos={repo.IdsVersionPedidos}) · "
            + $"consultas de ideas={repo.ConsultasIdeas} · "
            + $"latencia={reloj.ElapsedMilliseconds} ms");

        raiz.GetProperty("total").GetInt32().Should().Be(TotalIdeas);
        raiz.GetProperty("items").GetArrayLength().Should().Be(100);
        // La pagina conserva el texto de la version vigente: la optimizacion no cambia lo que se ve.
        raiz.GetProperty("items")[0].GetProperty("texto").GetString()
            .Should().Be("Version vigente de la idea 1.");

        // El nucleo de H-10: ni una lectura puntual por idea, y una sola consulta de versiones
        // acotada a los ids de la pagina (no de la campania).
        repo.LecturasPuntualesVersion.Should().Be(0);
        repo.ConsultasVersiones.Should().Be(0);
        repo.ConsultasIdeas.Should().Be(1);
        repo.ConsultasVersionesEnBloque.Should().Be(1);
        repo.IdsVersionPedidos.Should().BeLessThanOrEqualTo(200);
        repo.ConsultasRespuestas.Should().Be(0);
    }

    [Fact]
    public async Task Ultima_Pagina_Tambien_Resuelve_Solo_Sus_Versiones()
    {
        var repo = SembrarCampania();
        using var fabrica = Construir(repo);
        using var client = ClienteAdmin(fabrica);

        using var respuesta = await client.GetAsync(
            $"/api/admin/ideas?campaniaId={CampaniaId}&pageSize=100&page=10");

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await respuesta.Content.ReadAsStringAsync();
        using var documento = JsonDocument.Parse(json);
        var raiz = documento.RootElement;

        raiz.GetProperty("total").GetInt32().Should().Be(TotalIdeas);
        raiz.GetProperty("page").GetInt32().Should().Be(10);
        raiz.GetProperty("items").GetArrayLength().Should().Be(100);
        raiz.GetProperty("items")[99].GetProperty("texto").GetString()
            .Should().Be($"Version vigente de la idea {TotalIdeas}.");
        repo.LecturasPuntualesVersion.Should().Be(0);
        repo.IdsVersionPedidos.Should().BeLessThanOrEqualTo(200);
    }

    [Fact]
    public async Task Detalle_De_Una_Idea_No_Lee_La_Particion_Completa_De_Respuestas()
    {
        var repo = SembrarCampania();
        using var fabrica = Construir(repo);
        using var client = ClienteAdmin(fabrica);

        var reloj = Stopwatch.StartNew();
        using var respuesta = await client.GetAsync($"/api/admin/ideas/idea_0500?campaniaId={CampaniaId}");
        reloj.Stop();

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        _salida.WriteLine(
            $"P-34 H-10 · detalle de 1 idea entre {TotalIdeas} · "
            + $"documentos de respuesta leidos={repo.DocumentosRespuestaLeidos} "
            + $"(la particion tiene {repo.Respuestas.Count}) · "
            + $"latencia={reloj.ElapsedMilliseconds} ms");

        var json = await respuesta.Content.ReadAsStringAsync();
        json.Should().Contain("\"id\":\"idea_0500\"");
        json.Should().Contain("Aporte 1 de la idea 500.");
        json.Should().NotContain("Aporte 1 de la idea 501.");

        // El nucleo de la segunda mitad de H-10: se leen los aportes de la idea, no la particion.
        repo.ConsultasRespuestas.Should().Be(0);
        repo.ConsultasRespuestasPorIdea.Should().Be(1);
        repo.DocumentosRespuestaLeidos.Should().Be(AportesPorIdea);
    }

    private static RepositorioContador SembrarCampania()
    {
        var repo = new RepositorioContador();
        for (var indice = 1; indice <= TotalIdeas; indice++)
        {
            var ideaId = $"idea_{indice:D4}";
            var propuesta = VersionIdeaConsolidada.Crear(
                $"{ideaId}_v1", CampaniaId, ideaId, 1, null, $"Propuesta inicial de la idea {indice}.",
                new[] { $"resp_{indice}" }, new[] { $"resp_{indice}" }, TipoAporteIdea.Inicial,
                EstadoConfirmacionVersionIdea.Descartada, null, null, null, null, Epoca);
            var confirmada = VersionIdeaConsolidada.Crear(
                $"{ideaId}_v2", CampaniaId, ideaId, 2, propuesta.Id, $"Version vigente de la idea {indice}.",
                new[] { $"resp_{indice}" }, new[] { $"resp_{indice}" }, TipoAporteIdea.Complemento,
                EstadoConfirmacionVersionIdea.Confirmada, null, null, null, null, Epoca, Epoca);
            var idea = IdeaConsolidada
                .Crear(ideaId, CampaniaId, $"u_{indice % 50}", "p_1", $"conv_{indice}", $"resp_{indice}", 1, Epoca)
                .ConfirmarVersion(confirmada.Id, Epoca);

            repo.Ideas.Add(idea);
            repo.Versiones.Add(propuesta);
            repo.Versiones.Add(confirmada);
            for (var aporte = 1; aporte <= AportesPorIdea; aporte++)
            {
                repo.Respuestas.Add(Respuesta.Crear(
                    $"resp_{indice}_{aporte}", CampaniaId, $"u_{indice % 50}", "p_1", $"conv_{indice}",
                    $"Aporte {aporte} de la idea {indice}.", "whatsapp", false, EstadoRespuesta.Evaluada,
                    Epoca.AddMinutes(aporte), null, ideaId: ideaId,
                    tipoAporte: aporte == 1 ? TipoAporteIdea.Inicial : TipoAporteIdea.Complemento));
            }
        }

        return repo;
    }

    private static WebApplicationFactory<Program> Construir(IRepositorioRespuestas respuestas)
        => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(respuestas);
                services.AddSingleton<IServicioSesion, SesionesFake>();
            });
        });

    private static HttpClient ClienteAdmin(WebApplicationFactory<Program> fabrica)
    {
        var client = fabrica.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", $"{CookieSesion}=token-admin");
        return client;
    }

    /// <summary>
    /// Doble del repositorio que cuenta las operaciones como lo haría Cosmos: una lectura puntual por
    /// <c>ObtenerVersionIdeaAsync</c> y una consulta por cada listado dentro de la partición.
    /// </summary>
    private sealed class RepositorioContador : IRepositorioRespuestas
    {
        public List<IdeaConsolidada> Ideas { get; } = [];

        public List<VersionIdeaConsolidada> Versiones { get; } = [];

        public List<Respuesta> Respuestas { get; } = [];

        public int LecturasPuntualesVersion { get; private set; }

        public int ConsultasVersiones { get; private set; }

        public int ConsultasIdeas { get; private set; }

        public int ConsultasRespuestas { get; private set; }

        public int DocumentosRespuestaLeidos { get; private set; }

        public int ConsultasVersionesEnBloque { get; private set; }

        public int IdsVersionPedidos { get; private set; }

        public int ConsultasRespuestasPorIdea { get; private set; }

        public Task<IReadOnlyCollection<IdeaConsolidada>> ListarIdeasConsolidadasAsync(
            string campaniaId, CancellationToken cancellationToken)
        {
            ConsultasIdeas++;
            return Task.FromResult<IReadOnlyCollection<IdeaConsolidada>>(
                Ideas.Where(idea => idea.CampaniaId == campaniaId).ToArray());
        }

        public Task<IdeaConsolidada?> ObtenerIdeaConsolidadaAsync(
            string campaniaId, string ideaId, CancellationToken cancellationToken)
            => Task.FromResult(Ideas.FirstOrDefault(idea => idea.CampaniaId == campaniaId && idea.Id == ideaId));

        public Task<VersionIdeaConsolidada?> ObtenerVersionIdeaAsync(
            string campaniaId, string versionId, CancellationToken cancellationToken)
        {
            LecturasPuntualesVersion++;
            return Task.FromResult(Versiones.FirstOrDefault(v => v.CampaniaId == campaniaId && v.Id == versionId));
        }

        public Task<IReadOnlyCollection<VersionIdeaConsolidada>> ListarVersionesIdeaAsync(
            string campaniaId, string ideaId, CancellationToken cancellationToken)
        {
            ConsultasVersiones++;
            return Task.FromResult<IReadOnlyCollection<VersionIdeaConsolidada>>(
                Versiones.Where(v => v.CampaniaId == campaniaId && v.IdeaId == ideaId)
                    .OrderBy(v => v.NumeroVersion)
                    .ToArray());
        }

        /// <summary>P-34 §6: una sola consulta acotada por los ids de la pagina.</summary>
        public Task<IReadOnlyCollection<VersionIdeaConsolidada>> ListarVersionesDeCampaniaAsync(
            string campaniaId, IReadOnlyCollection<string> versionIds, CancellationToken cancellationToken)
        {
            ConsultasVersionesEnBloque++;
            IdsVersionPedidos += versionIds.Count;
            var ids = versionIds.ToHashSet(StringComparer.Ordinal);
            return Task.FromResult<IReadOnlyCollection<VersionIdeaConsolidada>>(
                Versiones.Where(v => v.CampaniaId == campaniaId && ids.Contains(v.Id)).ToArray());
        }

        /// <summary>P-34 §6: los aportes de una idea, sin recorrer la particion completa.</summary>
        public Task<IReadOnlyCollection<Respuesta>> ListarRespuestasPorIdeaAsync(
            string campaniaId, string ideaId, CancellationToken cancellationToken)
        {
            ConsultasRespuestasPorIdea++;
            var documentos = Respuestas.Where(r => r.CampaniaId == campaniaId && r.IdeaId == ideaId).ToArray();
            DocumentosRespuestaLeidos += documentos.Length;
            return Task.FromResult<IReadOnlyCollection<Respuesta>>(documentos);
        }

        public Task<IReadOnlyCollection<Respuesta>> ListarRespuestasAsync(
            string campaniaId, CancellationToken cancellationToken)
        {
            ConsultasRespuestas++;
            var documentos = Respuestas.Where(r => r.CampaniaId == campaniaId).ToArray();
            DocumentosRespuestaLeidos += documentos.Length;
            return Task.FromResult<IReadOnlyCollection<Respuesta>>(documentos);
        }

        public Task GuardarRespuestaAsync(Respuesta respuesta, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<Respuesta?> ObtenerRespuestaAsync(string campaniaId, string respuestaId, CancellationToken cancellationToken)
            => Task.FromResult(Respuestas.FirstOrDefault(r => r.Id == respuestaId));

        public Task GuardarEvaluacionAsync(DominioEvaluacion evaluacion, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<DominioEvaluacion?> ObtenerEvaluacionPorRespuestaAsync(string campaniaId, string respuestaId, CancellationToken cancellationToken)
            => Task.FromResult<DominioEvaluacion?>(null);

        public Task<DominioEvaluacion?> ObtenerEvaluacionPorIdAsync(string campaniaId, string evaluacionId, CancellationToken cancellationToken)
            => Task.FromResult<DominioEvaluacion?>(null);

        public Task<IReadOnlyCollection<DominioEvaluacion>> ListarEvaluacionesAsync(string campaniaId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<DominioEvaluacion>>([]);

        public Task<int> ContarEvaluacionesUsuarioAsync(string campaniaId, string usuarioId, CancellationToken cancellationToken)
            => Task.FromResult(0);

        public Task<long> SumarTokensCampaniaAsync(string campaniaId, CancellationToken cancellationToken)
            => Task.FromResult(0L);

        public Task GuardarArtefactoAsync(ArtefactoMarkdown artefacto, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<ArtefactoMarkdown?> ObtenerArtefactoAsync(string campaniaId, string artefactoId, CancellationToken cancellationToken)
            => Task.FromResult<ArtefactoMarkdown?>(null);

        public Task<IReadOnlyCollection<ArtefactoMarkdown>> ListarArtefactosAsync(string campaniaId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<ArtefactoMarkdown>>([]);

        public Task<ConteoBorradoRespuestas> EliminarPorUsuarioAsync(string campaniaId, string? usuarioId, CancellationToken cancellationToken)
            => Task.FromResult(new ConteoBorradoRespuestas(0, 0, 0, []));
    }

    private sealed class SesionesFake : IServicioSesion
    {
        public Task<SesionEmitida> EmitirAsync(Usuario usuario, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<PrincipalSesion?> ValidarAsync(string token, CancellationToken cancellationToken)
            => Task.FromResult<PrincipalSesion?>(token == "token-admin"
                ? new PrincipalSesion("u_admin", "Admin", RolUsuario.Admin, CsrfAdmin, DateTimeOffset.UtcNow.AddMinutes(30))
                : null);
    }
}
