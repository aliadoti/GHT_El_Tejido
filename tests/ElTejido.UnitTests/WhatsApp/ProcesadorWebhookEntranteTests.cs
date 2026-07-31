using ElTejido.Application.Common;
using ElTejido.Application.Conversacion;
using ElTejido.Application.Identidad;
using ElTejido.Application.Participantes;
using ElTejido.Application.Seguridad;
using ElTejido.Application.WhatsApp;
using ElTejido.Domain.Campanas;
using ElTejido.Domain.Common;
using ElTejido.Domain.Seguridad;
using ElTejido.Domain.Usuarios;
using ElTejido.UnitTests.Soporte;
using FluentAssertions;
using NSubstitute;

namespace ElTejido.UnitTests.WhatsApp;

public sealed class ProcesadorWebhookEntranteTests
{
    private const string Numero = "573001112233";

    private readonly IWhatsAppGateway _gateway = Substitute.For<IWhatsAppGateway>();
    private readonly IRegistroWebhookDedupe _dedupe = Substitute.For<IRegistroWebhookDedupe>();
    private readonly ILimitadorNumeroEntrante _limitadorNumero = Substitute.For<ILimitadorNumeroEntrante>();
    private readonly IResolutorParticipante _resolutor = Substitute.For<IResolutorParticipante>();
    private readonly IServicioEnrutamientoParticipacion _enrutamiento = Substitute.For<IServicioEnrutamientoParticipacion>();
    private readonly IOrquestadorConversacion _orquestador = Substitute.For<IOrquestadorConversacion>();
    private readonly IRepositorioLogSeguridad _logSeguridad = Substitute.For<IRepositorioLogSeguridad>();
    private readonly IProveedorCorrelacion _correlacion = Substitute.For<IProveedorCorrelacion>();
    private readonly MensajeEntrante _mensaje = new(Numero, "Mi idea", "wamid.ABC", DateTimeOffset.UnixEpoch);

    public ProcesadorWebhookEntranteTests()
    {
        // Por defecto el rate por numero permite (los casos que lo prueban lo sobrescriben).
        _limitadorNumero.RegistrarYPermitirAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
    }

    [Fact]
    public async Task Procesar_PayloadSinMensaje_DevuelveNoMensaje()
    {
        _gateway.ParsearWebhook(Arg.Any<WhatsAppWebhookPayload>()).Returns((MensajeEntrante?)null);

        var resultado = await Construir().ProcesarAsync(new WhatsAppWebhookPayload(), CancellationToken.None);

        resultado.Estado.Should().Be(ResultadoProcesoEntrante.NoMensaje);
        await _dedupe.DidNotReceiveWithAnyArgs().IntentarRegistrarMensajeAsync(default!, default, default);
    }

    [Fact]
    public async Task Procesar_MensajeRepetido_DevuelveDuplicado()
    {
        _gateway.ParsearWebhook(Arg.Any<WhatsAppWebhookPayload>()).Returns(_mensaje);
        _dedupe.IntentarRegistrarMensajeAsync(_mensaje.WhatsappMessageId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var resultado = await Construir().ProcesarAsync(new WhatsAppWebhookPayload(), CancellationToken.None);

        resultado.Estado.Should().Be(ResultadoProcesoEntrante.Duplicado);
        await _resolutor.DidNotReceiveWithAnyArgs().ResolverCandidatosAsync(default!, default);
    }

    [Fact]
    public async Task Procesar_NumeroNoAutorizado_DevuelveNoAutorizado()
    {
        _gateway.ParsearWebhook(Arg.Any<WhatsAppWebhookPayload>()).Returns(_mensaje);
        _dedupe.IntentarRegistrarMensajeAsync(Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _resolutor.ResolverCandidatosAsync(Numero, Arg.Any<CancellationToken>())
            .Returns(new ResultadoCandidatos.NoAutorizado(MotivoRechazo.NoMatriculado));

        var resultado = await Construir().ProcesarAsync(new WhatsAppWebhookPayload(), CancellationToken.None);

        resultado.Estado.Should().Be(ResultadoProcesoEntrante.NoAutorizado);
        resultado.Motivo.Should().Be(MotivoRechazo.NoMatriculado);
        await _orquestador.DidNotReceiveWithAnyArgs().ProcesarMensajeEntranteAsync(default!, default!, default);
    }

    [Fact]
    public async Task Procesar_Autorizado_EntregaAlOrquestador()
    {
        var candidato = AutorizarConCandidato();
        _enrutamiento.ResolverAsync(Arg.Any<Usuario>(), Arg.Any<IReadOnlyList<CandidatoCampania>>(), Arg.Any<MensajeEntrante>(), Arg.Any<CancellationToken>())
            .Returns(ci => new ResultadoEnrutamiento.ContinuarConversacion(candidato, ci.Arg<MensajeEntrante>(), null));

        var resultado = await Construir().ProcesarAsync(new WhatsAppWebhookPayload(), CancellationToken.None);

        resultado.Estado.Should().Be(ResultadoProcesoEntrante.Procesado);
        await _orquestador.Received(1).ProcesarMensajeEntranteAsync(
            Arg.Is<ParticipanteResuelto>(p => p.Campania.Id == candidato.Campania.Id),
            Arg.Any<MensajeEntrante>(),
            Arg.Any<CancellationToken>());
        await _enrutamiento.DidNotReceiveWithAnyArgs().ConfirmarProcesadoAsync(default!, default!, default);
    }

    [Fact]
    public async Task Procesar_AporteConservado_ConfirmaProcesadoTrasElOrquestador()
    {
        var candidato = AutorizarConCandidato();
        _enrutamiento.ResolverAsync(Arg.Any<Usuario>(), Arg.Any<IReadOnlyList<CandidatoCampania>>(), Arg.Any<MensajeEntrante>(), Arg.Any<CancellationToken>())
            .Returns(ci => new ResultadoEnrutamiento.ContinuarConversacion(
                candidato, ci.Arg<MensajeEntrante>(), "route_u_1_wamid.ABC"));

        var resultado = await Construir().ProcesarAsync(new WhatsAppWebhookPayload(), CancellationToken.None);

        resultado.Estado.Should().Be(ResultadoProcesoEntrante.Procesado);
        Received.InOrder(() =>
        {
            _orquestador.ProcesarMensajeEntranteAsync(Arg.Any<ParticipanteResuelto>(), Arg.Any<MensajeEntrante>(), Arg.Any<CancellationToken>());
            _enrutamiento.ConfirmarProcesadoAsync("u_1", "wamid.ABC", Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task Procesar_VariasCampanias_DevuelveSeleccionPendienteSinOrquestador()
    {
        AutorizarConCandidato();
        _enrutamiento.ResolverAsync(Arg.Any<Usuario>(), Arg.Any<IReadOnlyList<CandidatoCampania>>(), Arg.Any<MensajeEntrante>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoEnrutamiento.SeleccionPendiente("route_u_1_wamid.ABC"));

        var resultado = await Construir().ProcesarAsync(new WhatsAppWebhookPayload(), CancellationToken.None);

        resultado.Estado.Should().Be(ResultadoProcesoEntrante.SeleccionCampaniaPendiente);
        await _orquestador.DidNotReceiveWithAnyArgs().ProcesarMensajeEntranteAsync(default!, default!, default);
    }

    [Fact]
    public async Task Procesar_SinElegibles_DevuelveSilencioNeutral()
    {
        AutorizarConCandidato();
        _enrutamiento.ResolverAsync(Arg.Any<Usuario>(), Arg.Any<IReadOnlyList<CandidatoCampania>>(), Arg.Any<MensajeEntrante>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoEnrutamiento.SinElegibles());

        var resultado = await Construir().ProcesarAsync(new WhatsAppWebhookPayload(), CancellationToken.None);

        resultado.Estado.Should().Be(ResultadoProcesoEntrante.SinCampaniaElegible);
        await _orquestador.DidNotReceiveWithAnyArgs().ProcesarMensajeEntranteAsync(default!, default!, default);
    }

    [Fact]
    public async Task Procesar_MensajeMuyLargo_SeAcotaAlMaximoDeCampania()
    {
        var largo = new string('a', 5000);
        var candidato = AutorizarConCandidato(texto: largo);
        _enrutamiento.ResolverAsync(Arg.Any<Usuario>(), Arg.Any<IReadOnlyList<CandidatoCampania>>(), Arg.Any<MensajeEntrante>(), Arg.Any<CancellationToken>())
            .Returns(ci => new ResultadoEnrutamiento.ContinuarConversacion(candidato, ci.Arg<MensajeEntrante>(), null));

        MensajeEntrante? entregado = null;
        await _orquestador.ProcesarMensajeEntranteAsync(
            Arg.Any<ParticipanteResuelto>(),
            Arg.Do<MensajeEntrante>(m => entregado = m),
            Arg.Any<CancellationToken>());

        await Construir().ProcesarAsync(new WhatsAppWebhookPayload(), CancellationToken.None);

        entregado.Should().NotBeNull();
        entregado!.Texto.Length.Should().Be(1500);
    }

    [Fact]
    public async Task Procesar_NumeroExcedeRate_DescartaYRegistraRateNumero()
    {
        _gateway.ParsearWebhook(Arg.Any<WhatsAppWebhookPayload>()).Returns(_mensaje);
        _dedupe.IntentarRegistrarMensajeAsync(Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _limitadorNumero.RegistrarYPermitirAsync(Numero, Arg.Any<CancellationToken>()).Returns(false);
        LogSeguridad? capturado = null;
        _logSeguridad.When(x => x.RegistrarAsync(Arg.Any<LogSeguridad>(), Arg.Any<CancellationToken>()))
            .Do(ci => capturado = ci.Arg<LogSeguridad>());

        var resultado = await Construir().ProcesarAsync(new WhatsAppWebhookPayload(), CancellationToken.None);

        resultado.Estado.Should().Be(ResultadoProcesoEntrante.RateLimitado);
        await _resolutor.DidNotReceiveWithAnyArgs().ResolverCandidatosAsync(default!, default);
        await _orquestador.DidNotReceiveWithAnyArgs().ProcesarMensajeEntranteAsync(default!, default!, default);
        capturado.Should().NotBeNull();
        capturado!.TipoEvento.Should().Be(TipoEventoSeguridad.RateLimit);
        capturado.Detalle.Should().Be("rate_numero");
        capturado.Numero.Should().Be(Numero);
    }

    private ProcesadorWebhookEntrante Construir()
        => new(_gateway, _dedupe, _limitadorNumero, _resolutor, _enrutamiento, _orquestador, _logSeguridad, _correlacion, new RelojFijo(DateTimeOffset.UnixEpoch));

    /// <summary>Mockea parseo, dedupe y candidatos autorizados; devuelve el candidato unico.</summary>
    private CandidatoCampania AutorizarConCandidato(string? texto = null)
    {
        _gateway.ParsearWebhook(Arg.Any<WhatsAppWebhookPayload>())
            .Returns(texto is null ? _mensaje : _mensaje with { Texto = texto });
        _dedupe.IntentarRegistrarMensajeAsync(Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var resuelto = CrearParticipanteResuelto();
        var candidato = new CandidatoCampania(resuelto.Participante, resuelto.Campania, resuelto.PreguntaVigente);
        _resolutor.ResolverCandidatosAsync(Numero, Arg.Any<CancellationToken>())
            .Returns(new ResultadoCandidatos.Autorizado(resuelto.Usuario, new[] { candidato }));
        return candidato;
    }

    private static ParticipanteResuelto CrearParticipanteResuelto()
    {
        var pregunta = FabricasDominio.CrearPregunta("p_1", 1);
        var campania = FabricasDominio.CrearCampania("c_1", EstadoCampania.Activa, new[] { pregunta });
        var participante = FabricasDominio.CrearParticipante("pc_1", "c_1", "u_1", Numero);
        var usuario = Usuario.Crear(
            "u_1",
            "Ana",
            ElTejido.Domain.Identidad.NumeroWhatsApp.FromNormalized(Numero),
            RolUsuario.Participante,
            EstadoRegistro.Activo,
            "Operaciones",
            "GHT",
            null,
            null,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch);
        return new ParticipanteResuelto(usuario, campania, participante, pregunta);
    }
}
