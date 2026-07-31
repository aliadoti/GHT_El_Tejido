using ElTejido.Application.Configuracion;
using ElTejido.Application.Participantes;
using ElTejido.Application.Usuarios;
using ElTejido.Domain.Campanas;
using ElTejido.Infrastructure.Persistencia.Memoria;
using FluentAssertions;
using NSubstitute;

namespace ElTejido.UnitTests.Configuracion;

/// <summary>
/// P-26 corte 1 (07 §2): crear, editar y duplicar una campania preserva
/// <c>participacionContinua</c>; el default y las campanias historicas quedan en <c>false</c>.
/// </summary>
public sealed class ServicioGestionCampaniasParticipacionContinuaTests
{
    private readonly ServicioGestionCampanias _servicio;

    public ServicioGestionCampaniasParticipacionContinuaTests()
        => _servicio = new ServicioGestionCampanias(
            new RepositorioCampaniasMemoria(),
            Substitute.For<IRepositorioUsuarios>(),
            Substitute.For<IRepositorioParticipantes>(),
            TimeProvider.System);

    [Fact]
    public async Task Crear_SinElFlag_QuedaApagadoPorDefecto()
    {
        var campania = await _servicio.CrearCampaniaAsync(Solicitud(Config()), CancellationToken.None);

        campania.ConfigConversacional.ParticipacionContinua.Should().BeFalse();
    }

    [Fact]
    public async Task CrearYEditar_HacenRoundTripDelFlag()
    {
        var creada = await _servicio.CrearCampaniaAsync(
            Solicitud(Config(participacionContinua: true)),
            CancellationToken.None);
        creada.ConfigConversacional.ParticipacionContinua.Should().BeTrue();

        var apagada = await _servicio.ActualizarCampaniaAsync(
            creada.Id,
            new SolicitudActualizarCampania(
                null, null, null, null, null, null, null, Config(participacionContinua: false), null),
            CancellationToken.None);
        apagada.ConfigConversacional.ParticipacionContinua.Should().BeFalse();

        var releida = await _servicio.ObtenerCampaniaAsync(creada.Id, CancellationToken.None);
        releida.ConfigConversacional.ParticipacionContinua.Should().BeFalse();
    }

    [Fact]
    public async Task Duplicar_CopiaLaEleccionExplicitaDelFlag()
    {
        var original = await _servicio.CrearCampaniaAsync(
            Solicitud(Config(participacionContinua: true)),
            CancellationToken.None);

        var copia = await _servicio.DuplicarCampaniaAsync(original.Id, CancellationToken.None);

        copia.Id.Should().NotBe(original.Id);
        copia.ConfigConversacional.ParticipacionContinua.Should().BeTrue();
    }

    private static SolicitudGuardarCampania Solicitud(ConfigConversacional config)
        => new(
            "Convencion 2026",
            "Captura de ideas",
            "Recolectar y evaluar ideas",
            "r_general",
            null,
            "llm_default",
            ConfigMarkdown.Crear(TipoArtefactoMarkdown.Respuesta),
            config,
            LimitesSeguridad.Crear(1500, 10, 2));

    private static ConfigConversacional Config(bool participacionContinua = false)
        => ConfigConversacional.Crear(1, "Gracias.", participacionContinua: participacionContinua);
}
