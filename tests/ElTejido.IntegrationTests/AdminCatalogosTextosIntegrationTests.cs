using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using ElTejido.Application.Auth;
using ElTejido.Application.Campanas;
using ElTejido.Application.Configuracion;
using ElTejido.Domain.Campanas;
using ElTejido.Domain.Common;
using ElTejido.Domain.Usuarios;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ElTejido.IntegrationTests;

public sealed class AdminCatalogosTextosIntegrationTests
{
    [Fact]
    public async Task Catalogo_AdminCreaActivaYConsultaVersionEfectiva()
    {
        using var fabrica = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("Persistencia:Modo", "Memoria");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IServicioSesion>();
                services.AddSingleton<IServicioSesion, SesionesFake>();
            });
        });
        using var client = fabrica.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", "eltejido_sesion=token-admin");
        var contenido = ContenidoValido();

        using var crear = new HttpRequestMessage(HttpMethod.Post, "/api/admin/catalogos-textos")
        {
            Content = JsonContent.Create(new
            {
                familiaId = "conversacion-global",
                idioma = "es",
                contenido.Mensajes,
                contenido.Frases,
            }),
        };
        crear.Headers.Add("X-CSRF-Token", "csrf-admin");
        using var creado = await client.SendAsync(crear);

        creado.StatusCode.Should().Be(HttpStatusCode.Created);
        creado.Headers.ETag.Should().NotBeNull();

        using var activar = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/admin/catalogos-textos/conversacion-global/es/versiones/1/activar");
        activar.Headers.Add("X-CSRF-Token", "csrf-admin");
        activar.Headers.TryAddWithoutValidation("If-Match", creado.Headers.ETag!.Tag);
        using var activado = await client.SendAsync(activar);
        activado.StatusCode.Should().Be(HttpStatusCode.OK);

        using var efectivo = await client.GetAsync("/api/admin/catalogos-textos/efectivo?idioma=es");
        efectivo.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await efectivo.Content.ReadAsStringAsync();
        json.Should().Contain("\"origen\":\"catalogo\"");
        json.Should().Contain("\"estado\":\"activo\"");
        json.Should().Contain("\"idioma\":\"es\"");
    }

    [Fact]
    public async Task Semilla_AdminLaCreaComoBorradorSinActivarla()
    {
        using var fabrica = ConstruirFabrica();
        using var client = fabrica.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", "eltejido_sesion=token-admin");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/catalogos-textos/semillas/en");
        request.Headers.Add("X-CSRF-Token", "csrf-admin");

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("\"idioma\":\"en\"");
        json.Should().Contain("\"estado\":\"borrador\"");
        using var efectivo = await client.GetAsync("/api/admin/catalogos-textos/efectivo?idioma=en");
        (await efectivo.Content.ReadAsStringAsync()).Should().Contain("\"origen\":\"emergencia\"");
    }

    // --- DT-P32-02 corte 1/3: semilla base vs. fotografia legacy ---

    [Fact]
    public async Task SemillaBase_CreaBorradorAunqueElLegacySupereElLimite()
    {
        using var fabrica = ConstruirFabrica(maxFrasesPorGrupo: 30, conLegacyExcedido: true);
        using var client = ClienteAdmin(fabrica);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/catalogos-textos/semillas/es/base");
        request.Headers.Add("X-CSRF-Token", "csrf-admin");
        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("\"idioma\":\"es\"").And.Contain("\"estado\":\"borrador\"");
        json.Should().NotContain("frase legacy");
        (await ListarVersionesAsync(client)).Should().HaveCount(1);
    }

    [Fact]
    public async Task PrevalidarLegacy_ReportaElGrupoExcedidoYNoPersisteNada()
    {
        using var fabrica = ConstruirFabrica(maxFrasesPorGrupo: 30, conLegacyExcedido: true);
        using var client = ClienteAdmin(fabrica);

        using var response = await client.GetAsync(
            "/api/admin/catalogos-textos/semillas/es/legacy/preview");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("\"valido\":false");
        json.Should().Contain("\"field\":\"frases.despertarProactivo\"");
        json.Should().Contain("\"issue\":\"debe_tener_entre_1_y_30_elementos\"");
        json.Should().Contain("\"gruposFrases\":16");
        json.Should().NotContain("frase legacy");
        (await ListarVersionesAsync(client)).Should().BeEmpty();
    }

    [Fact]
    public async Task ExportarLegacy_ConservaTodasLasEntradasAunqueSeaInvalido()
    {
        using var fabrica = ConstruirFabrica(maxFrasesPorGrupo: 30, conLegacyExcedido: true);
        using var client = ClienteAdmin(fabrica);

        using var response = await client.GetAsync(
            "/api/admin/catalogos-textos/semillas/es/legacy/exportar");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentDisposition!.FileNameStar
            .Should().Be("catalogo-catalogo_conversacion-es-legacy-editable.json");
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("\"formato\": \"catalogo-textos/v1\"");
        // Ninguna entrada se recorta: estan las 31, incluida la que rompe el limite.
        json.Should().Contain("frase legacy 0").And.Contain("frase legacy 30");
        (await ListarVersionesAsync(client)).Should().BeEmpty();
    }

    [Fact]
    public async Task ImportarLegacy_PorEncimaDelLimite_DevuelveValidacionYNoCreaVersion()
    {
        using var fabrica = ConstruirFabrica(maxFrasesPorGrupo: 30, conLegacyExcedido: true);
        using var client = ClienteAdmin(fabrica);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/catalogos-textos/semillas/es/legacy");
        request.Headers.Add("X-CSRF-Token", "csrf-admin");
        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("VALIDATION_ERROR").And.Contain("frases.despertarProactivo");
        (await ListarVersionesAsync(client)).Should().BeEmpty();
    }

    [Fact]
    public async Task ImportarLegacy_ConLimiteOperativoAmpliado_CreaBorradorSinRecompilar()
    {
        using var fabrica = ConstruirFabrica(maxFrasesPorGrupo: 100, conLegacyExcedido: true);
        using var client = ClienteAdmin(fabrica);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/catalogos-textos/semillas/es/legacy");
        request.Headers.Add("X-CSRF-Token", "csrf-admin");
        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await response.Content.ReadAsStringAsync()).Should().Contain("\"estado\":\"borrador\"");
        using var efectivo = await client.GetAsync("/api/admin/catalogos-textos/efectivo?idioma=es");
        // Sigue sin activarse: el efectivo cae al respaldo compilado.
        (await efectivo.Content.ReadAsStringAsync()).Should().Contain("\"origen\":\"emergencia\"");
    }

    [Fact]
    public async Task SemillasNuevas_VisorPrevalidaPeroNoPuedeCrear()
    {
        using var fabrica = ConstruirFabrica(maxFrasesPorGrupo: 30, conLegacyExcedido: true);
        using var client = fabrica.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", "eltejido_sesion=token-visor");

        using var preview = await client.GetAsync("/api/admin/catalogos-textos/semillas/es/legacy/preview");
        using var crear = new HttpRequestMessage(HttpMethod.Post, "/api/admin/catalogos-textos/semillas/es/base");
        crear.Headers.Add("X-CSRF-Token", "csrf-visor");
        using var creado = await client.SendAsync(crear);

        preview.StatusCode.Should().Be(HttpStatusCode.OK);
        creado.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SemillaBase_IdiomaInvalido_DevuelveValidacion()
    {
        using var fabrica = ConstruirFabrica();
        using var client = ClienteAdmin(fabrica);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/catalogos-textos/semillas/fr/base");
        request.Headers.Add("X-CSRF-Token", "csrf-admin");
        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("\"field\":\"idioma\"");
    }

    // --- DT-P32-02 corte 2/3: edicion masiva JSON, readiness y campanas ---

    [Fact]
    public async Task EdicionMasiva_DescargarEditarPrevalidarEImportar_CreaVersionSiguienteEnBorrador()
    {
        using var fabrica = ConstruirFabrica();
        using var client = ClienteAdmin(fabrica);
        await CrearSemillaBaseAsync(client, "es");

        using var descarga = await client.GetAsync(
            "/api/admin/catalogos-textos/catalogo_conversacion/es/versiones/1/exportar");
        descarga.StatusCode.Should().Be(HttpStatusCode.OK);
        descarga.Content.Headers.ContentDisposition!.FileNameStar
            .Should().Be("catalogo-catalogo_conversacion-es-v1-editable.json");
        var archivo = JsonNode.Parse(await descarga.Content.ReadAsStringAsync())!.AsObject();
        archivo["formato"]!.GetValue<string>().Should().Be("catalogo-textos/v1");
        archivo["mensajes"]!["acuseContinuar"] = "Perfecto, sigamos con tu idea.";
        // Metadatos informativos: el servidor debe ignorarlos, no obedecerlos.
        archivo["metadatos"]!["version"] = 99;
        archivo["metadatos"]!["estado"] = "activo";

        using var prevalidacion = await EnviarJsonAsync(
            client, HttpMethod.Post, "/api/admin/catalogos-textos/importar/prevalidar?idioma=es", archivo);
        var cuerpoPrevalidacion = await prevalidacion.Content.ReadAsStringAsync();
        using var importacion = await EnviarJsonAsync(
            client, HttpMethod.Post, "/api/admin/catalogos-textos/importar?idioma=es", archivo);

        prevalidacion.StatusCode.Should().Be(HttpStatusCode.OK);
        cuerpoPrevalidacion.Should().Contain("\"valido\":true").And.Contain("\"errores\":[]");
        cuerpoPrevalidacion.Should().Contain("\"mensajes\":29").And.Contain("\"gruposFrases\":16");
        importacion.StatusCode.Should().Be(HttpStatusCode.Created);
        var creado = await importacion.Content.ReadAsStringAsync();
        creado.Should().Contain("\"version\":2").And.Contain("\"estado\":\"borrador\"");
        creado.Should().Contain("Perfecto, sigamos con tu idea.");
        (await ListarVersionesAsync(client)).Should().HaveCount(2);
        using var efectivo = await client.GetAsync("/api/admin/catalogos-textos/efectivo?idioma=es");
        // Importar nunca activa: sin activacion explicita el efectivo sigue en el respaldo compilado.
        (await efectivo.Content.ReadAsStringAsync()).Should().Contain("\"origen\":\"emergencia\"");
    }

    [Fact]
    public async Task Prevalidar_ContenidoInvalido_DevuelveTodosLosErroresConDoscientosYSinEscribir()
    {
        using var fabrica = ConstruirFabrica();
        using var client = ClienteAdmin(fabrica);
        var archivo = await ArchivoBaseAsync(client, "es");
        archivo["formato"] = "catalogo-textos/v9";
        archivo["mensajes"]!["acuseContinuar"] = "";
        archivo["mensajes"]!["saludoReactivacion"] = "Hola {{secreto}}";
        archivo["mensajes"]!["claveInventada"] = "texto";
        archivo["frases"]!["continuar"] = new JsonArray("listo", "  LISTO ");

        using var respuesta = await EnviarJsonAsync(
            client, HttpMethod.Post, "/api/admin/catalogos-textos/importar/prevalidar?idioma=es", archivo);

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await respuesta.Content.ReadAsStringAsync();
        json.Should().Contain("\"valido\":false");
        json.Should().Contain("\"field\":\"formato\"").And.Contain("\"issue\":\"no_soportado\"");
        json.Should().Contain("\"field\":\"mensajes.acuseContinuar\"").And.Contain("\"issue\":\"vacio\"");
        json.Should().Contain("placeholder_no_permitido:secreto");
        json.Should().Contain("\"field\":\"mensajes.claveInventada\"").And.Contain("clave_desconocida");
        json.Should().Contain("frase_duplicada");
        (await ListarVersionesAsync(client)).Should().HaveCount(1);
    }

    [Fact]
    public async Task Importar_ContenidoInvalido_DevuelveCuatrocientosYNoCreaVersion()
    {
        using var fabrica = ConstruirFabrica();
        using var client = ClienteAdmin(fabrica);
        var archivo = await ArchivoBaseAsync(client, "es");
        archivo["mensajes"]!["acuseContinuar"] = "";

        using var respuesta = await EnviarJsonAsync(
            client, HttpMethod.Post, "/api/admin/catalogos-textos/importar", archivo);

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await respuesta.Content.ReadAsStringAsync()).Should().Contain("VALIDATION_ERROR");
        (await ListarVersionesAsync(client)).Should().HaveCount(1);
    }

    [Fact]
    public async Task Importar_IdiomaDistintoAlSeleccionado_SeRechaza()
    {
        using var fabrica = ConstruirFabrica();
        using var client = ClienteAdmin(fabrica);
        var archivo = await ArchivoBaseAsync(client, "en");

        using var prevalidacion = await EnviarJsonAsync(
            client, HttpMethod.Post, "/api/admin/catalogos-textos/importar/prevalidar?idioma=es", archivo);
        using var importacion = await EnviarJsonAsync(
            client, HttpMethod.Post, "/api/admin/catalogos-textos/importar?idioma=es", archivo);

        (await prevalidacion.Content.ReadAsStringAsync()).Should().Contain("no_coincide_con_seleccion");
        importacion.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Importar_JsonMalformadoOSobreElLimite_DevuelveCuatrocientosAntesDeValidar()
    {
        using var fabrica = ConstruirFabrica(maxBytesImportacionJson: 2048);
        using var client = ClienteAdmin(fabrica);

        using var malformado = new HttpRequestMessage(HttpMethod.Post, "/api/admin/catalogos-textos/importar/prevalidar")
        {
            Content = new StringContent("{\"mensajes\": ", Encoding.UTF8, "application/json"),
        };
        malformado.Headers.Add("X-CSRF-Token", "csrf-admin");
        using var respuestaMalformada = await client.SendAsync(malformado);

        using var enorme = new HttpRequestMessage(HttpMethod.Post, "/api/admin/catalogos-textos/importar")
        {
            Content = new StringContent(
                "{\"relleno\":\"" + new string('x', 4096) + "\"}",
                Encoding.UTF8,
                "application/json"),
        };
        enorme.Headers.Add("X-CSRF-Token", "csrf-admin");
        using var respuestaEnorme = await client.SendAsync(enorme);

        respuestaMalformada.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await respuestaMalformada.Content.ReadAsStringAsync()).Should().Contain("json_invalido");
        respuestaEnorme.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await respuestaEnorme.Content.ReadAsStringAsync()).Should().Contain("excede_2048_bytes");
    }

    [Fact]
    public async Task Importar_ContentTypeNoJson_SeRechaza()
    {
        using var fabrica = ConstruirFabrica();
        using var client = ClienteAdmin(fabrica);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/catalogos-textos/importar")
        {
            Content = new StringContent("mensajes=1", Encoding.UTF8, "text/plain"),
        };
        request.Headers.Add("X-CSRF-Token", "csrf-admin");
        using var respuesta = await client.SendAsync(request);

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await respuesta.Content.ReadAsStringAsync()).Should().Contain("debe_ser_application_json");
    }

    [Fact]
    public async Task Readiness_DistingueGateBorradorYActivo()
    {
        using var fabrica = ConstruirFabrica();
        using var client = ClienteAdmin(fabrica);

        using var inicial = await client.GetAsync("/api/admin/catalogos-textos/readiness");
        var antes = await inicial.Content.ReadAsStringAsync();

        var creado = await CrearSemillaBaseAsync(client, "es");
        using var conBorrador = await client.GetAsync("/api/admin/catalogos-textos/readiness?idioma=es");
        var borrador = await conBorrador.Content.ReadAsStringAsync();

        using var activar = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/admin/catalogos-textos/catalogo_conversacion/es/versiones/1/activar");
        activar.Headers.Add("X-CSRF-Token", "csrf-admin");
        activar.Headers.TryAddWithoutValidation("If-Match", creado.Etag);
        (await client.SendAsync(activar)).StatusCode.Should().Be(HttpStatusCode.OK);
        using var conActivo = await client.GetAsync("/api/admin/catalogos-textos/readiness?idioma=es");
        var activo = await conActivo.Content.ReadAsStringAsync();

        // El gate real del proceso sigue apagado aunque el preview `/efectivo` devuelva contenido.
        antes.Should().Contain("\"gateHabilitado\":false").And.Contain("\"listo\":false");
        antes.Should().Contain("\"maxFrasesPorGrupo\":100");
        borrador.Should().Contain("\"tieneBorrador\":true").And.Contain("\"tieneActivo\":false");
        activo.Should().Contain("\"tieneActivo\":true").And.Contain("\"activaValida\":true");
        activo.Should().Contain("\"versionActiva\":1").And.Contain("\"listo\":true");
        activo.Should().Contain("\"gateHabilitado\":false");
    }

    /// <summary>
    /// DT-P32-03 §3.2: readiness enumera los pares `plantillaRef + idioma` que exigirian las
    /// campanias con el gate encendido. Falta selectiva de `en`: el catalogo puede estar listo y el
    /// lote inicial fallaria igual, asi que `listoParaGateOn` debe quedar en `false`.
    /// </summary>
    [Fact]
    public async Task Readiness_ConMapeoMetaSoloEnEspanol_ReportaElParInglesFaltante()
    {
        using var fabrica = ConstruirFabrica(mapeos: MapeosMeta(("es", "es_CO"), ("en", null)));
        using var client = ClienteAdmin(fabrica);
        await SembrarCampaniaBilingueAsync(fabrica, "inicio_campania");
        await ActivarSemillaBaseAsync(client, "es");
        await ActivarSemillaBaseAsync(client, "en");

        using var respuesta = await client.GetAsync("/api/admin/catalogos-textos/readiness");
        var cuerpo = JsonNode.Parse(await respuesta.Content.ReadAsStringAsync())!.AsObject();

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        // El significado editorial de `listo` no cambia: los dos catalogos siguen activos y validos.
        cuerpo["listo"]!.GetValue<bool>().Should().BeTrue();
        cuerpo["listoParaGateOn"]!.GetValue<bool>().Should().BeFalse();

        var mapeos = cuerpo["mapeosMeta"]!.AsArray();
        mapeos.Should().HaveCount(2);
        var espanol = mapeos.Single(x => x!["idioma"]!.GetValue<string>() == "es")!.AsObject();
        espanol["plantillaRef"]!.GetValue<string>().Should().Be("inicio_campania");
        espanol["configurado"]!.GetValue<bool>().Should().BeTrue();
        espanol["problemas"]!.AsArray().Should().BeEmpty();

        var ingles = mapeos.Single(x => x!["idioma"]!.GetValue<string>() == "en")!.AsObject();
        ingles["configurado"]!.GetValue<bool>().Should().BeFalse();
        ingles["nombreConfigurado"]!.GetValue<bool>().Should().BeFalse();
        ingles["problemas"]!.AsArray().Select(x => x!.GetValue<string>())
            .Should().BeEquivalentTo("nombre_faltante", "idioma_meta_faltante");
        var requirente = ingles["campanias"]!.AsArray().Should().ContainSingle().Subject!.AsObject();
        requirente["campaniaId"]!.GetValue<string>().Should().Be("c_bilingue");
        requirente["estado"]!.GetValue<string>().Should().Be("activa");
        requirente["mensajeInicialId"]!.GetValue<string>().Should().Be("mi_1");
        // Ni secretos ni contenido del participante viajan en la respuesta.
        cuerpo.ToJsonString().Should().NotContain("token").And.NotContain("Hola ");
    }

    [Fact]
    public async Task Readiness_ConMapaCompleto_QuedaListoParaGateOn()
    {
        using var fabrica = ConstruirFabrica(mapeos: MapeosMeta(("es", "es_CO"), ("en", "en_US")));
        using var client = ClienteAdmin(fabrica);
        await SembrarCampaniaBilingueAsync(fabrica, "inicio_campania");
        await ActivarSemillaBaseAsync(client, "es");
        await ActivarSemillaBaseAsync(client, "en");

        using var respuesta = await client.GetAsync("/api/admin/catalogos-textos/readiness");
        var cuerpo = JsonNode.Parse(await respuesta.Content.ReadAsStringAsync())!.AsObject();

        cuerpo["listoParaGateOn"]!.GetValue<bool>().Should().BeTrue();
        var mapeos = cuerpo["mapeosMeta"]!.AsArray();
        mapeos.Should().HaveCount(2);
        mapeos.Should().OnlyContain(x => x!["configurado"]!.GetValue<bool>());
        mapeos.Single(x => x!["idioma"]!.GetValue<string>() == "en")!["componentes"]!
            .AsArray().Select(x => x!.GetValue<string>()).Should().Equal("nombre");
    }

    /// <summary>
    /// DT-P32-03-01 §7.2 y §7.4: un borrador a medio construir se sigue enumerando con sus problemas,
    /// pero no puede mantener apagada la senal de las campanias que ya operan. `borrador` no tiene
    /// transicion a `archivada`, asi que ese bloqueo seria permanente.
    /// </summary>
    [Fact]
    public async Task Readiness_ConBorradorIncompleto_LoMuestraSinBloquearElGate()
    {
        using var fabrica = ConstruirFabrica(mapeos: MapeosMeta(("es", "es_CO"), ("en", "en_US")));
        using var client = ClienteAdmin(fabrica);
        await SembrarCampaniaBilingueAsync(fabrica, "inicio_campania");
        await SembrarCampaniaBilingueAsync(
            fabrica, "inicio_sin_mapeo", id: "c_borrador", estado: EstadoCampania.Borrador);
        await ActivarSemillaBaseAsync(client, "es");
        await ActivarSemillaBaseAsync(client, "en");

        using var respuesta = await client.GetAsync("/api/admin/catalogos-textos/readiness");
        var cuerpo = JsonNode.Parse(await respuesta.Content.ReadAsStringAsync())!.AsObject();

        cuerpo["listoParaGateOn"]!.GetValue<bool>().Should().BeTrue();
        var mapeos = cuerpo["mapeosMeta"]!.AsArray();
        mapeos.Should().HaveCount(4);
        var pendientes = mapeos
            .Where(x => x!["plantillaRef"]!.GetValue<string>() == "inicio_sin_mapeo")
            .Select(x => x!.AsObject())
            .ToArray();
        pendientes.Should().HaveCount(2);
        pendientes.Should().OnlyContain(x => !x["bloqueaGateOn"]!.GetValue<bool>());
        // El diagnostico no desaparece: el administrador debe verlo antes de activar el borrador.
        pendientes.Should().OnlyContain(x => x["problemas"]!.AsArray().Count > 0);
        mapeos.Where(x => x!["plantillaRef"]!.GetValue<string>() == "inicio_campania")
            .Should().OnlyContain(x => x!["bloqueaGateOn"]!.GetValue<bool>());
    }

    /// <summary>
    /// DT-P32-03-01 §7.5: con el gate ON el envio inicial resuelve por `plantillaRef + idioma`; una
    /// campania sin ese mapeo no puede activarse. La guarda es local: no consulta Meta.
    /// </summary>
    [Fact]
    public async Task ActivarCampania_ConGateOnYSinMapeoPropio_Responde400YConservaElBorrador()
    {
        using var fabrica = ConstruirFabrica(mapeos: MapeosMeta(("es", "es_CO")), gateActivo: true);
        using var client = ClienteAdmin(fabrica);
        await SembrarCampaniaEspanolaAsync(fabrica, "c_sin_mapeo", "otro_alias");

        using var respuesta = await CambiarEstadoAsync(client, "c_sin_mapeo", "activa");
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<CuerpoErrorTest>();

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        cuerpo!.Error.Code.Should().Be("VALIDATION_ERROR");
        cuerpo.Error.Details!.Select(x => x.Field).Should().AllBe("mapeosMeta.mi_1.es");
        cuerpo.Error.Details!.Select(x => x.Issue).Should().BeEquivalentTo(
            "nombre_faltante", "idioma_meta_faltante");
        var campania = await ObtenerCampaniaAsync(fabrica, "c_sin_mapeo");
        campania.Estado.Should().Be(EstadoCampania.Borrador);
    }

    /// <summary>
    /// DT-P32-03-01 §7.6: los mapeos propios completos alcanzan; otro borrador incompleto no bloquea.
    /// </summary>
    [Fact]
    public async Task ActivarCampania_ConGateOnYMapeoPropio_ActivaAunqueHayaOtroBorradorIncompleto()
    {
        using var fabrica = ConstruirFabrica(mapeos: MapeosMeta(("es", "es_CO")), gateActivo: true);
        using var client = ClienteAdmin(fabrica);
        await SembrarCampaniaEspanolaAsync(fabrica, "c_lista", "inicio_campania");
        await SembrarCampaniaEspanolaAsync(fabrica, "c_incompleta", "otro_alias");

        using var respuesta = await CambiarEstadoAsync(client, "c_lista", "activa");

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ObtenerCampaniaAsync(fabrica, "c_lista")).Estado.Should().Be(EstadoCampania.Activa);
        (await ObtenerCampaniaAsync(fabrica, "c_incompleta")).Estado.Should().Be(EstadoCampania.Borrador);
    }

    [Fact]
    public async Task ActivarCampania_ConGateOffYSinMapeo_ConservaLaConductaPrevia()
    {
        using var fabrica = ConstruirFabrica();
        using var client = ClienteAdmin(fabrica);
        await SembrarCampaniaEspanolaAsync(fabrica, "c_legacy", "otro_alias");

        using var respuesta = await CambiarEstadoAsync(client, "c_legacy", "activa");

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ObtenerCampaniaAsync(fabrica, "c_legacy")).Estado.Should().Be(EstadoCampania.Activa);
    }

    [Fact]
    public async Task EdicionMasiva_VisorPrevalidaYConsultaReadinessPeroNoImporta()
    {
        using var fabrica = ConstruirFabrica();
        using var admin = ClienteAdmin(fabrica);
        var archivo = await ArchivoBaseAsync(admin, "es");
        using var visor = fabrica.CreateClient();
        visor.DefaultRequestHeaders.Add("Cookie", "eltejido_sesion=token-visor");

        using var readiness = await visor.GetAsync("/api/admin/catalogos-textos/readiness");
        using var prevalidacion = await EnviarJsonAsync(
            visor, HttpMethod.Post, "/api/admin/catalogos-textos/importar/prevalidar", archivo, "csrf-visor");
        using var importacion = await EnviarJsonAsync(
            visor, HttpMethod.Post, "/api/admin/catalogos-textos/importar", archivo, "csrf-visor");

        readiness.StatusCode.Should().Be(HttpStatusCode.OK);
        // La prevalidacion es POST porque necesita cuerpo, pero no escribe: el visor puede revisar.
        prevalidacion.StatusCode.Should().Be(HttpStatusCode.OK);
        (await prevalidacion.Content.ReadAsStringAsync()).Should().Contain("\"valido\":true");
        importacion.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ListarVersionesAsync(admin)).Should().HaveCount(1);
    }

    [Fact]
    public async Task Prevalidar_SinTokenCsrf_SeRechazaAunqueNoEscriba()
    {
        using var fabrica = ConstruirFabrica();
        using var client = ClienteAdmin(fabrica);
        var archivo = await ArchivoBaseAsync(client, "es");

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/admin/catalogos-textos/importar/prevalidar")
        {
            Content = new StringContent(archivo.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        using var respuesta = await client.SendAsync(request);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static async Task<HttpResponseMessage> EnviarJsonAsync(
        HttpClient client,
        HttpMethod metodo,
        string ruta,
        JsonNode cuerpo,
        string csrf = "csrf-admin")
    {
        using var request = new HttpRequestMessage(metodo, ruta)
        {
            Content = new StringContent(cuerpo.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("X-CSRF-Token", csrf);
        return await client.SendAsync(request);
    }

    private static async Task<(int Version, string Etag)> CrearSemillaBaseAsync(HttpClient client, string idioma)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/admin/catalogos-textos/semillas/{idioma}/base");
        request.Headers.Add("X-CSRF-Token", "csrf-admin");
        using var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        return (json["version"]!.GetValue<int>(), response.Headers.ETag!.Tag);
    }

    private static async Task ActivarSemillaBaseAsync(HttpClient client, string idioma)
    {
        var creado = await CrearSemillaBaseAsync(client, idioma);
        using var activar = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/admin/catalogos-textos/catalogo_conversacion/{idioma}/versiones/{creado.Version}/activar");
        activar.Headers.Add("X-CSRF-Token", "csrf-admin");
        activar.Headers.TryAddWithoutValidation("If-Match", creado.Etag);
        using var respuesta = await client.SendAsync(activar);
        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>Ajustes `WhatsApp:PlantillaEnvioInicial:Mapeos` por idioma; `null` deja el par sin configurar.</summary>
    private static Dictionary<string, string> MapeosMeta(
        params (string Idioma, string? IdiomaMeta)[] idiomas)
    {
        var ajustes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (idioma, idiomaMeta) in idiomas.Where(x => x.IdiomaMeta is not null))
        {
            var prefijo = $"WhatsApp:PlantillaEnvioInicial:Mapeos:inicio_campania:{idioma}";
            ajustes[$"{prefijo}:Nombre"] = $"el_tejido_inicio_{idioma}";
            ajustes[$"{prefijo}:Idioma"] = idiomaMeta!;
            ajustes[$"{prefijo}:Componentes:0"] = "nombre";
        }

        return ajustes;
    }

    /// <summary>
    /// Campania activa `es`/`en` cuyo unico mensaje inicial activo exige el alias en ambos idiomas.
    /// El repositorio en memoria es singleton, asi que sembrarlo aqui alimenta el readiness real.
    /// </summary>
    private static async Task<HttpResponseMessage> CambiarEstadoAsync(
        HttpClient client,
        string campaniaId,
        string estado)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/admin/campanias/{campaniaId}/estado")
        {
            Content = JsonContent.Create(new { estado }),
        };
        request.Headers.Add("X-CSRF-Token", "csrf-admin");
        return await client.SendAsync(request);
    }

    private static async Task<Campania> ObtenerCampaniaAsync(WebApplicationFactory<Program> fabrica, string id)
    {
        var campania = await fabrica.Services
            .GetRequiredService<IRepositorioCampanias>()
            .ObtenerCampaniaPorIdAsync(id, CancellationToken.None);
        return campania.Should().NotBeNull().And.Subject.As<Campania>();
    }

    /// <summary>Campania espanola en borrador cuya localizacion `es` declara el alias indicado.</summary>
    private static async Task SembrarCampaniaEspanolaAsync(
        WebApplicationFactory<Program> fabrica,
        string id,
        string plantillaRef)
    {
        var mensaje = MensajeInicial.Crear(
            "mi_1",
            "saludo",
            "Hola {{nombre}}.",
            1,
            ["nombre"],
            EstadoRegistro.Activo,
            PlantillaWhatsApp.Crear("legacy_saludo", "es", ["nombre"]));
        var campania = Campania.Crear(
            id,
            "Campania",
            "Descripcion",
            "Objetivo",
            EstadoCampania.Borrador,
            [mensaje],
            null,
            "rub_1",
            null,
            "llm_1",
            ConfigMarkdown.Crear(TipoArtefactoMarkdown.Respuesta),
            ConfigConversacional.Crear(1, "Gracias."),
            LimitesSeguridad.Crear(1500, 10, 2),
            null,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch,
            idiomasHabilitados: ["es"],
            localizaciones: new Dictionary<string, LocalizacionCampania>(StringComparer.Ordinal)
            {
                ["es"] = LocalizacionCampania.Crear(
                    "es",
                    "Campania",
                    "Descripcion",
                    "Objetivo",
                    "Gracias.",
                    new Dictionary<string, LocalizacionMensajeInicial>(StringComparer.Ordinal)
                    {
                        ["mi_1"] = new("Hola {{nombre}}.", plantillaRef),
                    },
                    null),
            });

        await fabrica.Services
            .GetRequiredService<IRepositorioCampanias>()
            .GuardarCampaniaAsync(campania, CancellationToken.None);
    }

    private static async Task SembrarCampaniaBilingueAsync(
        WebApplicationFactory<Program> fabrica,
        string plantillaRef,
        string id = "c_bilingue",
        EstadoCampania estado = EstadoCampania.Activa)
    {
        var mensaje = MensajeInicial.Crear(
            "mi_1",
            "saludo",
            "Hola {{nombre}}.",
            1,
            ["nombre"],
            EstadoRegistro.Activo,
            PlantillaWhatsApp.Crear("legacy_saludo", "es", ["nombre"]));
        var localizaciones = new[] { "es", "en" }.ToDictionary(
            idioma => idioma,
            idioma => LocalizacionCampania.Crear(
                idioma,
                "Campania",
                "Descripcion",
                "Objetivo",
                "Gracias.",
                new Dictionary<string, LocalizacionMensajeInicial>(StringComparer.Ordinal)
                {
                    ["mi_1"] = new("Hola {{nombre}}.", plantillaRef),
                },
                null),
            StringComparer.Ordinal);

        var campania = Campania.Crear(
            id,
            "Campania",
            "Descripcion",
            "Objetivo",
            estado,
            [mensaje],
            null,
            "rub_1",
            null,
            "llm_1",
            ConfigMarkdown.Crear(TipoArtefactoMarkdown.Respuesta),
            ConfigConversacional.Crear(1, "Gracias."),
            LimitesSeguridad.Crear(1500, 10, 2),
            null,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch,
            idiomasHabilitados: ["es", "en"],
            localizaciones: localizaciones);

        await fabrica.Services
            .GetRequiredService<IRepositorioCampanias>()
            .GuardarCampaniaAsync(campania, CancellationToken.None);
    }

    private static async Task<JsonObject> ArchivoBaseAsync(HttpClient client, string idioma)
    {
        await CrearSemillaBaseAsync(client, idioma);
        using var descarga = await client.GetAsync(
            $"/api/admin/catalogos-textos/catalogo_conversacion/{idioma}/versiones/1/exportar");
        return JsonNode.Parse(await descarga.Content.ReadAsStringAsync())!.AsObject();
    }

    private static HttpClient ClienteAdmin(WebApplicationFactory<Program> fabrica)
    {
        var client = fabrica.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", "eltejido_sesion=token-admin");
        return client;
    }

    private static async Task<IReadOnlyCollection<object>> ListarVersionesAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/admin/catalogos-textos?idioma=es");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<IReadOnlyCollection<object>>()
            ?? Array.Empty<object>();
    }

    private static WebApplicationFactory<Program> ConstruirFabrica(
        int? maxFrasesPorGrupo = null,
        bool conLegacyExcedido = false,
        int? maxBytesImportacionJson = null,
        IReadOnlyDictionary<string, string>? mapeos = null,
        bool gateActivo = false)
        => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("Persistencia:Modo", "Memoria");
            if (gateActivo)
            {
                // Solo para probar la guarda de activacion: el gate real nace y sigue apagado.
                builder.UseSetting("Conversacion:CatalogoTextosHabilitado", "true");
            }

            foreach (var ajuste in mapeos ?? new Dictionary<string, string>(StringComparer.Ordinal))
            {
                builder.UseSetting(ajuste.Key, ajuste.Value);
            }

            if (maxFrasesPorGrupo is not null)
            {
                builder.UseSetting(
                    "Conversacion:CatalogoTextos:MaxFrasesPorGrupo",
                    maxFrasesPorGrupo.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }

            if (maxBytesImportacionJson is not null)
            {
                builder.UseSetting(
                    "Conversacion:CatalogoTextos:MaxBytesImportacionJson",
                    maxBytesImportacionJson.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }

            if (conLegacyExcedido)
            {
                // Reproduce la corrida del 2026-08-13: 31 frases heredadas en un solo grupo.
                for (var indice = 0; indice < 31; indice++)
                {
                    builder.UseSetting(
                        $"Conversacion:FrasesDespertarProactivo:{indice}",
                        $"frase legacy {indice}");
                }
            }

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IServicioSesion>();
                services.AddSingleton<IServicioSesion, SesionesFake>();
            });
        });

    private static SolicitudContenidoCatalogoTextos ContenidoValido()
    {
        var mensajes = ValidadorCatalogoTextosConversacion.ClavesMensajes
            .ToDictionary(x => x, x => $"{x} {{{{nombre}}}}", StringComparer.Ordinal);
        var frases = ValidadorCatalogoTextosConversacion.ClavesFrases
            .ToDictionary(
                x => x,
                x => (IReadOnlyCollection<string>)new[] { $"{x} opcion" },
                StringComparer.Ordinal);
        return new SolicitudContenidoCatalogoTextos(mensajes, frases);
    }

    private sealed class SesionesFake : IServicioSesion
    {
        public Task<SesionEmitida> EmitirAsync(ElTejido.Domain.Usuarios.Usuario usuario, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<PrincipalSesion?> ValidarAsync(string token, CancellationToken cancellationToken)
            => Task.FromResult<PrincipalSesion?>(token switch
            {
                "token-admin" => new PrincipalSesion(
                    "u_admin",
                    "Admin",
                    RolUsuario.Admin,
                    "csrf-admin",
                    DateTimeOffset.UtcNow.AddMinutes(30)),
                "token-visor" => new PrincipalSesion(
                    "u_visor",
                    "Visor",
                    RolUsuario.Visor,
                    "csrf-visor",
                    DateTimeOffset.UtcNow.AddMinutes(30)),
                _ => null,
            });
    }
}
