using ElTejido.Application.Conversacion;
using ElTejido.Domain.Conversaciones;
using FluentAssertions;

namespace ElTejido.UnitTests.Conversacion;

public sealed class PoliticaColaCoachingIdeasTests
{
    private static readonly DateTimeOffset Ahora = DateTimeOffset.UnixEpoch.AddHours(1);
    private readonly PoliticaColaCoachingIdeas _politica = new();

    [Fact]
    public void Crear_ActivaSoloLaPrimeraPendienteYConservaFinalizadas()
    {
        var cola = _politica.Crear(
            "wamid.raiz",
            new[]
            {
                new RaizIdeaCoaching(1, "resp_1", MotivoFinalizacionIdea.Umbral),
                new RaizIdeaCoaching(2, "resp_2", null),
                new RaizIdeaCoaching(3, "resp_3", null),
            },
            Ahora);

        cola.IdeaActivaIndice.Should().Be(2);
        cola.Ideas.Should().ContainSingle(idea => idea.Estado == EstadoIdeaCoaching.Activa);
        cola.Ideas[0].MotivoFinalizacion.Should().Be(MotivoFinalizacionIdea.Umbral);
    }

    [Fact]
    public void FinalizarActiva_ActivaLaSiguienteYAlAgotarseFinalizaLaCola()
    {
        var cola = _politica.Crear(
            "wamid.raiz",
            new[]
            {
                new RaizIdeaCoaching(1, "resp_1", null),
                new RaizIdeaCoaching(2, "resp_2", null),
            },
            Ahora);
        cola = _politica.RegistrarRepregunta(cola);
        cola = _politica.ActualizarRespuestaVigente(cola, "resp_1_rev_1");

        cola = _politica.FinalizarActiva(cola, MotivoFinalizacionIdea.Participante, Ahora.AddMinutes(1));

        cola.IdeaActivaIndice.Should().Be(2);
        cola.Ideas[0].RespuestaVigenteId.Should().Be("resp_1_rev_1");
        cola.Ideas[0].MotivoFinalizacion.Should().Be(MotivoFinalizacionIdea.Participante);

        cola = _politica.FinalizarActiva(cola, MotivoFinalizacionIdea.MaxRevisiones, Ahora.AddMinutes(2));

        cola.Estado.Should().Be(EstadoCoachingIdeas.Finalizado);
        cola.IdeaActiva.Should().BeNull();
        cola.Ideas.Should().OnlyContain(idea => idea.Estado == EstadoIdeaCoaching.Finalizada);
    }

    [Fact]
    public void ActualizarVersionIdeaVigente_ConservaElAporteYEstableceLaReferenciaCanonica()
    {
        var cola = _politica.Crear(
            "wamid.raiz",
            new[] { new RaizIdeaCoaching(1, "resp_1", null) },
            Ahora);

        cola = _politica.ActualizarRespuestaVigente(cola, "resp_1_complemento");
        cola = _politica.ActualizarVersionIdeaVigente(cola, "idea_1", "idea_1_v2");

        cola.IdeaActiva.Should().BeEquivalentTo(new
        {
            RespuestaVigenteId = "resp_1_complemento",
            IdeaId = "idea_1",
            VersionIdeaVigenteId = "idea_1_v2",
        });
    }
}
