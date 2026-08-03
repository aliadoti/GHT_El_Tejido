using ElTejido.Application.Conversacion;
using ElTejido.Domain.Conversaciones;
using ElTejido.Infrastructure.Conversaciones;
using FluentAssertions;
using DominioConversacion = ElTejido.Domain.Conversaciones.Conversacion;

namespace ElTejido.UnitTests.Conversacion;

public sealed class ConversacionCosmosMappingTests
{
    [Fact]
    public void Conversacion_RoundTrip_ConservaColaI18YLegacyQuedaSinCola()
    {
        var ahora = DateTimeOffset.UnixEpoch.AddHours(1);
        var cola = new PoliticaColaCoachingIdeas().Crear(
            "wamid.raiz",
            new[]
            {
                new RaizIdeaCoaching(1, "resp_1", null, "idea_1", "idea_1_v1"),
                new RaizIdeaCoaching(2, "resp_2", null),
            },
            ahora);
        cola = new PoliticaColaCoachingIdeas().RegistrarRepregunta(cola);
        var conversacion = DominioConversacion
            .Iniciar("conv_1", "c_1", "u_1", "p_1", "whatsapp", null, ahora)
            .ConCoachingIdeas(cola);

        var resultado = ConversacionCosmosDocument.FromDomain(conversacion).ToDomain();
        var legacy = new ConversacionCosmosDocument
        {
            Id = "conv_legacy",
            CampaniaId = "c_1",
            UsuarioId = "u_1",
            PreguntaId = "p_1",
            VentanaServicioVenceEn = ahora.AddHours(24),
            FechaInicio = ahora,
        }.ToDomain();

        resultado.CoachingIdeas.Should().NotBeNull();
        resultado.CoachingIdeas!.IdeaActivaIndice.Should().Be(1);
        resultado.CoachingIdeas.IdeaActiva!.RepreguntasUsadas.Should().Be(1);
        resultado.CoachingIdeas.IdeaActiva.IdeaId.Should().Be("idea_1");
        resultado.CoachingIdeas.IdeaActiva.VersionIdeaVigenteId.Should().Be("idea_1_v1");
        legacy.CoachingIdeas.Should().BeNull();
    }

    [Fact]
    public void Conversacion_RoundTrip_ConservaCicloP26YDocumentoHistoricoEquivaleACiclo1()
    {
        var ahora = DateTimeOffset.UnixEpoch.AddHours(1);
        var conversacion = DominioConversacion.Iniciar(
            "conv_c2",
            "c_1",
            "u_1",
            "p_1",
            "whatsapp",
            null,
            ahora,
            cicloParticipacion: 2,
            origenAporteMessageId: "wamid.raiz2",
            enrutamientoAporteId: "route_u_1_wamid.raiz2");

        var resultado = ConversacionCosmosDocument.FromDomain(conversacion).ToDomain();
        var historico = new ConversacionCosmosDocument
        {
            Id = "conv_legacy",
            CampaniaId = "c_1",
            UsuarioId = "u_1",
            PreguntaId = "p_1",
            VentanaServicioVenceEn = ahora.AddHours(24),
            FechaInicio = ahora,
        }.ToDomain();

        resultado.CicloParticipacion.Should().Be(2);
        resultado.OrigenAporteMessageId.Should().Be("wamid.raiz2");
        resultado.EnrutamientoAporteId.Should().Be("route_u_1_wamid.raiz2");
        historico.CicloParticipacion.Should().Be(1);
        historico.OrigenAporteMessageId.Should().BeNull();
        historico.EnrutamientoAporteId.Should().BeNull();
    }

    [Fact]
    public void Conversacion_CicloParticipacionMenorA1_Lanza()
    {
        var ahora = DateTimeOffset.UnixEpoch.AddHours(1);

        var acto = () => DominioConversacion.Iniciar(
            "conv_1", "c_1", "u_1", "p_1", "whatsapp", null, ahora, cicloParticipacion: 0);

        acto.Should().Throw<ElTejido.Domain.Common.DomainValidationException>();
    }

    [Fact]
    public void Conversacion_RoundTrip_ConservaElEstadoDeSeleccionDeIdea()
    {
        var ahora = DateTimeOffset.UnixEpoch.AddHours(1);
        var conversacion = DominioConversacion
            .Iniciar("conv_1", "c_1", "u_1", "p_1", "whatsapp", null, ahora)
            .AvanzarA(EstadoMaquinaConversacion.EsperandoSeleccionIdea);

        var documento = ConversacionCosmosDocument.FromDomain(conversacion);

        documento.EstadoMaquina.Should().Be("esperandoSeleccionIdea");
        documento.ToDomain().EstadoMaquina.Should().Be(EstadoMaquinaConversacion.EsperandoSeleccionIdea);
    }

    [Fact]
    public void Conversacion_RoundTrip_ConservaAclaracionDeSalidaP27YDocumentoHistoricoQuedaSinPendiente()
    {
        var ahora = DateTimeOffset.UnixEpoch.AddHours(1);
        var conversacion = DominioConversacion
            .Iniciar("conv_1", "c_1", "u_1", "p_1", "whatsapp", null, ahora)
            .AvanzarA(EstadoMaquinaConversacion.EsperandoConfirmacionSalida)
            .ConIntencionControlPendiente(IntencionControlPendiente.Crear(1, ahora));
        var documento = ConversacionCosmosDocument.FromDomain(conversacion);
        var historico = new ConversacionCosmosDocument
        {
            Id = "conv_legacy",
            CampaniaId = "c_1",
            UsuarioId = "u_1",
            PreguntaId = "p_1",
            VentanaServicioVenceEn = ahora.AddHours(24),
            FechaInicio = ahora,
        };

        documento.EstadoMaquina.Should().Be("esperandoConfirmacionSalida");
        documento.IntencionControlPendiente.Should().NotBeNull();
        documento.IntencionControlPendiente!.Tipo.Should().Be("aclararSalida");
        documento.ToDomain().IntencionControlPendiente!.IntentosInvalidos.Should().Be(1);
        documento.ToDomain().IntencionControlPendiente!.CreadoEn.Should().Be(ahora);
        historico.ToDomain().IntencionControlPendiente.Should().BeNull();
    }

    [Fact]
    public void Conversacion_AclaracionDeSalidaP27_SeLimpiaAlVolverARepreguntaOCerrar()
    {
        var ahora = DateTimeOffset.UnixEpoch.AddHours(1);
        var esperandoSalida = DominioConversacion
            .Iniciar("conv_1", "c_1", "u_1", "p_1", "whatsapp", null, ahora)
            .AvanzarA(EstadoMaquinaConversacion.EsperandoConfirmacionSalida)
            .ConIntencionControlPendiente(IntencionControlPendiente.Crear(0, ahora));

        esperandoSalida.AvanzarA(EstadoMaquinaConversacion.EsperandoRepregunta)
            .IntencionControlPendiente.Should().BeNull();
        esperandoSalida.Cerrar(ahora).IntencionControlPendiente.Should().BeNull();
    }
}
