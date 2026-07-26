using ElTejido.Application.Campanas;
using ElTejido.Application.Common;
using ElTejido.Application.Conversacion;
using ElTejido.Application.Seguridad;
using ElTejido.Domain.Campanas;
using ElTejido.Domain.Common;
using ElTejido.Domain.Conversaciones;
using ElTejido.UnitTests.Soporte;
using FluentAssertions;
using NSubstitute;
using DominioConversacion = ElTejido.Domain.Conversaciones.Conversacion;

namespace ElTejido.UnitTests.Conversacion;

public sealed class ServicioExpiracionConversacionesTests
{
    private static readonly DateTimeOffset Ahora = DateTimeOffset.UnixEpoch.AddDays(10);

    private readonly IRepositorioConversaciones _conversaciones = Substitute.For<IRepositorioConversaciones>();
    private readonly IRepositorioCampanias _campanias = Substitute.For<IRepositorioCampanias>();
    private readonly IRepositorioLogSeguridad _logSeguridad = Substitute.For<IRepositorioLogSeguridad>();
    private readonly IProveedorCorrelacion _correlacion = Substitute.For<IProveedorCorrelacion>();
    private readonly IOrquestadorConversacion _orquestador = Substitute.For<IOrquestadorConversacion>();

    [Fact]
    public async Task CerrarExpiradas_Habilitada_CierraLosHilosAbiertosDeCadaCampania()
    {
        SembrarCampanias(Campania("c_1"));
        var conv1 = DominioConversacion.Iniciar("conv_1", "c_1", "u_1", "p_1", "whatsapp", null, DateTimeOffset.UnixEpoch);
        var conv2 = DominioConversacion.Iniciar("conv_2", "c_1", "u_2", "p_1", "whatsapp", null, DateTimeOffset.UnixEpoch);
        _conversaciones.ListarAbiertasInactivasAsync("c_1", Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(new[] { conv1, conv2 });

        var cerradas = await Construir(horas: 48).CerrarExpiradasAsync(CancellationToken.None);

        cerradas.Should().Be(2);
        await _conversaciones.Received(2).GuardarConversacionAsync(
            Arg.Is<DominioConversacion>(c => c.Estado == EstadoConversacion.Cerrada), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CerrarExpiradas_HorasLegacy_UsaElPlazoEnMinutosEquivalente()
    {
        SembrarCampanias(Campania("c_1"));
        _conversaciones.ListarAbiertasInactivasAsync("c_1", Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<DominioConversacion>());

        await Construir(horas: 48).CerrarExpiradasAsync(CancellationToken.None);

        // 48h heredadas = 2880 min: el limite debe ser "ahora - 2880 min".
        await _conversaciones.Received(1).ListarAbiertasInactivasAsync(
            "c_1", Ahora.AddMinutes(-2880), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CerrarExpiradas_MinutosPorCampania_GananSobreElDefaultGlobal()
    {
        SembrarCampanias(Campania("c_1", minutosInactividad: 5));
        _conversaciones.ListarAbiertasInactivasAsync("c_1", Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<DominioConversacion>());

        await Construir(minutos: 30).CerrarExpiradasAsync(CancellationToken.None);

        // El override por campaña (5 min) manda sobre el default global (30 min).
        await _conversaciones.Received(1).ListarAbiertasInactivasAsync(
            "c_1", Ahora.AddMinutes(-5), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CerrarExpiradas_CampaniaOverrideCero_NoConsultaEsaCampania()
    {
        SembrarCampanias(Campania("c_1", minutosInactividad: 0));

        var cerradas = await Construir(minutos: 30).CerrarExpiradasAsync(CancellationToken.None);

        cerradas.Should().Be(0);
        await _conversaciones.DidNotReceiveWithAnyArgs().ListarAbiertasInactivasAsync(default!, default, default);
    }

    [Fact]
    public async Task CerrarExpiradas_TimeoutI18_FinalizaActivaYAvanzaSinCerrarLasDemas()
    {
        var campania = Campania(
            "c_1",
            coachingSecuencialIdeas: true,
            minutosCoachingPorIdea: 5);
        SembrarCampanias(campania);
        var cola = new PoliticaColaCoachingIdeas().Crear(
            "wamid.raiz",
            new[]
            {
                new RaizIdeaCoaching(1, "resp_1", null),
                new RaizIdeaCoaching(2, "resp_2", null),
            },
            Ahora.AddMinutes(-10));
        var conversacion = DominioConversacion
            .Iniciar("conv_1", "c_1", "u_1", "p_1", "whatsapp", null, Ahora.AddMinutes(-10))
            .ConCoachingIdeas(cola);
        _conversaciones.ListarConversacionesAsync("c_1", Arg.Any<CancellationToken>())
            .Returns(new[] { conversacion });

        var transiciones = await Construir().CerrarExpiradasAsync(CancellationToken.None);

        transiciones.Should().Be(1);
        await _conversaciones.Received(1).GuardarConversacionAsync(
            Arg.Is<DominioConversacion>(guardada =>
                guardada.Estado == EstadoConversacion.Abierta
                && guardada.CoachingIdeas!.IdeaActivaIndice == 2
                && guardada.CoachingIdeas.Ideas[0].MotivoFinalizacion == MotivoFinalizacionIdea.Tiempo),
            Arg.Any<CancellationToken>());
        await _logSeguridad.Received(1).RegistrarAsync(
            Arg.Is<ElTejido.Domain.Seguridad.LogSeguridad>(log =>
                log.TipoEvento == ElTejido.Domain.Seguridad.TipoEventoSeguridad.CoachingSecuencialIdeas
                && log.Resultado == "timeout"
                && log.Detalle!.Contains("motivo:tiempo", StringComparison.Ordinal)),
            Arg.Any<CancellationToken>());
        await _orquestador.Received(1).EnviarTurnoCoachingPendienteAsync(
            Arg.Is<DominioConversacion>(guardada => guardada.CoachingIdeas!.IdeaActivaIndice == 2),
            campania,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CerrarExpiradas_TimeoutI18_VentanaWhatsAppCerrada_NoEnviaTextoLibre()
    {
        var campania = Campania(
            "c_1",
            coachingSecuencialIdeas: true,
            minutosCoachingPorIdea: 5);
        SembrarCampanias(campania);
        var cola = new PoliticaColaCoachingIdeas().Crear(
            "wamid.raiz",
            new[]
            {
                new RaizIdeaCoaching(1, "resp_1", null),
                new RaizIdeaCoaching(2, "resp_2", null),
            },
            Ahora.AddHours(-25));
        var conversacion = DominioConversacion
            .Iniciar("conv_1", "c_1", "u_1", "p_1", "whatsapp", null, Ahora.AddHours(-25))
            .ConCoachingIdeas(cola);
        _conversaciones.ListarConversacionesAsync("c_1", Arg.Any<CancellationToken>())
            .Returns(new[] { conversacion });

        await Construir().CerrarExpiradasAsync(CancellationToken.None);

        await _orquestador.DidNotReceiveWithAnyArgs()
            .EnviarTurnoCoachingPendienteAsync(default!, default!, default);
    }

    [Fact]
    public async Task CerrarExpiradas_Desactivada_NoConsultaNiCierra()
    {
        var servicio = Construir(coachingHabilitado: false);

        servicio.Habilitada.Should().BeFalse();
        (await servicio.CerrarExpiradasAsync(CancellationToken.None)).Should().Be(0);

        await _campanias.DidNotReceiveWithAnyArgs().BuscarCampaniasAsync(default!, default);
        await _conversaciones.DidNotReceiveWithAnyArgs().ListarAbiertasInactivasAsync(default!, default, default);
        await _conversaciones.DidNotReceiveWithAnyArgs().GuardarConversacionAsync(default!, default);
    }

    private void SembrarCampanias(params Campania[] campanias)
        => _campanias.BuscarCampaniasAsync(Arg.Any<FiltroCampanias>(), Arg.Any<CancellationToken>()).Returns(campanias);

    private static Campania Campania(
        string id,
        int? minutosInactividad = null,
        bool coachingSecuencialIdeas = false,
        int? minutosCoachingPorIdea = null)
        => ElTejido.Domain.Campanas.Campania.Crear(
            id, $"Campania {id}", "Descripcion", "Objetivo", EstadoCampania.Activa,
            mensajesIniciales: null,
            new[] { FabricasDominio.CrearPregunta("p_1", 1) },
            "rub_1", promptRefs: null, "llm_1",
            ConfigMarkdown.Crear(TipoArtefactoMarkdown.Campania),
            ConfigConversacional.Crear(
                1,
                "Gracias por participar.",
                segmentacionIdeas: coachingSecuencialIdeas,
                minutosInactividadSesion: minutosInactividad,
                coachingSecuencialIdeas: coachingSecuencialIdeas,
                minutosCoachingPorIdea: minutosCoachingPorIdea),
            LimitesSeguridad.Crear(1500, 10, 2),
            usuariosHabilitados: null, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);

    private ServicioExpiracionConversaciones Construir(
        int horas = 0,
        int minutos = 0,
        bool coachingHabilitado = true)
        => new(
            _conversaciones,
            _campanias,
            _logSeguridad,
            _correlacion,
            _orquestador,
            new OpcionesConversacion
            {
                HorasExpiracionSinRespuesta = horas,
                MinutosInactividadSesion = minutos,
                CoachingSecuencialIdeas = coachingHabilitado,
            },
            new RelojFijo(Ahora));
}
