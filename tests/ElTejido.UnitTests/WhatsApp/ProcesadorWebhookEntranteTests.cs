using ElTejido.Application.Conversacion;
using ElTejido.Application.Identidad;
using ElTejido.Application.Participantes;
using ElTejido.Application.WhatsApp;
using ElTejido.Domain.Campanas;
using ElTejido.Domain.Common;
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
    private readonly IResolutorParticipante _resolutor = Substitute.For<IResolutorParticipante>();
    private readonly IOrquestadorConversacion _orquestador = Substitute.For<IOrquestadorConversacion>();
    private readonly MensajeEntrante _mensaje = new(Numero, "Mi idea", "wamid.ABC", DateTimeOffset.UnixEpoch);

    [Fact]
    public async Task Procesar_PayloadSinMensaje_DevuelveNoMensaje()
    {
        _gateway.ParsearWebhook(Arg.Any<WhatsAppWebhookPayload>()).Returns((MensajeEntrante?)null);

        var resultado = await Construir().ProcesarAsync(new WhatsAppWebhookPayload(), CancellationToken.None);

        resultado.Should().Be(ResultadoProcesoEntrante.NoMensaje);
        await _dedupe.DidNotReceiveWithAnyArgs().IntentarRegistrarMensajeAsync(default!, default, default);
    }

    [Fact]
    public async Task Procesar_MensajeRepetido_DevuelveDuplicado()
    {
        _gateway.ParsearWebhook(Arg.Any<WhatsAppWebhookPayload>()).Returns(_mensaje);
        _dedupe.IntentarRegistrarMensajeAsync(_mensaje.WhatsappMessageId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var resultado = await Construir().ProcesarAsync(new WhatsAppWebhookPayload(), CancellationToken.None);

        resultado.Should().Be(ResultadoProcesoEntrante.Duplicado);
        await _resolutor.DidNotReceiveWithAnyArgs().ResolverAsync(default!, default);
    }

    [Fact]
    public async Task Procesar_NumeroNoAutorizado_DevuelveNoAutorizado()
    {
        _gateway.ParsearWebhook(Arg.Any<WhatsAppWebhookPayload>()).Returns(_mensaje);
        _dedupe.IntentarRegistrarMensajeAsync(Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _resolutor.ResolverAsync(Numero, Arg.Any<CancellationToken>())
            .Returns(new ResultadoResolucion.NoAutorizado(MotivoRechazo.NoMatriculado));

        var resultado = await Construir().ProcesarAsync(new WhatsAppWebhookPayload(), CancellationToken.None);

        resultado.Should().Be(ResultadoProcesoEntrante.NoAutorizado);
        await _orquestador.DidNotReceiveWithAnyArgs().ProcesarMensajeEntranteAsync(default!, default!, default);
    }

    [Fact]
    public async Task Procesar_Autorizado_EntregaAlOrquestador()
    {
        _gateway.ParsearWebhook(Arg.Any<WhatsAppWebhookPayload>()).Returns(_mensaje);
        _dedupe.IntentarRegistrarMensajeAsync(Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _resolutor.ResolverAsync(Numero, Arg.Any<CancellationToken>())
            .Returns(new ResultadoResolucion.Autorizado(CrearParticipanteResuelto()));

        var resultado = await Construir().ProcesarAsync(new WhatsAppWebhookPayload(), CancellationToken.None);

        resultado.Should().Be(ResultadoProcesoEntrante.Procesado);
        await _orquestador.Received(1).ProcesarMensajeEntranteAsync(
            Arg.Any<ParticipanteResuelto>(),
            Arg.Any<MensajeEntrante>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Procesar_MensajeMuyLargo_SeAcotaAlMaximoDeCampania()
    {
        var largo = new string('a', 5000);
        _gateway.ParsearWebhook(Arg.Any<WhatsAppWebhookPayload>())
            .Returns(_mensaje with { Texto = largo });
        _dedupe.IntentarRegistrarMensajeAsync(Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _resolutor.ResolverAsync(Numero, Arg.Any<CancellationToken>())
            .Returns(new ResultadoResolucion.Autorizado(CrearParticipanteResuelto()));

        MensajeEntrante? entregado = null;
        await _orquestador.ProcesarMensajeEntranteAsync(
            Arg.Any<ParticipanteResuelto>(),
            Arg.Do<MensajeEntrante>(m => entregado = m),
            Arg.Any<CancellationToken>());

        await Construir().ProcesarAsync(new WhatsAppWebhookPayload(), CancellationToken.None);

        entregado.Should().NotBeNull();
        entregado!.Texto.Length.Should().Be(1500);
    }

    private ProcesadorWebhookEntrante Construir()
        => new(_gateway, _dedupe, _resolutor, _orquestador, new RelojFijo(DateTimeOffset.UnixEpoch));

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
