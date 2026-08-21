using System.IO.Compression;
using System.Net;
using System.Text;
using ClosedXML.Excel;
using ElTejido.Application.Auth;
using ElTejido.Application.Campanas;
using ElTejido.Application.Respuestas;
using ElTejido.Application.Usuarios;
using ElTejido.Domain.Campanas;
using ElTejido.Domain.Common;
using ElTejido.Domain.Evaluacion;
using ElTejido.Domain.Identidad;
using ElTejido.Domain.Respuestas;
using ElTejido.Domain.Usuarios;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using DominioEvaluacion = ElTejido.Domain.Evaluacion.Evaluacion;

namespace ElTejido.IntegrationTests;

/// <summary>
/// P-34 §4.5 (04 §5.8): exportación de resultados y ZIP de documentos. Verifica lo que hace útil al
/// archivo: el alcance declarado dentro de él, el mismo filtro que la pantalla, el anonimizado y el
/// tope explícito.
/// </summary>
public sealed class ExportacionResultadosIntegrationTests
{
    private const string CookieSesion = "eltejido_sesion";
    private const string CsrfAdmin = "csrf-admin";
    private const string CampaniaId = "c_1";
    private static readonly DateTimeOffset Epoca = DateTimeOffset.UnixEpoch;

    [Fact]
    public async Task Csv_LlevaBomHojaDeFiltrosYLasFilasDelAlcance()
    {
        using var fabrica = Construir();
        using var client = ClienteAdmin(fabrica);

        using var respuesta = await client.GetAsync(
            $"/api/admin/campanias/{CampaniaId}/exportar?recurso=ideas&formato=csv");

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        respuesta.Content.Headers.ContentType!.ToString().Should().StartWith("text/csv");
        respuesta.Content.Headers.ContentDisposition!.ToString()
            .Should().Contain("Convencion-GHT-2026_ideas_");

        var bytes = await respuesta.Content.ReadAsByteArrayAsync();
        // UTF-8 con BOM: sin él, Excel abre los acentos rotos.
        bytes.Take(3).Should().Equal(0xEF, 0xBB, 0xBF);

        var texto = Encoding.UTF8.GetString(bytes);
        texto.Should().Contain("# Campaña: Convención GHT 2026");
        texto.Should().Contain("# Recurso: ideas");
        texto.Should().Contain("# Total de filas: 2");
        texto.Should().Contain("# Exportado por: Admin (u_admin)");
        texto.Should().Contain("Participante,Código,Área");
        texto.Should().Contain("Ana Perez");
        texto.Should().Contain("Riego por goteo, de noche");
        // El texto con coma va entre comillas: si no, el CSV se corre de columna.
        texto.Should().Contain("\"Riego por goteo, de noche\"");
    }

    [Fact]
    public async Task Xlsx_TraeLaHojaDeFiltrosPrimeroYLaTablaDespues()
    {
        using var fabrica = Construir();
        using var client = ClienteAdmin(fabrica);

        using var respuesta = await client.GetAsync($"/api/admin/campanias/{CampaniaId}/exportar");

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        respuesta.Content.Headers.ContentType!.ToString()
            .Should().StartWith("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

        using var flujo = new MemoryStream(await respuesta.Content.ReadAsByteArrayAsync());
        using var libro = new XLWorkbook(flujo);
        libro.Worksheets.Select(hoja => hoja.Name).Should().Equal("Filtros aplicados", "Ideas");
        libro.Worksheet("Ideas").Cell(1, 1).GetString().Should().Be("Participante");
        libro.Worksheet("Ideas").Cell(2, 1).GetString().Should().Be("Ana Perez");
        libro.Worksheet("Filtros aplicados").Cell(1, 2).GetString().Should().Be("Convención GHT 2026");
    }

    // El archivo dice exactamente lo que el administrador vio en pantalla: mismo filtro, mismo orden.
    [Fact]
    public async Task Exportar_RespetaLosFiltrosDelListadoYLosDeclaraEnElArchivo()
    {
        using var fabrica = Construir();
        using var client = ClienteAdmin(fabrica);

        using var respuesta = await client.GetAsync(
            $"/api/admin/campanias/{CampaniaId}/exportar?formato=csv&area=Operaciones");

        var texto = await respuesta.Content.ReadAsStringAsync();
        texto.Should().Contain("# Filtro · area: Operaciones");
        texto.Should().Contain("# Total de filas: 1");
        texto.Should().Contain("Ana Perez");
        texto.Should().NotContain("Beto Ruiz");
    }

    [Fact]
    public async Task Exportar_Anonimizado_NoDejaRastroDelNombre()
    {
        using var fabrica = Construir();
        using var client = ClienteAdmin(fabrica);

        using var respuesta = await client.GetAsync(
            $"/api/admin/campanias/{CampaniaId}/exportar?formato=csv&anonimizado=true");

        var texto = await respuesta.Content.ReadAsStringAsync();
        texto.Should().NotContain("Ana Perez");
        texto.Should().NotContain("Beto Ruiz");
        texto.Should().Contain("U-000042");
        texto.Should().Contain("# Anonimizado: sí");
        // Los atributos que no identifican por sí solos se conservan: el archivo sigue siendo útil.
        texto.Should().Contain("Operaciones");
    }

    [Fact]
    public async Task Exportar_Aportes_UnaFilaPorMensajeConSuVersion()
    {
        using var fabrica = Construir();
        using var client = ClienteAdmin(fabrica);

        using var respuesta = await client.GetAsync(
            $"/api/admin/campanias/{CampaniaId}/exportar?recurso=aportes&formato=csv");

        var texto = await respuesta.Content.ReadAsStringAsync();
        texto.Should().Contain("# Recurso: aportes");
        texto.Should().Contain("Tipo de aporte");
        texto.Should().Contain("Idea inicial de Ana");
        texto.Should().Contain("Aporte de Beto");
        // El aporte histórico sin idea no pertenece al alcance de la pantalla.
        texto.Should().NotContain("Historico sin idea");
    }

    [Fact]
    public async Task Exportar_Evaluaciones_LlevaCriteriosRubricaYModelo()
    {
        using var fabrica = Construir();
        using var client = ClienteAdmin(fabrica);

        using var respuesta = await client.GetAsync(
            $"/api/admin/campanias/{CampaniaId}/exportar?recurso=evaluaciones&formato=csv");

        var texto = await respuesta.Content.ReadAsStringAsync();
        texto.Should().Contain("Calificación por criterio");
        texto.Should().Contain("claridad=4");
        texto.Should().Contain("gpt-4o-mini");
        texto.Should().Contain("rub_1");
    }

    [Fact]
    public async Task Exportar_ParametroInvalido_Responde400()
    {
        using var fabrica = Construir();
        using var client = ClienteAdmin(fabrica);

        using var recurso = await client.GetAsync($"/api/admin/campanias/{CampaniaId}/exportar?recurso=todo");
        using var formato = await client.GetAsync($"/api/admin/campanias/{CampaniaId}/exportar?formato=pdf");

        recurso.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await recurso.Content.ReadAsStringAsync()).Should().Contain("valor_invalido");
        formato.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Documentos_ZipConNombresLegiblesYSoloLoFiltrado()
    {
        using var fabrica = Construir();
        using var client = ClienteAdmin(fabrica);

        using var respuesta = await client.GetAsync(
            $"/api/admin/campanias/{CampaniaId}/documentos.zip?area=Operaciones");

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        respuesta.Content.Headers.ContentType!.ToString().Should().Be("application/zip");

        using var flujo = new MemoryStream(await respuesta.Content.ReadAsByteArrayAsync());
        using var zip = new ZipArchive(flujo, ZipArchiveMode.Read);
        zip.Entries.Select(entrada => entrada.FullName).Should().Equal("U-000042_Ana-Perez_idea-1.md");

        using var lector = new StreamReader(zip.Entries[0].Open());
        (await lector.ReadToEndAsync()).Should().Contain("# Idea de Ana");
    }

    [Fact]
    public async Task Documentos_Anonimizado_NoNombraAlParticipanteNiEnElNombreDelArchivo()
    {
        using var fabrica = Construir();
        using var client = ClienteAdmin(fabrica);

        using var respuesta = await client.GetAsync(
            $"/api/admin/campanias/{CampaniaId}/documentos.zip?anonimizado=true");

        using var flujo = new MemoryStream(await respuesta.Content.ReadAsByteArrayAsync());
        using var zip = new ZipArchive(flujo, ZipArchiveMode.Read);
        zip.Entries.Select(entrada => entrada.FullName).Should().Contain("U-000042_idea-1.md");
        zip.Entries.Should().NotContain(entrada => entrada.FullName.Contains("Ana", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Exportar_ExigeSesionAdministrativa()
    {
        using var fabrica = Construir();
        using var anonimo = fabrica.CreateClient();
        using var visor = ClienteVisor(fabrica);

        using var sinSesion = await anonimo.GetAsync($"/api/admin/campanias/{CampaniaId}/exportar");
        using var conVisor = await visor.GetAsync($"/api/admin/campanias/{CampaniaId}/exportar?formato=csv");

        sinSesion.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        // Es un GET: el visor puede leer, como en el resto de las consultas de resultados.
        conVisor.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static WebApplicationFactory<Program> Construir()
    {
        var respuestas = Substitute.For<IRepositorioRespuestas>();
        var usuarios = Substitute.For<IRepositorioUsuarios>();
        var campanias = Substitute.For<IRepositorioCampanias>();

        var ana = Usuario.Crear(
            "u_ana", 42, "Ana Perez", NumeroWhatsApp.FromNormalized("573001112233"), RolUsuario.Participante,
            EstadoRegistro.Activo, "Operaciones", "Flores El Aljibe", null, null, Epoca, Epoca, sede: "AL");
        var beto = Usuario.Crear(
            "u_beto", 43, "Beto Ruiz", NumeroWhatsApp.FromNormalized("573001112244"), RolUsuario.Participante,
            EstadoRegistro.Activo, "Comercial", "Flores El Aljibe", null, null, Epoca, Epoca, sede: "AL");
        usuarios
            .ListarUsuariosPorIdsAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(llamada => new[] { ana, beto }
                .Where(usuario => ((IReadOnlyCollection<string>)llamada[0]).Contains(usuario.Id))
                .ToArray());

        campanias.ObtenerCampaniaPorIdAsync(CampaniaId, Arg.Any<CancellationToken>())
            .Returns(Campania.Crear(
                CampaniaId,
                "Convención GHT 2026",
                "Descripcion",
                "Objetivo",
                EstadoCampania.Activa,
                null,
                null,
                "rub_1",
                null,
                "llm_1",
                ConfigMarkdown.Crear(TipoArtefactoMarkdown.Idea),
                ConfigConversacional.Crear(1, "Gracias."),
                LimitesSeguridad.Crear(1500, 10, 2),
                null,
                Epoca,
                Epoca));

        var versionAna = VersionIdeaConsolidada.Crear(
            "idea_ana_v1", CampaniaId, "idea_ana", 1, null, "Riego por goteo, de noche", ["resp_ana"], ["resp_ana"],
            TipoAporteIdea.Inicial, EstadoConfirmacionVersionIdea.Confirmada, null, null, null, null, Epoca, Epoca);
        var versionBeto = VersionIdeaConsolidada.Crear(
            "idea_beto_v1", CampaniaId, "idea_beto", 1, null, "Turnos rotativos", ["resp_beto"], ["resp_beto"],
            TipoAporteIdea.Inicial, EstadoConfirmacionVersionIdea.Propuesta, null, null, null, null, Epoca);
        var ideaAna = IdeaConsolidada.Crear("idea_ana", CampaniaId, "u_ana", "p_1", "conv_ana", "resp_ana", 1, Epoca)
            .ConfirmarVersion(versionAna.Id, Epoca);
        var ideaBeto = IdeaConsolidada.Crear("idea_beto", CampaniaId, "u_beto", "p_1", "conv_beto", "resp_beto", 1, Epoca)
            .ConPropuesta(versionBeto.Id, Epoca);

        respuestas.ListarIdeasConsolidadasAsync(CampaniaId, Arg.Any<CancellationToken>())
            .Returns(new[] { ideaAna, ideaBeto });
        respuestas
            .ListarVersionesDeIdeasAsync(CampaniaId, Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(llamada => new[] { versionAna, versionBeto }
                .Where(version => ((IReadOnlyCollection<string>)llamada[1]).Contains(version.IdeaId))
                .ToArray());
        respuestas
            .ListarVersionesDeCampaniaAsync(CampaniaId, Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(llamada => new[] { versionAna, versionBeto }
                .Where(version => ((IReadOnlyCollection<string>)llamada[1]).Contains(version.Id))
                .ToArray());

        var evaluacion = DominioEvaluacion.Crear(
            "eval_ana", CampaniaId, "resp_ana", "u_ana", "p_1", "rub_1", 1, "pr_1", 1, "llm_1",
            new ConfigLlmSnapshot("AzureOpenAI", "gpt-4o-mini", "https://x", new Dictionary<string, object?>()),
            new Dictionary<string, decimal> { ["claridad"] = 1m },
            [CalificacionCriterio.Crear("claridad", 4m, "clara")],
            4.5m, "explica", "Buena idea", RecomendacionEvaluacion.Cerrar, null, ["riego"], ["agua"], false, Epoca,
            ideaId: "idea_ana");
        respuestas.ListarEvaluacionesAsync(CampaniaId, Arg.Any<CancellationToken>()).Returns(new[] { evaluacion });
        respuestas
            .ListarEvaluacionesPorIdsAsync(CampaniaId, Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(new[] { evaluacion });

        respuestas.ListarRespuestasAsync(CampaniaId, Arg.Any<CancellationToken>()).Returns(new[]
        {
            Respuesta.Crear("resp_ana", CampaniaId, "u_ana", "p_1", "conv_ana", "Idea inicial de Ana", "whatsapp",
                false, EstadoRespuesta.Evaluada, Epoca, null, ideaId: "idea_ana", tipoAporte: TipoAporteIdea.Inicial),
            Respuesta.Crear("resp_beto", CampaniaId, "u_beto", "p_1", "conv_beto", "Aporte de Beto", "whatsapp",
                false, EstadoRespuesta.Evaluada, Epoca, null, ideaId: "idea_beto", tipoAporte: TipoAporteIdea.Inicial),
            Respuesta.Crear("resp_viejo", CampaniaId, "u_ana", "p_1", "conv_ana", "Historico sin idea", "whatsapp",
                false, EstadoRespuesta.Evaluada, Epoca, null),
        });

        respuestas.ListarArtefactosAsync(CampaniaId, Arg.Any<CancellationToken>()).Returns(new[]
        {
            ArtefactoMarkdown.Crear(
                "md_ana", CampaniaId, TipoArtefactoMarkdown.Idea, "u_ana", "p_1", "resp_ana", "eval_ana",
                "# Idea de Ana\n\nRiego por goteo", "campanias/c_1/idea/idea_ana.md", EstadoArtefacto.Generado, 1,
                Epoca, Epoca, ideaRef: "idea_ana", versionIdeaRef: "idea_ana_v1"),
        });

        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(respuestas);
                services.AddSingleton(usuarios);
                services.AddSingleton(campanias);
                services.AddSingleton<IServicioSesion, SesionesFake>();
            });
        });
    }

    private static HttpClient ClienteAdmin(WebApplicationFactory<Program> fabrica)
    {
        var client = fabrica.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", $"{CookieSesion}=token-admin");
        return client;
    }

    private static HttpClient ClienteVisor(WebApplicationFactory<Program> fabrica)
    {
        var client = fabrica.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", $"{CookieSesion}=token-visor");
        return client;
    }

    private sealed class SesionesFake : IServicioSesion
    {
        public Task<SesionEmitida> EmitirAsync(Usuario usuario, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<PrincipalSesion?> ValidarAsync(string token, CancellationToken cancellationToken)
            => Task.FromResult<PrincipalSesion?>(token switch
            {
                "token-admin" => new PrincipalSesion("u_admin", "Admin", RolUsuario.Admin, CsrfAdmin, DateTimeOffset.UtcNow.AddMinutes(30)),
                "token-visor" => new PrincipalSesion("u_visor", "Visor", RolUsuario.Visor, CsrfAdmin, DateTimeOffset.UtcNow.AddMinutes(30)),
                _ => null,
            });
    }
}
