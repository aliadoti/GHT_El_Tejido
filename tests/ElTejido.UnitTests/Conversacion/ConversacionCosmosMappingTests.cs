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
}
