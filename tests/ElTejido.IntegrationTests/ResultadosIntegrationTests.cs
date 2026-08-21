using System.Net;
using ElTejido.Application.Auth;
using ElTejido.Application.Conversacion;
using ElTejido.Application.Markdown;
using ElTejido.Application.Respuestas;
using ElTejido.Application.Usuarios;
using ElTejido.Domain.Campanas;
using ElTejido.Domain.Common;
using ElTejido.Domain.Conversaciones;
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
/// Verifica los endpoints de consulta de resultados (04 §5.8): respuestas/evaluaciones,
/// conversaciones, Markdown (detalle, raw y regeneracion). Repos mockeados.
/// </summary>
public sealed class ResultadosIntegrationTests
{
    private const string CookieSesion = "eltejido_sesion";
    private const string CsrfAdmin = "csrf-admin";
    private const string CampaniaId = "c_1";
    private static readonly DateTimeOffset Epoca = DateTimeOffset.UnixEpoch;

    [Fact]
    public async Task Respuestas_ListaYDetalleConEvaluacion()
    {
        using var fabrica = Construir();
        using var client = ClienteAdmin(fabrica);

        using var lista = await client.GetAsync($"/api/admin/respuestas?campaniaId={CampaniaId}");
        lista.StatusCode.Should().Be(HttpStatusCode.OK);
        var listaJson = await lista.Content.ReadAsStringAsync();
        listaJson.Should().Contain("resp_1");
        listaJson.Should().Contain("\"ideaIndice\":1");
        listaJson.Should().Contain("\"respuestaPadreId\":\"wamid.1\"");
        listaJson.Should().Contain("\"ideaRaizId\":\"resp_1\"");
        listaJson.Should().Contain("\"revisionIndice\":0");
        // I-17: el DTO expone el nivel de madurez sellado (incubacion por defecto / maduro).
        listaJson.Should().Contain("\"nivelMadurez\":\"incubacion\"");
        listaJson.Should().Contain("\"nivelMadurez\":\"maduro\"");

        using var detalle = await client.GetAsync($"/api/admin/respuestas/resp_1?campaniaId={CampaniaId}");
        detalle.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await detalle.Content.ReadAsStringAsync();
        json.Should().Contain("\"recomendacion\":\"cerrar\"");
        json.Should().Contain("eval_actual");
        json.Should().Contain("\"ideaIndice\":1");
    }

    [Fact]
    public async Task Respuestas_FiltroPorNivelMadurez_SeparaMadurasEIncubacion()
    {
        using var fabrica = Construir();
        using var client = ClienteAdmin(fabrica);

        using var maduras = await client.GetAsync($"/api/admin/respuestas?campaniaId={CampaniaId}&nivelMadurez=maduro");
        maduras.StatusCode.Should().Be(HttpStatusCode.OK);
        var madurasJson = await maduras.Content.ReadAsStringAsync();
        madurasJson.Should().Contain("resp_2");
        madurasJson.Should().NotContain("resp_1");

        using var incubacion = await client.GetAsync($"/api/admin/respuestas?campaniaId={CampaniaId}&nivelMadurez=incubacion");
        var incubacionJson = await incubacion.Content.ReadAsStringAsync();
        incubacionJson.Should().Contain("resp_1");
        incubacionJson.Should().NotContain("resp_2");
    }

    [Fact]
    public async Task Ideas_ListaUnaFilaPorIdeaConSuEstadoYTextoVigente()
    {
        using var fabrica = Construir();
        using var client = ClienteAdmin(fabrica);

        using var lista = await client.GetAsync($"/api/admin/ideas?campaniaId={CampaniaId}");

        lista.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await lista.Content.ReadAsStringAsync();
        json.Should().Contain("\"total\":2");
        json.Should().Contain("\"id\":\"idea_1\"");
        json.Should().Contain("\"texto\":\"Idea consolidada y confirmada.\"");
        json.Should().Contain("\"estadoResultado\":\"madura\"");
        json.Should().Contain("\"estadoCuraduria\":\"pendiente\"");
        json.Should().Contain("\"nivelMadurez\":\"maduro\"");
        // La idea rechazada conserva su texto propuesto y se marca como no confirmada.
        json.Should().Contain("\"estadoResultado\":\"rechazada\"");
        json.Should().Contain("\"confirmada\":false");
    }

    [Fact]
    public async Task Ideas_FiltroPorEstadoResultado_SeparaMadurasDeRechazadas()
    {
        using var fabrica = Construir();
        using var client = ClienteAdmin(fabrica);

        using var maduras = await client.GetAsync($"/api/admin/ideas?campaniaId={CampaniaId}&estadoResultado=madura");
        var madurasJson = await maduras.Content.ReadAsStringAsync();
        using var curaduria = await client.GetAsync($"/api/admin/ideas?campaniaId={CampaniaId}&estadoCuraduria=pendiente");
        var curaduriaJson = await curaduria.Content.ReadAsStringAsync();

        madurasJson.Should().Contain("idea_1").And.NotContain("idea_2");
        curaduriaJson.Should().Contain("idea_1").And.NotContain("idea_2");
    }

    [Fact]
    public async Task Ideas_DetalleDevuelveVersionesAportesYEvaluacion()
    {
        using var fabrica = Construir();
        using var client = ClienteAdmin(fabrica);

        using var detalle = await client.GetAsync($"/api/admin/ideas/idea_1?campaniaId={CampaniaId}");

        detalle.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await detalle.Content.ReadAsStringAsync();
        json.Should().Contain("\"versionConfirmada\"");
        json.Should().Contain("\"estadoConfirmacion\":\"confirmada\"");
        json.Should().Contain("\"numeroVersion\":1");
        json.Should().Contain("eval_1");
        json.Should().Contain("\"aportes\"");
        // P-34 §6: los aportes llegan de la consulta por `ideaId`; el doble no responde el listado
        // completo de la campaña, así que este texto solo aparece si se usó la ruta nueva.
        json.Should().Contain("Mi idea");
    }

    // P-34 §4.1 (04 §5.8): la identidad la resuelve el servidor y viaja embebida, con la calificación
    // vigente. Sin usuario, la fila lo dice en vez de dejar un id técnico haciéndose pasar por nombre.
    [Fact]
    public async Task P34_Ideas_EmbebeParticipanteYCalificacionVigente()
    {
        using var fabrica = Construir();
        using var client = ClienteAdmin(fabrica);

        using var lista = await client.GetAsync($"/api/admin/ideas?campaniaId={CampaniaId}");

        lista.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await lista.Content.ReadAsStringAsync();
        json.Should().Contain("\"nombre\":\"Ana Perez\"");
        json.Should().Contain("\"codigoUsuarioLegible\":\"U-000042\"");
        json.Should().Contain("\"area\":\"Operaciones\"");
        json.Should().Contain("\"sede\":\"AL\"");
        json.Should().Contain("\"estado\":\"activo\"");
        json.Should().Contain("\"resuelto\":true");
        json.Should().Contain("\"calificacionTotal\":4");
        json.Should().Contain("\"evaluadaEn\"");
        json.Should().Contain("\"preguntaSeguimiento\":\"¿Qué impacto tendría esta idea en el equipo?\"");
        json.Should().Contain("\"preguntaSeguimiento\":null");
        // El participante que ya no existe viaja marcado, nunca omitido.
        json.Should().Contain("\"usuarioId\":\"u_2\"");
        json.Should().Contain("\"resuelto\":false");
        // No se filtra PII que el listado no necesita.
        json.Should().NotContain("573001112233");
        json.Should().NotContain("\"email\"");
    }

    [Fact]
    public async Task P34_Ideas_FiltraPorAtributosDelParticipante()
    {
        using var fabrica = Construir();
        using var client = ClienteAdmin(fabrica);

        using var operaciones = await client.GetAsync(
            $"/api/admin/ideas?campaniaId={CampaniaId}&area=OPERACIONES");
        using var otraArea = await client.GetAsync(
            $"/api/admin/ideas?campaniaId={CampaniaId}&area=Comercial");

        var operacionesJson = await operaciones.Content.ReadAsStringAsync();
        var otraAreaJson = await otraArea.Content.ReadAsStringAsync();
        operacionesJson.Should().Contain("\"total\":1").And.Contain("idea_1").And.NotContain("idea_2");
        otraAreaJson.Should().Contain("\"total\":0");
    }

    [Fact]
    public async Task P34_Ideas_BusquedaLibreMiraNombreCodigoYTexto()
    {
        using var fabrica = Construir();
        using var client = ClienteAdmin(fabrica);

        using var porNombre = await client.GetAsync($"/api/admin/ideas?campaniaId={CampaniaId}&q=p%C3%A9rez");
        // «consolidada» solo aparece en el texto vigente de idea_1, no en el nombre ni en el código.
        using var porTexto = await client.GetAsync($"/api/admin/ideas?campaniaId={CampaniaId}&q=consolidada");
        using var sinCoincidencia = await client.GetAsync($"/api/admin/ideas?campaniaId={CampaniaId}&q=zzzz");

        (await porNombre.Content.ReadAsStringAsync()).Should().Contain("idea_1").And.NotContain("idea_2");
        (await porTexto.Content.ReadAsStringAsync()).Should().Contain("\"id\":\"idea_1\"").And.NotContain("\"id\":\"idea_2\"");
        (await sinCoincidencia.Content.ReadAsStringAsync()).Should().Contain("\"total\":0");
    }

    [Fact]
    public async Task P34_Ideas_OrdenaPorCalificacionDejandoSinEvaluarAlFinal()
    {
        using var fabrica = Construir();
        using var client = ClienteAdmin(fabrica);

        using var respuesta = await client.GetAsync(
            $"/api/admin/ideas?campaniaId={CampaniaId}&orden=calificacion&dir=desc");

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await respuesta.Content.ReadAsStringAsync();
        json.IndexOf("idea_1", StringComparison.Ordinal)
            .Should().BeLessThan(json.IndexOf("idea_2", StringComparison.Ordinal));
        json.Should().Contain("\"total\":2");
    }

    // Un rango o un orden mal escritos se rechazan: devolver una lista vacía seria decir que no hay
    // datos cuando lo que hay es una consulta invalida.
    [Fact]
    public async Task P34_Ideas_CriteriosInvalidos_Responde400ConTodosLosMotivos()
    {
        using var fabrica = Construir();
        using var client = ClienteAdmin(fabrica);

        using var respuesta = await client.GetAsync(
            $"/api/admin/ideas?campaniaId={CampaniaId}&desde=ayer&orden=magia&calificacionMin=5&calificacionMax=1");

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await respuesta.Content.ReadAsStringAsync();
        json.Should().Contain("VALIDATION_ERROR");
        json.Should().Contain("desde");
        json.Should().Contain("formato_invalido");
        json.Should().Contain("orden");
        json.Should().Contain("valor_invalido");
        json.Should().Contain("rango_invalido");
    }

    [Fact]
    public async Task Ideas_SinCampaniaId_Responde400()
    {
        using var fabrica = Construir();
        using var client = ClienteAdmin(fabrica);

        using var respuesta = await client.GetAsync("/api/admin/ideas");

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Respuestas_SinCampaniaId_Responde400()
    {
        using var fabrica = Construir();
        using var client = ClienteAdmin(fabrica);

        using var respuesta = await client.GetAsync("/api/admin/respuestas");

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Evaluaciones_ListaDiagnosticaEnlacesSinExponerTextoLibre()
    {
        using var fabrica = Construir();
        using var client = ClienteAdmin(fabrica);

        using var respuesta = await client.GetAsync($"/api/admin/evaluaciones?campaniaId={CampaniaId}");

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await respuesta.Content.ReadAsStringAsync();
        json.Should().Contain("\"total\":5");
        json.Should().Contain("\"enlazadas\":1");
        json.Should().Contain("\"huerfanas\":2");
        json.Should().Contain("\"superadas\":1");
        json.Should().Contain("\"sinVersionIdea\":1");
        json.Should().Contain("\"id\":\"eval_actual\"");
        json.Should().Contain("\"enlace\":\"enlazada\"");
        json.Should().Contain("\"id\":\"eval_superada\"");
        json.Should().Contain("\"enlace\":\"superada\"");
        json.Should().Contain("\"motivoDesenlace\":\"evaluacion_mas_reciente_existe\"");
        json.Should().Contain("\"id\":\"eval_huerfana\"");
        json.Should().Contain("\"motivoDesenlace\":\"respuesta_inexistente\"");
        json.Should().Contain("\"id\":\"eval_sin_respuesta\"");
        json.Should().Contain("\"motivoDesenlace\":\"respuesta_id_vacio\"");
        json.Should().Contain("\"id\":\"eval_sin_version\"");
        json.Should().Contain("\"enlace\":\"sin_version_idea\"");
        json.Should().NotContain("\"explicacion\"");
        json.Should().NotContain("\"retroalimentacionEnviada\"");
        json.Should().NotContain("\"parafraseoDevuelto\"");
        json.Should().NotContain("\"repreguntaSugerida\"");
        json.Should().NotContain("\"calificacionPorCriterio\"");
        json.Should().NotContain("\"configLLMSnapshot\"");
    }

    [Fact]
    public async Task Evaluaciones_FiltrosYAutorizacionRespetanElContrato()
    {
        using var fabrica = Construir();
        using var admin = ClienteAdmin(fabrica);
        using var visor = ClienteVisor(fabrica);
        using var anonimo = fabrica.CreateClient();

        using var huerfanas = await admin.GetAsync($"/api/admin/evaluaciones?campaniaId={CampaniaId}&enlace=huerfana&pageSize=1");
        using var visorRespuesta = await visor.GetAsync($"/api/admin/evaluaciones?campaniaId={CampaniaId}");
        using var sinCampania = await admin.GetAsync("/api/admin/evaluaciones");
        using var sinSesion = await anonimo.GetAsync($"/api/admin/evaluaciones?campaniaId={CampaniaId}");

        huerfanas.StatusCode.Should().Be(HttpStatusCode.OK);
        var jsonHuerfanas = await huerfanas.Content.ReadAsStringAsync();
        jsonHuerfanas.Should().Contain("\"total\":2");
        jsonHuerfanas.Should().Contain("\"huerfanas\":2");
        jsonHuerfanas.Should().Contain("\"pageSize\":1");
        jsonHuerfanas.Should().Contain("\"items\":[{");
        visorRespuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        sinCampania.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        sinSesion.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Markdown_RawDevuelveContenido()
    {
        using var fabrica = Construir();
        using var client = ClienteAdmin(fabrica);

        using var raw = await client.GetAsync($"/api/admin/markdown/md_1/raw?campaniaId={CampaniaId}");

        raw.StatusCode.Should().Be(HttpStatusCode.OK);
        raw.Content.Headers.ContentType!.MediaType.Should().Be("text/markdown");
        (await raw.Content.ReadAsStringAsync()).Should().Contain("# Aporte");
    }

    [Fact]
    public async Task Markdown_Regenerar_RequiereCsrfYDevuelveArtefacto()
    {
        using var fabrica = Construir();
        using var client = ClienteAdmin(fabrica);

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/markdown/md_1/regenerar?campaniaId={CampaniaId}");
        request.Headers.Add("X-CSRF-Token", CsrfAdmin);
        using var respuesta = await client.SendAsync(request);

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        (await respuesta.Content.ReadAsStringAsync()).Should().Contain("\"version\":2");
    }

    [Fact]
    public async Task Conversaciones_DetalleIncluyeMensajes()
    {
        using var fabrica = Construir();
        using var client = ClienteAdmin(fabrica);

        using var detalle = await client.GetAsync($"/api/admin/conversaciones/conv_1?campaniaId={CampaniaId}");

        detalle.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await detalle.Content.ReadAsStringAsync();
        json.Should().Contain("conv_1");
        json.Should().Contain("\"direccion\":\"in\"");
        json.Should().Contain("\"coachingIdeas\"");
        json.Should().Contain("\"ideaActivaIndice\":1");
    }

    private static WebApplicationFactory<Program> Construir()
    {
        var respuestas = Substitute.For<IRepositorioRespuestas>();
        var respuesta = Respuesta.Crear(
            "resp_1", CampaniaId, "u_1", "p_1", "conv_1", "Mi idea", "whatsapp", false,
            EstadoRespuesta.Evaluada, Epoca, new[] { "t_oper" }, ideaIndice: 1, respuestaPadreId: "wamid.1",
            ideaRaizId: "resp_1", revisionIndice: 0);
        // I-17: una madura para verificar exposicion y filtro por nivelMadurez (04 §5.8).
        var respuestaMadura = Respuesta.Crear(
            "resp_2", CampaniaId, "u_2", "p_1", "conv_2", "Idea madura", "whatsapp", false,
            EstadoRespuesta.Evaluada, Epoca, null, nivelMadurez: NivelMadurez.Maduro);
        respuestas.ListarRespuestasAsync(CampaniaId, Arg.Any<CancellationToken>()).Returns(new[] { respuesta, respuestaMadura });
        respuestas.ObtenerRespuestaAsync(CampaniaId, "resp_1", Arg.Any<CancellationToken>()).Returns(respuesta);
        var evaluacionActual = CrearEvaluacion("eval_actual", "resp_1", Epoca.AddMinutes(20));
        var evaluacionSuperada = CrearEvaluacion("eval_superada", "resp_1", Epoca.AddMinutes(10));
        var evaluacionHuerfana = CrearEvaluacion("eval_huerfana", "resp_inexistente", Epoca.AddMinutes(30));
        var evaluacionSinRespuesta = CrearEvaluacion("eval_sin_respuesta", string.Empty, Epoca.AddMinutes(40));
        var evaluacionSinVersion = CrearEvaluacion("eval_sin_version", "resp_2", Epoca.AddMinutes(50), ideaId: "idea_2");
        respuestas.ListarEvaluacionesAsync(CampaniaId, Arg.Any<CancellationToken>()).Returns(new[]
        {
            evaluacionSuperada,
            evaluacionHuerfana,
            evaluacionSinRespuesta,
            evaluacionActual,
            evaluacionSinVersion,
        });
        respuestas.ObtenerEvaluacionPorRespuestaAsync(CampaniaId, "resp_1", Arg.Any<CancellationToken>()).Returns(evaluacionActual);
        var artefacto = CrearArtefacto(1);
        respuestas.ObtenerArtefactoAsync(CampaniaId, "md_1", Arg.Any<CancellationToken>()).Returns(artefacto);
        respuestas.ListarArtefactosAsync(CampaniaId, Arg.Any<CancellationToken>()).Returns(new[] { artefacto });

        // I-19 (04 §5.8): una idea madura y una rechazada para cubrir lista, filtros y detalle.
        var versionMadura = VersionIdeaConsolidada.Crear(
            "idea_1_v1", CampaniaId, "idea_1", 1, null, "Idea consolidada y confirmada.", new[] { "resp_1" },
            new[] { "resp_1" }, TipoAporteIdea.Inicial, EstadoConfirmacionVersionIdea.Confirmada, null, null, null,
            null, Epoca, Epoca);
        var versionRechazada = VersionIdeaConsolidada.Crear(
            "idea_2_v1", CampaniaId, "idea_2", 1, null, "Idea que el participante descarto.", new[] { "resp_2" },
            new[] { "resp_2" }, TipoAporteIdea.Inicial, EstadoConfirmacionVersionIdea.Propuesta, null, null, null,
            null, Epoca);
        var ideaMadura = IdeaConsolidada.Crear("idea_1", CampaniaId, "u_1", "p_1", "conv_1", "resp_1", 1, Epoca)
            .ConfirmarVersion(versionMadura.Id, Epoca)
            .Cerrar(EstadoResultadoIdeaConsolidada.Madura, "eval_1", "umbral", Epoca);
        var ideaRechazada = IdeaConsolidada.Crear("idea_2", CampaniaId, "u_2", "p_1", "conv_2", "resp_2", 2, Epoca)
            .ConPropuesta(versionRechazada.Id, Epoca)
            .Cerrar(EstadoResultadoIdeaConsolidada.Rechazada, null, "rechazoParticipante", Epoca);
        respuestas.ListarIdeasConsolidadasAsync(CampaniaId, Arg.Any<CancellationToken>())
            .Returns(new[] { ideaMadura, ideaRechazada });
        respuestas.ObtenerIdeaConsolidadaAsync(CampaniaId, "idea_1", Arg.Any<CancellationToken>()).Returns(ideaMadura);
        respuestas.ObtenerVersionIdeaAsync(CampaniaId, "idea_1_v1", Arg.Any<CancellationToken>()).Returns(versionMadura);
        respuestas.ObtenerVersionIdeaAsync(CampaniaId, "idea_2_v1", Arg.Any<CancellationToken>()).Returns(versionRechazada);
        // P-34 §6: el listado resuelve las versiones de la pagina en una sola consulta por ids.
        respuestas
            .ListarVersionesDeCampaniaAsync(CampaniaId, Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(llamada => new[] { versionMadura, versionRechazada }
                .Where(version => ((IReadOnlyCollection<string>)llamada[1]).Contains(version.Id))
                .ToArray());
        respuestas.ListarVersionesIdeaAsync(CampaniaId, "idea_1", Arg.Any<CancellationToken>())
            .Returns(new[] { versionMadura });
        // P-34 §6: el detalle pide los aportes por ideaId en vez de leer la particion completa.
        respuestas.ListarRespuestasPorIdeaAsync(CampaniaId, "idea_1", Arg.Any<CancellationToken>())
            .Returns(new[] { respuesta });
        var evaluacionIdea = CrearEvaluacion(
            fecha: Epoca.AddMinutes(60),
            repreguntaSugerida: "¿Qué impacto tendría esta idea en el equipo?");
        respuestas.ObtenerEvaluacionPorIdAsync(CampaniaId, "eval_1", Arg.Any<CancellationToken>()).Returns(evaluacionIdea);
        // P-34 §5: la calificacion vigente del listado se pide en bloque por ids.
        respuestas
            .ListarEvaluacionesPorIdsAsync(CampaniaId, Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(llamada => ((IReadOnlyCollection<string>)llamada[1]).Contains("eval_1")
                ? new[] { evaluacionIdea }
                : []);

        // P-34 §4.1: la identidad la resuelve el servidor. `u_2` no existe a proposito: es el caso de
        // participante no identificado que antes se colaba como un id tecnico en pantalla.
        var usuarios = Substitute.For<IRepositorioUsuarios>();
        var ana = Usuario.Crear(
            "u_1", 42, "Ana Perez", NumeroWhatsApp.FromNormalized("573001112233"), RolUsuario.Participante,
            EstadoRegistro.Activo, "Operaciones", "Flores El Aljibe", null, null, Epoca, Epoca, sede: "AL");
        usuarios
            .ListarUsuariosPorIdsAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(llamada => ((IReadOnlyCollection<string>)llamada[0]).Contains("u_1") ? new[] { ana } : []);
        usuarios.ObtenerUsuarioPorIdAsync("u_1", Arg.Any<CancellationToken>()).Returns(ana);

        var conversaciones = Substitute.For<IRepositorioConversaciones>();
        var cola = new PoliticaColaCoachingIdeas().Crear(
            "wamid.1",
            new[] { new RaizIdeaCoaching(1, "resp_1", null) },
            Epoca);
        conversaciones.ObtenerConversacionAsync(CampaniaId, "conv_1", Arg.Any<CancellationToken>())
            .Returns(Conversacion
                .Iniciar("conv_1", CampaniaId, "u_1", "p_1", "whatsapp", null, Epoca)
                .ConCoachingIdeas(cola));
        conversaciones.ListarMensajesAsync(CampaniaId, "conv_1", Arg.Any<CancellationToken>())
            .Returns(new[] { Mensaje.Crear("msg_1", CampaniaId, "conv_1", DireccionMensaje.In, "Mi idea", "wamid.1", Epoca) });

        var compilador = Substitute.For<ICompiladorMarkdown>();
        compilador.CompilarAsync(Arg.Any<SolicitudCompilacion>(), Arg.Any<CancellationToken>()).Returns(CrearArtefacto(2));

        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(respuestas);
                services.AddSingleton(usuarios);
                services.AddSingleton(conversaciones);
                services.AddSingleton(compilador);
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

    private static DominioEvaluacion CrearEvaluacion(
        string id = "eval_1",
        string respuestaId = "resp_1",
        DateTimeOffset? fecha = null,
        string? ideaId = null,
        string? versionIdeaId = null,
        string? repreguntaSugerida = null)
        => DominioEvaluacion.Crear(
            id, CampaniaId, respuestaId, "u_1", "p_1", "rub_1", 1, "pr_eval", 1, "llm_1",
            new ConfigLlmSnapshot("AzureOpenAI", "gpt-4o-mini", "https://x", new Dictionary<string, object?>()),
            new Dictionary<string, decimal> { ["claridad"] = 1m },
            new[] { CalificacionCriterio.Crear("claridad", 4m, "clara") },
            4m, "explica", "Buena idea",
            repreguntaSugerida is null ? RecomendacionEvaluacion.Cerrar : RecomendacionEvaluacion.Repreguntar,
            repreguntaSugerida,
            new[] { "tema" }, new[] { "ent" }, false, fecha ?? Epoca, ideaId: ideaId, versionIdeaId: versionIdeaId);

    private static ArtefactoMarkdown CrearArtefacto(int version)
        => ArtefactoMarkdown.Crear(
            "md_1", CampaniaId, TipoArtefactoMarkdown.Respuesta, "u_1", "p_1", "resp_1", "eval_1",
            "# Aporte de Ana\n\nContenido", "campanias/c_1/respuesta/resp_1.md", EstadoArtefacto.Generado, version, Epoca, Epoca);

    private sealed class SesionesFake : IServicioSesion
    {
        public Task<SesionEmitida> EmitirAsync(Usuario usuario, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<PrincipalSesion?> ValidarAsync(string token, CancellationToken cancellationToken)
            => Task.FromResult<PrincipalSesion?>(token == "token-admin"
                ? new PrincipalSesion("u_admin", "Admin", RolUsuario.Admin, CsrfAdmin, DateTimeOffset.UtcNow.AddMinutes(30))
                : token == "token-visor"
                    ? new PrincipalSesion("u_visor", "Visor", RolUsuario.Visor, CsrfAdmin, DateTimeOffset.UtcNow.AddMinutes(30))
                : null);
    }
}
