using ElTejido.Application.Common;
using ElTejido.Application.Conversacion;
using ElTejido.Application.Identidad;
using ElTejido.Application.Respuestas;
using ElTejido.Application.Seguridad;
using ElTejido.Application.WhatsApp;
using ElTejido.Domain.Campanas;
using ElTejido.Domain.Common;
using ElTejido.Domain.Conversaciones;
using ElTejido.Domain.Participantes;
using ElTejido.Domain.Respuestas;
using ElTejido.Domain.Seguridad;
using ElTejido.Domain.Usuarios;
using ElTejido.UnitTests.Soporte;
using FluentAssertions;
using NSubstitute;
using DominioConversacion = ElTejido.Domain.Conversaciones.Conversacion;

namespace ElTejido.UnitTests.Conversacion;

/// <summary>
/// P-26 corte 2 (spec §5.1–§5.3/§5.5, §11): resolucion determinista de campania — 0/1/N elegibles,
/// seleccion por numero/nombre/ambiguedad, conservacion e idempotencia del aporte, expiracion logica
/// de 24 h y revalidacion al aceptar. El LLM no participa en ninguna de estas decisiones.
/// </summary>
public sealed class ServicioEnrutamientoParticipacionTests
{
    private const string Numero = "573001112233";
    private static readonly DateTimeOffset Ahora = new(2026, 7, 31, 15, 0, 0, TimeSpan.Zero);

    private readonly EnrutamientosFake _enrutamientos = new();
    private readonly ConversacionesFake _conversaciones = new();
    private readonly IWhatsAppGateway _gateway = Substitute.For<IWhatsAppGateway>();
    private readonly IRepositorioRespuestas _respuestas = Substitute.For<IRepositorioRespuestas>();
    private readonly List<LogSeguridad> _logs = [];
    private readonly List<string> _enviados = [];
    private readonly Usuario _usuario;

    public ServicioEnrutamientoParticipacionTests()
    {
        _usuario = FabricasDominio.CrearUsuario("u_1", Numero, RolUsuario.Participante);
        _gateway.EnviarTextoAsync(
                Arg.Any<string>(),
                Arg.Do<string>(_enviados.Add),
                Arg.Any<TipoEnvioMensaje>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<string?>())
            .Returns(EnvioResultado.Ok("wamid.out"));
    }

    [Fact]
    public async Task Resolver_SinCampaniasElegibles_DevuelveSilencioNeutralSinMenu()
    {
        // Campania activa pero completada (hilo cerrado) y sin participacion continua.
        var candidato = Candidato("c_1");
        _conversaciones.Agregar(ConversacionCerrada("c_1", "p_1"));

        var resultado = await Servicio().ResolverAsync(_usuario, [candidato], Mensaje("wamid.raiz"), CancellationToken.None);

        resultado.Should().BeOfType<ResultadoEnrutamiento.SinElegibles>();
        _enviados.Should().BeEmpty();
        _enrutamientos.Documentos.Should().BeEmpty();
    }

    [Fact]
    public async Task Resolver_CampaniaCompletadaPeroContinua_SigueElegible()
    {
        var candidato = Candidato("c_1", participacionContinua: true);
        _conversaciones.Agregar(ConversacionCerrada("c_1", "p_1"));

        var resultado = await Servicio().ResolverAsync(_usuario, [candidato], Mensaje("wamid.raiz"), CancellationToken.None);

        resultado.Should().BeOfType<ResultadoEnrutamiento.ContinuarConversacion>()
            .Which.Candidato.Campania.Id.Should().Be("c_1");
    }

    [Fact]
    public async Task P28_SaludoSinFlujoYContinuidad_DespiertaSinGuardarAporteNiCrearMenu()
    {
        var candidato = Candidato("c_1", participacionContinua: true);
        _conversaciones.Agregar(ConversacionCerrada("c_1", "p_1"));

        var resultado = await Servicio(despertarProactivo: true)
            .ResolverAsync(_usuario, [candidato], Mensaje("wamid.hola", "Hola"), CancellationToken.None);

        resultado.Should().BeOfType<ResultadoEnrutamiento.DespertarProactivo>()
            .Which.Candidato.Campania.Id.Should().Be("c_1");
        _enrutamientos.Documentos.Should().BeEmpty("el saludo no es un aporte raíz");
        _enviados.Should().BeEmpty("el orquestador compone el saludo después de la decisión");
    }

    [Fact]
    public async Task P28_AporteSustantivoSinFlujo_NoEsSecuestradoPorElDespertar()
    {
        var candidato = Candidato("c_1", participacionContinua: true);
        _conversaciones.Agregar(ConversacionCerrada("c_1", "p_1"));

        var resultado = await Servicio(despertarProactivo: true)
            .ResolverAsync(_usuario, [candidato], Mensaje("wamid.idea", "Propongo cambiar el proceso de atención."), CancellationToken.None);

        resultado.Should().BeOfType<ResultadoEnrutamiento.ContinuarConversacion>()
            .Which.Contexto.Should().NotBeNull();
    }

    [Fact]
    public async Task P28_FlagApagado_ConservaLaRutaP26ParaElSaludo()
    {
        var candidato = Candidato("c_1", participacionContinua: true);
        _conversaciones.Agregar(ConversacionCerrada("c_1", "p_1"));

        var resultado = await Servicio()
            .ResolverAsync(_usuario, [candidato], Mensaje("wamid.hola", "Hola"), CancellationToken.None);

        resultado.Should().BeOfType<ResultadoEnrutamiento.ContinuarConversacion>();
    }

    [Fact]
    public async Task P28_VariasCampanias_SeleccionaYDespiertaSinEntregarElSaludoComoAporte()
    {
        var candidatos = new[]
        {
            Candidato("c_1", nombre: "Alfa", participacionContinua: true),
            Candidato("c_2", nombre: "Beta", participacionContinua: true),
        };
        _conversaciones.Agregar(ConversacionCerrada("c_1", "p_1"));
        _conversaciones.Agregar(ConversacionCerrada("c_2", "p_1"));
        var servicio = Servicio(despertarProactivo: true);

        var ofrecido = await servicio.ResolverAsync(_usuario, candidatos, Mensaje("wamid.hola", "Hola"), CancellationToken.None);
        var resuelto = await servicio.ResolverAsync(_usuario, candidatos, Mensaje("wamid.elegir", "2"), CancellationToken.None);

        ofrecido.Should().BeOfType<ResultadoEnrutamiento.SeleccionPendiente>();
        var despertar = resuelto.Should().BeOfType<ResultadoEnrutamiento.DespertarProactivo>().Which;
        despertar.Candidato.Campania.Id.Should().Be("c_2");
        var ruta = _enrutamientos.Documentos.Should().ContainSingle().Which;
        ruta.EsEntradaProactiva.Should().BeTrue();
        ruta.Estado.Should().Be(EstadoEnrutamientoAporte.Completado);
        ruta.ProcesadoEn.Should().BeNull("un saludo no se entrega como aporte");
    }

    [Fact]
    public async Task Resolver_UnaSolaElegible_ContinuaSinMenuNiPersistencia()
    {
        var candidato = Candidato("c_1");
        var mensaje = Mensaje("wamid.raiz");

        var resultado = await Servicio().ResolverAsync(_usuario, [candidato], mensaje, CancellationToken.None);

        var continuar = resultado.Should().BeOfType<ResultadoEnrutamiento.ContinuarConversacion>().Which;
        continuar.Mensaje.Should().Be(mensaje);
        continuar.EnrutamientoAporteId.Should().BeNull();
        _enviados.Should().BeEmpty();
        _enrutamientos.Documentos.Should().BeEmpty();
    }

    [Fact]
    public async Task Resolver_VariasElegibles_ConservaElAporteYEnviaMenuNumerado()
    {
        var mensaje = Mensaje("wamid.raiz", texto: "Se me ocurrio una idea");

        var resultado = await Servicio().ResolverAsync(
            _usuario,
            [Candidato("c_1", nombre: "Innovacion comercial"), Candidato("c_2", nombre: "Convencion de gerentes")],
            mensaje,
            CancellationToken.None);

        resultado.Should().BeOfType<ResultadoEnrutamiento.SeleccionPendiente>();
        var doc = _enrutamientos.Documentos.Should().ContainSingle().Which;
        doc.Estado.Should().Be(EstadoEnrutamientoAporte.SeleccionCampania);
        doc.TextoOriginal.Should().Be("Se me ocurrio una idea");
        doc.WhatsappMessageId.Should().Be("wamid.raiz");
        doc.CampaniasOfrecidas.Should().HaveCount(2);
        doc.CampaniasOfrecidas[0].Orden.Should().Be(1);
        var menu = _enviados.Should().ContainSingle().Which;
        menu.Should().Contain("1. Convencion de gerentes").And.Contain("2. Innovacion comercial");
        menu.Should().Contain("número o con el nombre");
        _logs.Should().Contain(l => l.Resultado == "ofrecido" && l.TipoEvento == TipoEventoSeguridad.EnrutamientoParticipacion);
        _logs.Should().OnlyContain(l => !l.Detalle!.Contains("Se me ocurrio"), "el texto del participante nunca va a telemetria");
    }

    [Fact]
    public async Task Resolver_MismoMensajeRaizDosVeces_NoDuplicaDocumentoNiMenu()
    {
        var servicio = Servicio();
        var candidatos = new[] { Candidato("c_1"), Candidato("c_2") };

        await servicio.ResolverAsync(_usuario, candidatos, Mensaje("wamid.raiz"), CancellationToken.None);
        _enrutamientos.SimularSinPendientes();
        var segunda = await servicio.ResolverAsync(_usuario, candidatos, Mensaje("wamid.raiz"), CancellationToken.None);

        segunda.Should().BeOfType<ResultadoEnrutamiento.SeleccionPendiente>();
        _enrutamientos.Documentos.Should().ContainSingle();
        _enviados.Should().ContainSingle("un reintento interno no reoferta el menu");
    }

    [Fact]
    public async Task Seleccion_PorNumero_EntregaElAporteOriginalYQuedaListo()
    {
        var servicio = Servicio();
        var candidatos = new[] { Candidato("c_1", nombre: "Alfa"), Candidato("c_2", nombre: "Beta") };
        await servicio.ResolverAsync(_usuario, candidatos, Mensaje("wamid.raiz", texto: "Mi gran idea"), CancellationToken.None);

        var resultado = await servicio.ResolverAsync(_usuario, candidatos, Mensaje("wamid.sel", texto: "2"), CancellationToken.None);

        var continuar = resultado.Should().BeOfType<ResultadoEnrutamiento.ContinuarConversacion>().Which;
        continuar.Candidato.Campania.Id.Should().Be("c_2");
        continuar.Mensaje.Texto.Should().Be("Mi gran idea", "la respuesta de seleccion no sustituye el aporte");
        continuar.Mensaje.WhatsappMessageId.Should().Be("wamid.raiz");
        continuar.EnrutamientoAporteId.Should().NotBeNull();
        var doc = _enrutamientos.Documentos.Single();
        doc.Estado.Should().Be(EstadoEnrutamientoAporte.Listo);
        doc.CampaniaSeleccionadaId.Should().Be("c_2");
        doc.IntentosSeleccion.Should().ContainSingle(i => i.Resultado == ResultadoIntentoSeleccion.Valido);
        _logs.Should().Contain(l => l.Resultado == "seleccionado");
    }

    [Fact]
    public async Task Seleccion_PorNombreNormalizado_AceptaAcentosYMayusculas()
    {
        var servicio = Servicio();
        var candidatos = new[] { Candidato("c_1", nombre: "Innovación Comercial"), Candidato("c_2", nombre: "Convencion") };
        await servicio.ResolverAsync(_usuario, candidatos, Mensaje("wamid.raiz"), CancellationToken.None);

        var resultado = await servicio.ResolverAsync(
            _usuario, candidatos, Mensaje("wamid.sel", texto: "  innovacion   comercial "), CancellationToken.None);

        resultado.Should().BeOfType<ResultadoEnrutamiento.ContinuarConversacion>()
            .Which.Candidato.Campania.Id.Should().Be("c_1");
    }

    [Fact]
    public async Task Seleccion_NombreAmbiguo_ExigeNumeroYReofreceElMenu()
    {
        var servicio = Servicio();
        var candidatos = new[] { Candidato("c_1", nombre: "Ideas"), Candidato("c_2", nombre: "Ideas") };
        await servicio.ResolverAsync(_usuario, candidatos, Mensaje("wamid.raiz"), CancellationToken.None);

        var resultado = await servicio.ResolverAsync(_usuario, candidatos, Mensaje("wamid.sel", texto: "Ideas"), CancellationToken.None);

        resultado.Should().BeOfType<ResultadoEnrutamiento.SeleccionPendiente>();
        var doc = _enrutamientos.Documentos.Single();
        doc.Estado.Should().Be(EstadoEnrutamientoAporte.SeleccionCampania);
        doc.IntentosSeleccion.Should().ContainSingle(i => i.Resultado == ResultadoIntentoSeleccion.Invalido);
        _enviados.Should().HaveCount(2);
        _enviados[1].Should().Contain("No reconocí esa opción");
        _logs.Should().Contain(l => l.Resultado == "invalido");
    }

    [Fact]
    public async Task Seleccion_OpcionInvalida_ConservaElAporteYVuelveAPedir()
    {
        var servicio = Servicio();
        var candidatos = new[] { Candidato("c_1"), Candidato("c_2") };
        await servicio.ResolverAsync(_usuario, candidatos, Mensaje("wamid.raiz", texto: "Mi idea"), CancellationToken.None);

        var resultado = await servicio.ResolverAsync(_usuario, candidatos, Mensaje("wamid.sel", texto: "99"), CancellationToken.None);

        resultado.Should().BeOfType<ResultadoEnrutamiento.SeleccionPendiente>();
        var doc = _enrutamientos.Documentos.Single();
        doc.TextoOriginal.Should().Be("Mi idea", "el aporte raiz no se pierde");
        doc.IntentosSeleccion.Should().ContainSingle(i =>
            i.Resultado == ResultadoIntentoSeleccion.Invalido && i.WhatsappMessageId == "wamid.sel");
    }

    [Fact]
    public async Task Seleccion_Vencida_SeMarcaExpiradaYElMensajeEmpiezaOtraResolucion()
    {
        var reloj = new RelojMutable(Ahora);
        var servicio = Servicio(reloj);
        var candidatos = new[] { Candidato("c_1"), Candidato("c_2") };
        await servicio.ResolverAsync(_usuario, candidatos, Mensaje("wamid.raiz", texto: "Idea original"), CancellationToken.None);

        reloj.Avanzar(TimeSpan.FromHours(25));
        var resultado = await servicio.ResolverAsync(_usuario, candidatos, Mensaje("wamid.nuevo", texto: "Otra idea"), CancellationToken.None);

        resultado.Should().BeOfType<ResultadoEnrutamiento.SeleccionPendiente>();
        _enrutamientos.Documentos.Should().HaveCount(2);
        var expirado = _enrutamientos.Documentos.Single(d => d.WhatsappMessageId == "wamid.raiz");
        expirado.Estado.Should().Be(EstadoEnrutamientoAporte.Expirado);
        expirado.TextoOriginal.Should().Be("Idea original", "la evidencia se conserva auditable");
        _enrutamientos.Documentos.Single(d => d.WhatsappMessageId == "wamid.nuevo")
            .Estado.Should().Be(EstadoEnrutamientoAporte.SeleccionCampania);
        _logs.Should().Contain(l => l.Resultado == "expirado");
    }

    [Fact]
    public async Task Seleccion_CampaniaCerradaEntreOfertaYSeleccion_RecalculaYAutoSeleccionaLaRestante()
    {
        var servicio = Servicio();
        var candidatosIniciales = new[] { Candidato("c_1", nombre: "Alfa"), Candidato("c_2", nombre: "Beta") };
        await servicio.ResolverAsync(_usuario, candidatosIniciales, Mensaje("wamid.raiz", texto: "Mi idea"), CancellationToken.None);

        // La campania elegida (1=Alfa) dejo de ser candidata (p. ej. cerrada por un admin).
        var candidatosActuales = new[] { Candidato("c_2", nombre: "Beta") };
        var resultado = await servicio.ResolverAsync(_usuario, candidatosActuales, Mensaje("wamid.sel", texto: "1"), CancellationToken.None);

        var continuar = resultado.Should().BeOfType<ResultadoEnrutamiento.ContinuarConversacion>().Which;
        continuar.Candidato.Campania.Id.Should().Be("c_2", "queda una unica elegible y se selecciona sin menu");
        continuar.Mensaje.Texto.Should().Be("Mi idea");
        _logs.Should().Contain(l => l.Resultado == "invalido" && l.Detalle!.Contains("revalidacion"));
    }

    [Fact]
    public async Task Seleccion_SinCampaniasRestantesTrasRevalidar_CancelaYQuedaAuditable()
    {
        var servicio = Servicio();
        var candidatos = new[] { Candidato("c_1"), Candidato("c_2") };
        await servicio.ResolverAsync(_usuario, candidatos, Mensaje("wamid.raiz", texto: "Mi idea"), CancellationToken.None);

        _conversaciones.Agregar(ConversacionCerrada("c_1", "p_1"));
        _conversaciones.Agregar(ConversacionCerrada("c_2", "p_1"));
        var resultado = await servicio.ResolverAsync(_usuario, candidatos, Mensaje("wamid.sel", texto: "1"), CancellationToken.None);

        resultado.Should().BeOfType<ResultadoEnrutamiento.SinElegibles>();
        var doc = _enrutamientos.Documentos.Single();
        doc.Estado.Should().Be(EstadoEnrutamientoAporte.Cancelado);
        doc.TextoOriginal.Should().Be("Mi idea");
    }

    [Fact]
    public async Task ConfirmarProcesado_DesdeListo_FijaEnIdeaProcesadoEnYConversacion()
    {
        var servicio = Servicio();
        var candidatos = new[] { Candidato("c_1"), Candidato("c_2") };
        await servicio.ResolverAsync(_usuario, candidatos, Mensaje("wamid.raiz"), CancellationToken.None);
        await servicio.ResolverAsync(_usuario, candidatos, Mensaje("wamid.sel", texto: "1"), CancellationToken.None);
        var campaniaElegida = _enrutamientos.Documentos.Single().CampaniaSeleccionadaId!;
        _conversaciones.Agregar(ConversacionAbierta(campaniaElegida, "p_1", "conv_nueva"));

        await servicio.ConfirmarProcesadoAsync(_usuario.Id, "wamid.raiz", CancellationToken.None);
        await servicio.ConfirmarProcesadoAsync(_usuario.Id, "wamid.raiz", CancellationToken.None);

        var doc = _enrutamientos.Documentos.Single();
        doc.Estado.Should().Be(EstadoEnrutamientoAporte.EnIdea);
        doc.ProcesadoEn.Should().NotBeNull();
        doc.ConversacionId.Should().Be("conv_nueva");
        _logs.Count(l => l.Resultado == "procesado").Should().Be(1, "la transicion listo→enIdea ocurre una sola vez");
    }

    // ---------------------------------------------------------------------------------------------
    // Corte 3: selección de pregunta (§5.4), afinidad durante el coaching (§5.6) y cambio explícito
    // de campaña (§5.1 paso 3).
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task Resolver_CampaniaContinuaConVariasPreguntas_ConservaAporteYPideLaPregunta()
    {
        var candidato = Candidato("c_1", participacionContinua: true, preguntas: 2);
        // Recorrido completo: sin trabajo pendiente, la campaña sigue elegible solo por continuidad.
        _conversaciones.Agregar(ConversacionCerrada("c_1", "p_1"));
        _conversaciones.Agregar(ConversacionCerrada("c_1", "p_2"));

        var resultado = await Servicio().ResolverAsync(
            _usuario, [candidato], Mensaje("wamid.raiz", texto: "Una idea nueva"), CancellationToken.None);

        resultado.Should().BeOfType<ResultadoEnrutamiento.SeleccionPendiente>();
        var doc = _enrutamientos.Documentos.Should().ContainSingle().Which;
        doc.Estado.Should().Be(EstadoEnrutamientoAporte.SeleccionPregunta);
        doc.CampaniaSeleccionadaId.Should().Be("c_1");
        doc.TextoOriginal.Should().Be("Una idea nueva");
        doc.PreguntasOfrecidas.Should().HaveCount(2, "campaña continua completada reabre todas sus preguntas activas");
        var menu = _enviados.Should().ContainSingle().Which;
        menu.Should().Contain("1. Pregunta 1").And.Contain("2. Pregunta 2");
        menu.Should().Contain("Responde con el número");
    }

    [Fact]
    public async Task Seleccion_PorNumeroDePregunta_EntregaElAporteDirigidoAEsaPregunta()
    {
        var candidato = Candidato("c_1", participacionContinua: true, preguntas: 2);
        _conversaciones.Agregar(ConversacionCerrada("c_1", "p_1"));
        _conversaciones.Agregar(ConversacionCerrada("c_1", "p_2"));
        var servicio = Servicio();
        await servicio.ResolverAsync(_usuario, [candidato], Mensaje("wamid.raiz", texto: "Mi idea"), CancellationToken.None);

        var resultado = await servicio.ResolverAsync(
            _usuario, [candidato], Mensaje("wamid.sel", texto: "2"), CancellationToken.None);

        var continuar = resultado.Should().BeOfType<ResultadoEnrutamiento.ContinuarConversacion>().Which;
        continuar.Mensaje.Texto.Should().Be("Mi idea", "la respuesta de selección no sustituye el aporte");
        continuar.Contexto.Should().NotBeNull();
        continuar.Contexto!.PreguntaId.Should().Be("p_2");
        var doc = _enrutamientos.Documentos.Single();
        doc.Estado.Should().Be(EstadoEnrutamientoAporte.Listo);
        doc.PreguntaSeleccionadaId.Should().Be("p_2");
        doc.IntentosSeleccion.Should().ContainSingle(i =>
            i.Tipo == TipoIntentoSeleccion.Pregunta && i.Resultado == ResultadoIntentoSeleccion.Valido);
    }

    [Fact]
    public async Task Seleccion_PreguntaInvalida_ConservaElAporteYVuelveAPedirla()
    {
        var candidato = Candidato("c_1", participacionContinua: true, preguntas: 2);
        _conversaciones.Agregar(ConversacionCerrada("c_1", "p_1"));
        _conversaciones.Agregar(ConversacionCerrada("c_1", "p_2"));
        var servicio = Servicio();
        await servicio.ResolverAsync(_usuario, [candidato], Mensaje("wamid.raiz", texto: "Mi idea"), CancellationToken.None);

        var resultado = await servicio.ResolverAsync(
            _usuario, [candidato], Mensaje("wamid.sel", texto: "99"), CancellationToken.None);

        resultado.Should().BeOfType<ResultadoEnrutamiento.SeleccionPendiente>();
        var doc = _enrutamientos.Documentos.Single();
        doc.Estado.Should().Be(EstadoEnrutamientoAporte.SeleccionPregunta);
        doc.TextoOriginal.Should().Be("Mi idea");
        doc.IntentosSeleccion.Should().ContainSingle(i => i.Resultado == ResultadoIntentoSeleccion.Invalido);
        _enviados.Should().HaveCount(2);
        _enviados[1].Should().Contain("No reconocí esa opción");
    }

    [Fact]
    public async Task Resolver_CampaniaContinuaConUnaSolaPregunta_EntregaSinMenu()
    {
        var candidato = Candidato("c_1", participacionContinua: true);
        _conversaciones.Agregar(ConversacionCerrada("c_1", "p_1"));

        var resultado = await Servicio().ResolverAsync(
            _usuario, [candidato], Mensaje("wamid.raiz", texto: "Otra idea"), CancellationToken.None);

        var continuar = resultado.Should().BeOfType<ResultadoEnrutamiento.ContinuarConversacion>().Which;
        continuar.Contexto!.PreguntaId.Should().Be("p_1");
        _enviados.Should().BeEmpty("una sola pregunta elegible no produce menú");
        _enrutamientos.Documentos.Should().BeEmpty();
    }

    [Fact]
    public async Task Afinidad_ConversacionAbierta_EnrutaLaRespuestaSinVolverAPreguntarCampania()
    {
        var candidatos = new[] { Candidato("c_1"), Candidato("c_2") };
        var abierta = ConversacionAbierta("c_1", "p_1", "conv_activa");
        _conversaciones.Agregar(abierta);
        await SembrarAfinidadAsync("c_1", "conv_activa");

        var resultado = await Servicio().ResolverAsync(
            _usuario, candidatos, Mensaje("wamid.coach", texto: "Le agrego un indicador de impacto"), CancellationToken.None);

        var continuar = resultado.Should().BeOfType<ResultadoEnrutamiento.ContinuarConversacion>().Which;
        continuar.Candidato.Campania.Id.Should().Be("c_1");
        continuar.Contexto!.PreguntaId.Should().Be("p_1");
        continuar.Mensaje.Texto.Should().Be("Le agrego un indicador de impacto");
        _enviados.Should().BeEmpty("una respuesta de coaching nunca vuelve a listar campañas");
    }

    [Fact]
    public async Task Afinidad_ConversacionCerrada_SeMarcaCompletadaYElAporteVuelveAResolverse()
    {
        var candidatos = new[] { Candidato("c_1"), Candidato("c_2") };
        _conversaciones.Agregar(ConversacionCerrada("c_1", "p_1"));
        await SembrarAfinidadAsync("c_1", "conv_c_1_p_1");

        var resultado = await Servicio().ResolverAsync(
            _usuario, candidatos, Mensaje("wamid.nuevo", texto: "Se me ocurrió otra cosa"), CancellationToken.None);

        _enrutamientos.Documentos.Single(d => d.WhatsappMessageId == "wamid.afinidad")
            .Estado.Should().Be(EstadoEnrutamientoAporte.Completado);
        // c_1 quedó completada y no es continua; c_2 sigue pendiente, así que se resuelve sola.
        resultado.Should().BeOfType<ResultadoEnrutamiento.ContinuarConversacion>()
            .Which.Candidato.Campania.Id.Should().Be("c_2");
    }

    [Fact]
    public async Task Afinidad_VentanaDeServicioVencida_DejaDeEnrutarAutomaticamente()
    {
        var reloj = new RelojMutable(Ahora);
        var candidatos = new[] { Candidato("c_1"), Candidato("c_2") };
        _conversaciones.Agregar(ConversacionAbierta("c_1", "p_1", "conv_activa"));
        await SembrarAfinidadAsync("c_1", "conv_activa");

        reloj.Avanzar(TimeSpan.FromHours(25));
        var resultado = await Servicio(reloj).ResolverAsync(
            _usuario, candidatos, Mensaje("wamid.tarde", texto: "Otra idea"), CancellationToken.None);

        // Sin afinidad vigente vuelve a resolver: dos campañas elegibles => menú.
        resultado.Should().BeOfType<ResultadoEnrutamiento.SeleccionPendiente>();
        _enviados.Should().ContainSingle().Which.Should().Contain("¿A cuál campaña corresponde tu aporte?");
    }

    [Fact]
    public async Task CambioExplicitoDeCampania_SuspendeLaAfinidadSinCerrarLaIdeaYReofreceElMenu()
    {
        var candidatos = new[] { Candidato("c_1", nombre: "Alfa"), Candidato("c_2", nombre: "Beta") };
        var abierta = ConversacionAbierta("c_1", "p_1", "conv_activa");
        _conversaciones.Agregar(abierta);
        await SembrarAfinidadAsync("c_1", "conv_activa");

        var resultado = await Servicio().ResolverAsync(
            _usuario, candidatos, Mensaje("wamid.cambio", texto: "otra campaña"), CancellationToken.None);

        resultado.Should().BeOfType<ResultadoEnrutamiento.SeleccionPendiente>();
        _enviados.Should().ContainSingle().Which.Should().Contain("¿A cuál campaña corresponde tu aporte?");
        _logs.Should().Contain(l => l.Resultado == "cambioCampania");
        _conversaciones.Todas.Single(c => c.Id == "conv_activa").Estado
            .Should().Be(EstadoConversacion.Abierta, "cambiar de campaña no cierra ni rechaza la idea suspendida");
    }

    [Fact]
    public async Task CambioExplicitoDeCampania_SinOtraCampaniaDisponible_ConservaLaAfinidadActual()
    {
        var candidatos = new[] { Candidato("c_1") };
        var abierta = ConversacionAbierta("c_1", "p_1", "conv_activa");
        _conversaciones.Agregar(abierta);
        await SembrarAfinidadAsync("c_1", "conv_activa");

        var resultado = await Servicio().ResolverAsync(
            _usuario, candidatos, Mensaje("wamid.cambio", texto: "otra campaña"), CancellationToken.None);

        var cambio = resultado.Should().BeOfType<ResultadoEnrutamiento.CambioCampaniaAplicado>().Which;
        cambio.Candidato.Campania.Id.Should().Be("c_1");
        cambio.ConversacionAbierta!.Id.Should().Be("conv_activa");
        _enviados.Should().BeEmpty();
    }

    // P-26 §12 criterio 11: apagar el interruptor deja terminar la idea abierta y bloquea la siguiente.
    [Fact]
    public async Task FlagApagado_ConIdeaAbierta_LaDejaTerminar()
    {
        // La campaña ya no es continua, pero su conversación sigue abierta con afinidad vigente.
        var candidato = Candidato("c_1", participacionContinua: false);
        _conversaciones.Agregar(ConversacionAbierta("c_1", "p_1", "conv_activa"));
        await SembrarAfinidadAsync("c_1", "conv_activa");

        var resultado = await Servicio().ResolverAsync(
            _usuario, [candidato], Mensaje("wamid.sigue", texto: "Le agrego el indicador"), CancellationToken.None);

        resultado.Should().BeOfType<ResultadoEnrutamiento.ContinuarConversacion>()
            .Which.Contexto!.PreguntaId.Should().Be("p_1");
        _enviados.Should().BeEmpty("la idea en curso termina sin menús");
    }

    [Fact]
    public async Task FlagApagado_TrasCerrarLaIdea_BloqueaLaSiguiente()
    {
        var candidato = Candidato("c_1", participacionContinua: false);
        _conversaciones.Agregar(ConversacionCerrada("c_1", "p_1"));
        await SembrarAfinidadAsync("c_1", "conv_c_1_p_1");

        var resultado = await Servicio().ResolverAsync(
            _usuario, [candidato], Mensaje("wamid.otra", texto: "Se me ocurrió otra idea"), CancellationToken.None);

        resultado.Should().BeOfType<ResultadoEnrutamiento.SinElegibles>("sin continuidad no se abren ciclos nuevos");
        _enrutamientos.Documentos.Single(d => d.WhatsappMessageId == "wamid.afinidad")
            .Estado.Should().Be(EstadoEnrutamientoAporte.Completado);
    }

    [Fact]
    public async Task P30_FlagApagado_ConservaLaReaperturaAcotadaAnterior()
    {
        var candidato = Candidato("c_1");
        _conversaciones.Agregar(ConversacionCerrada("c_1", "p_1"));
        ConfigurarIdeasHistoricas([IdeaHistorica("idea_1", "conv_antigua", "Idea anterior", cerrada: true)]);

        var resultado = await Servicio(retomarIdeas: false).ResolverAsync(
            _usuario, [candidato], Mensaje("wamid.retomar", "quiero retomar una idea"), CancellationToken.None);

        resultado.Should().BeOfType<ResultadoEnrutamiento.SinElegibles>();
        _enrutamientos.Documentos.Should().BeEmpty();
        _enviados.Should().BeEmpty();
    }

    [Fact]
    public async Task P30_ListaSinFiltrarEstado_SeleccionaTituloExactoYCompletaAuditoria()
    {
        var candidato = Candidato("c_1");
        _conversaciones.Agregar(ConversacionCerrada("c_1", "p_1"));
        var descartada = IdeaHistorica("idea_1", "conv_1", "Idea descartada", cerrada: true, rechazada: true);
        var pendiente = IdeaHistorica("idea_2", "conv_2", "Idea pendiente", cerrada: false);
        ConfigurarIdeasHistoricas([descartada, pendiente]);
        var servicio = Servicio(retomarIdeas: true);

        var ofrecido = await servicio.ResolverAsync(
            _usuario, [candidato], Mensaje("wamid.retomar", "quiero retomar una idea"), CancellationToken.None);
        var seleccionado = await servicio.ResolverAsync(
            _usuario, [candidato], Mensaje("wamid.seleccion", "Idea pendiente"), CancellationToken.None);

        ofrecido.Should().BeOfType<ResultadoEnrutamiento.SeleccionPendiente>();
        _enviados.Should().ContainSingle().Which.Should()
            .Contain("Idea descartada (descartada)")
            .And.Contain("Idea pendiente (en proceso)");
        var retomar = seleccionado.Should().BeOfType<ResultadoEnrutamiento.RetomarIdea>().Which;
        retomar.Contexto.IdeaId.Should().Be("idea_2");
        retomar.Contexto.ConversacionId.Should().Be("conv_2");
        var ruta = _enrutamientos.Documentos.Should().ContainSingle().Which;
        ruta.Estado.Should().Be(EstadoEnrutamientoAporte.Listo);
        ruta.Modo.Should().Be(ModoEnrutamientoAporte.RetomarIdea);
        ruta.IntentosSeleccion.Should().ContainSingle(i => i.Tipo == TipoIntentoSeleccion.Idea);

        await servicio.ConfirmarRetomadaAsync(_usuario.Id, "wamid.retomar", true, CancellationToken.None);

        _enrutamientos.Documentos.Should().ContainSingle().Which.Estado
            .Should().Be(EstadoEnrutamientoAporte.EnIdea, "la reapertura queda como afinidad al ciclo historico");
        _logs.Should().Contain(log => log.TipoEvento == TipoEventoSeguridad.RetomarIdea && log.Resultado == "ofrecido");
        _logs.Should().OnlyContain(log => !log.Detalle!.Contains("Idea pendiente"));
    }

    /// <summary>Deja un enrutamiento en <c>enIdea</c> apuntando a una conversación (afinidad vigente §5.6).</summary>
    private Task SembrarAfinidadAsync(string campaniaId, string conversacionId)
        => _enrutamientos.GuardarAsync(
            EnrutamientoAporte.Crear(
                    _usuario.Id,
                    "wamid.afinidad",
                    "Aporte que abrió la idea",
                    EstadoEnrutamientoAporte.Listo,
                    Ahora,
                    campaniaSeleccionadaId: campaniaId,
                    preguntaSeleccionadaId: "p_1")
                .MarcarEnIdea(conversacionId, Ahora),
            CancellationToken.None);

    private ServicioEnrutamientoParticipacion Servicio(
        TimeProvider? reloj = null,
        bool despertarProactivo = false,
        bool retomarIdeas = false)
    {
        var logSeguridad = Substitute.For<IRepositorioLogSeguridad>();
        logSeguridad.RegistrarAsync(Arg.Do<LogSeguridad>(_logs.Add), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        return new ServicioEnrutamientoParticipacion(
            _enrutamientos,
            _conversaciones,
            _gateway,
            logSeguridad,
            Substitute.For<IProveedorCorrelacion>(),
            new OpcionesConversacion
            {
                DespertarProactivoHabilitado = despertarProactivo,
                RetomarIdeasHabilitado = retomarIdeas,
            },
            reloj ?? new RelojFijo(Ahora),
            _respuestas);
    }

    private IdeaConsolidada IdeaHistorica(
        string ideaId,
        string conversacionId,
        string texto,
        bool cerrada,
        bool rechazada = false)
    {
        var versionId = ideaId + "_v1";
        var version = VersionIdeaConsolidada.Crear(
            versionId,
            "c_1",
            ideaId,
            1,
            null,
            texto,
            ["aporte_1"],
            ["aporte_1"],
            TipoAporteIdea.Inicial,
            EstadoConfirmacionVersionIdea.Propuesta,
            null,
            null,
            null,
            null,
            Ahora.AddDays(-2));
        var idea = IdeaConsolidada.Crear(
                ideaId, "c_1", _usuario.Id, "p_1", conversacionId, "resp_1", 1, Ahora.AddDays(-2))
            .ConPropuesta(versionId, Ahora.AddDays(-2));
        if (cerrada)
        {
            version = version.Confirmar(Ahora.AddDays(-2));
            idea = idea.ConfirmarVersion(versionId, Ahora.AddDays(-2)).Cerrar(
                rechazada ? EstadoResultadoIdeaConsolidada.Rechazada : EstadoResultadoIdeaConsolidada.Pendiente,
                null,
                rechazada ? "rechazoParticipante" : "participante",
                Ahora.AddDays(-1));
        }

        _respuestas.ObtenerVersionIdeaAsync("c_1", versionId, Arg.Any<CancellationToken>())
            .Returns(version);
        return idea;
    }

    private void ConfigurarIdeasHistoricas(IReadOnlyCollection<IdeaConsolidada> ideas)
    {
        _respuestas.ListarIdeasHistoricasAsync("c_1", _usuario.Id, "p_1", Arg.Any<CancellationToken>())
            .Returns(ideas);
    }

    private CandidatoCampania Candidato(
        string campaniaId,
        string? nombre = null,
        bool participacionContinua = false,
        int preguntas = 1)
    {
        var activas = Enumerable.Range(1, preguntas)
            .Select(orden => FabricasDominio.CrearPregunta($"p_{orden}", orden))
            .ToArray();
        var campania = FabricasDominio.CrearCampania(
            campaniaId,
            EstadoCampania.Activa,
            activas,
            nombre,
            ConfigConversacional.Crear(1, "Gracias.", participacionContinua: participacionContinua));
        var participante = FabricasDominio.CrearParticipante($"pc_{campaniaId}", campaniaId, _usuario.Id, Numero);
        return new CandidatoCampania(participante, campania, activas[0]);
    }

    private static MensajeEntrante Mensaje(string wamid, string texto = "Hola")
        => new(Numero, texto, wamid, Ahora);

    private DominioConversacion ConversacionCerrada(string campaniaId, string preguntaId)
        => DominioConversacion
            .Iniciar($"conv_{campaniaId}_{preguntaId}", campaniaId, _usuario.Id, preguntaId, "whatsapp", null, Ahora.AddDays(-1))
            .Cerrar(Ahora.AddHours(-1));

    private DominioConversacion ConversacionAbierta(string campaniaId, string preguntaId, string id)
        => DominioConversacion.Iniciar(id, campaniaId, _usuario.Id, preguntaId, "whatsapp", null, Ahora);

    private sealed class EnrutamientosFake : IRepositorioEnrutamientosAporte
    {
        private readonly Dictionary<string, EnrutamientoAporte> _documentos = new(StringComparer.Ordinal);
        private bool _ocultarPendientes;

        public IReadOnlyList<EnrutamientoAporte> Documentos => _documentos.Values.ToArray();

        /// <summary>Simula el reintento interno: el pendiente no se ve en la busqueda inicial pero el id determinista choca.</summary>
        public void SimularSinPendientes() => _ocultarPendientes = true;

        public Task GuardarAsync(EnrutamientoAporte enrutamiento, CancellationToken cancellationToken)
        {
            _documentos[enrutamiento.Id] = enrutamiento;
            return Task.CompletedTask;
        }

        public Task<EnrutamientoAporte?> ObtenerPorMensajeAsync(string usuarioId, string whatsappMessageId, CancellationToken cancellationToken)
            => Task.FromResult(_documentos.GetValueOrDefault(EnrutamientoAporte.GenerarId(usuarioId, whatsappMessageId)));

        public Task<IReadOnlyCollection<EnrutamientoAporte>> ListarPorUsuarioAsync(string usuarioId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<EnrutamientoAporte>>(
                _ocultarPendientes
                    ? Array.Empty<EnrutamientoAporte>()
                    : _documentos.Values.Where(e => e.UsuarioId == usuarioId).ToArray());
    }

    private sealed class ConversacionesFake : IRepositorioConversaciones
    {
        private readonly List<DominioConversacion> _conversaciones = [];

        public IReadOnlyList<DominioConversacion> Todas => _conversaciones.ToArray();

        public void Agregar(DominioConversacion conversacion) => _conversaciones.Add(conversacion);

        public Task GuardarConversacionAsync(DominioConversacion conversacion, CancellationToken cancellationToken)
        {
            _conversaciones.Add(conversacion);
            return Task.CompletedTask;
        }

        public Task<DominioConversacion?> ObtenerConversacionAsync(string campaniaId, string conversacionId, CancellationToken cancellationToken)
            => Task.FromResult(_conversaciones.FirstOrDefault(c => c.Id == conversacionId && c.CampaniaId == campaniaId));

        public Task<IReadOnlyCollection<DominioConversacion>> ListarConversacionesAsync(string campaniaId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<DominioConversacion>>(
                _conversaciones.Where(c => c.CampaniaId == campaniaId).ToArray());

        public Task<IReadOnlyCollection<DominioConversacion>> ListarAbiertasInactivasAsync(string campaniaId, DateTimeOffset limite, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<DominioConversacion>>(Array.Empty<DominioConversacion>());

        public Task<IReadOnlyCollection<Mensaje>> ListarMensajesAsync(string campaniaId, string conversacionId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<Mensaje>>(Array.Empty<Mensaje>());

        public Task GuardarMensajeAsync(Mensaje mensaje, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<ConteoBorradoConversaciones> EliminarPorUsuarioAsync(string campaniaId, string? usuarioId, CancellationToken cancellationToken)
            => Task.FromResult(new ConteoBorradoConversaciones(0, 0));
    }

    private sealed class RelojMutable : TimeProvider
    {
        private DateTimeOffset _ahora;

        public RelojMutable(DateTimeOffset inicio) => _ahora = inicio;

        public void Avanzar(TimeSpan lapso) => _ahora += lapso;

        public override DateTimeOffset GetUtcNow() => _ahora;
    }
}
