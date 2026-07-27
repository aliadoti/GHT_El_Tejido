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

    [Fact]
    public void AgregarIdeaPendiente_EncolaAlFinalSinTocarLaActiva()
    {
        var cola = _politica.Crear(
            "wamid.raiz",
            new[] { new RaizIdeaCoaching(1, "resp_1", null, "idea_1", "idea_1_v1") },
            Ahora);

        cola = _politica.AgregarIdeaPendiente(
            cola, new RaizIdeaCoaching(2, "resp_1_rev_1_n1", null, "idea_nueva", "idea_nueva_v1"), maxIdeas: 5);

        cola.Ideas.Should().HaveCount(2);
        cola.IdeaActivaIndice.Should().Be(1);
        cola.Ideas[1].Should().BeEquivalentTo(new
        {
            IdeaIndice = 2,
            RespuestaRaizId = "resp_1_rev_1_n1",
            RespuestaVigenteId = "resp_1_rev_1_n1",
            Estado = EstadoIdeaCoaching.Pendiente,
            IdeaId = "idea_nueva",
            VersionIdeaVigenteId = "idea_nueva_v1",
            RepreguntasUsadas = 0,
        });
    }

    [Fact]
    public void AgregarIdeaPendiente_EsIdempotenteYRespetaElTope()
    {
        var cola = _politica.Crear(
            "wamid.raiz",
            new[] { new RaizIdeaCoaching(1, "resp_1", null, "idea_1", "idea_1_v1") },
            Ahora);
        var nueva = new RaizIdeaCoaching(2, "resp_nueva", null, "idea_nueva", "idea_nueva_v1");

        cola = _politica.AgregarIdeaPendiente(cola, nueva, maxIdeas: 5);
        var repetida = _politica.AgregarIdeaPendiente(cola, nueva, maxIdeas: 5);
        var topeAlcanzado = _politica.AgregarIdeaPendiente(
            cola, new RaizIdeaCoaching(3, "resp_tercera", null, "idea_tercera", "idea_tercera_v1"), maxIdeas: 2);

        repetida.Ideas.Should().HaveCount(2);
        topeAlcanzado.Ideas.Should().HaveCount(2);
        _politica.PuedeAgregarIdea(cola, maxIdeas: 2).Should().BeFalse();
    }

    [Fact]
    public void AgregarIdeaPendiente_ColaFinalizada_NoEncolaNada()
    {
        var cola = _politica.Crear(
            "wamid.raiz",
            new[] { new RaizIdeaCoaching(1, "resp_1", null, "idea_1", "idea_1_v1") },
            Ahora);
        cola = _politica.FinalizarActiva(cola, MotivoFinalizacionIdea.Umbral, Ahora);

        var resultado = _politica.AgregarIdeaPendiente(
            cola, new RaizIdeaCoaching(2, "resp_nueva", null, "idea_nueva", "idea_nueva_v1"), maxIdeas: 5);

        cola.Estado.Should().Be(EstadoCoachingIdeas.Finalizado);
        resultado.Ideas.Should().HaveCount(1);
    }
}
