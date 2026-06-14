using ElTejido.Application.Campanas;
using ElTejido.Application.Common;
using ElTejido.Application.Participantes;
using ElTejido.Application.Usuarios;
using ElTejido.Application.WhatsApp;
using ElTejido.Domain.Campanas;
using ElTejido.Domain.Common;
using ElTejido.Domain.Identidad;
using ElTejido.Domain.Participantes;
using ElTejido.Domain.Usuarios;
using ElTejido.Infrastructure.WhatsApp;
using ElTejido.UnitTests.Soporte;
using FluentAssertions;
using NSubstitute;

namespace ElTejido.UnitTests.WhatsApp;

public sealed class ServicioEnviosTests
{
    private const string CampaniaId = "c_1";
    private const string Numero = "573001112233";
    private static readonly DateTimeOffset Epoca = DateTimeOffset.UnixEpoch;

    private readonly IRepositorioCampanias _campanias = Substitute.For<IRepositorioCampanias>();
    private readonly IRepositorioParticipantes _participantes = Substitute.For<IRepositorioParticipantes>();
    private readonly IRepositorioUsuarios _usuarios = Substitute.For<IRepositorioUsuarios>();
    private readonly List<TrabajoEnvio> _encolados = [];
    private readonly IColaEnvios _cola = Substitute.For<IColaEnvios>();
    private readonly AlmacenJobsMemoria _jobs = new(new RelojFijo(Epoca));

    public ServicioEnviosTests()
    {
        _cola.EncolarAsync(Arg.Do<TrabajoEnvio>(_encolados.Add), Arg.Any<CancellationToken>());
        _usuarios.ObtenerUsuarioPorIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => CrearUsuario(call.Arg<string>()));
    }

    [Fact]
    public async Task EncolarIniciales_CampaniaNoActiva_LanzaConflicto()
    {
        ConfigurarCampania(EstadoCampania.Borrador);

        var accion = () => Construir().EncolarInicialesAsync(CampaniaId, null, null, CancellationToken.None);

        await accion.Should().ThrowAsync<ErrorConflicto>();
    }

    [Fact]
    public async Task EncolarIniciales_DosPendientes_EncolaAmbos()
    {
        ConfigurarCampania(EstadoCampania.Activa);
        ConfigurarParticipantes(
            CrearParticipante("u_1", EstadoEnvio.Pendiente, EstadoRespuestaParticipante.SinRespuesta),
            CrearParticipante("u_2", EstadoEnvio.Pendiente, EstadoRespuestaParticipante.SinRespuesta));

        var resultado = await Construir().EncolarInicialesAsync(CampaniaId, null, null, CancellationToken.None);

        resultado.Encolados.Should().Be(2);
        resultado.Estado.Should().Be("enProceso");
        _encolados.Should().HaveCount(2);
        _encolados.Should().OnlyContain(t => t.Tipo == TipoEnvioMensaje.Inicial && t.MensajeInicialId == "mi_1");
        _jobs.ObtenerJob(resultado.JobId).Should().NotBeNull();
    }

    [Fact]
    public async Task EncolarIniciales_OmiteParticipantesYaEnviados()
    {
        ConfigurarCampania(EstadoCampania.Activa);
        ConfigurarParticipantes(
            CrearParticipante("u_1", EstadoEnvio.Enviado, EstadoRespuestaParticipante.SinRespuesta),
            CrearParticipante("u_2", EstadoEnvio.Pendiente, EstadoRespuestaParticipante.SinRespuesta));

        var resultado = await Construir().EncolarInicialesAsync(CampaniaId, null, null, CancellationToken.None);

        resultado.Encolados.Should().Be(1);
        _encolados.Should().ContainSingle().Which.UsuarioId.Should().Be("u_2");
    }

    [Fact]
    public async Task EncolarIniciales_RenderizaVariablesEnTextoLibre()
    {
        ConfigurarCampania(EstadoCampania.Activa);
        ConfigurarParticipantes(CrearParticipante("u_1", EstadoEnvio.Pendiente, EstadoRespuestaParticipante.SinRespuesta));

        await Construir().EncolarInicialesAsync(CampaniaId, null, null, CancellationToken.None);

        _encolados.Should().ContainSingle().Which.TextoLibre.Should().Be("Hola Usuario u_1");
    }

    [Fact]
    public async Task Reenviar_SoloAlcanzaSinRespuesta()
    {
        ConfigurarCampania(EstadoCampania.Activa);
        ConfigurarParticipantes(
            CrearParticipante("u_1", EstadoEnvio.Enviado, EstadoRespuestaParticipante.Respondio),
            CrearParticipante("u_2", EstadoEnvio.Enviado, EstadoRespuestaParticipante.SinRespuesta));

        var resultado = await Construir().ReenviarSinRespuestaAsync(CampaniaId, null, CancellationToken.None);

        resultado.Encolados.Should().Be(1);
        _encolados.Should().ContainSingle().Which.Tipo.Should().Be(TipoEnvioMensaje.Reenvio);
    }

    [Fact]
    public async Task Reintentar_SoloAlcanzaErrores()
    {
        ConfigurarCampania(EstadoCampania.Activa);
        ConfigurarParticipantes(
            CrearParticipante("u_1", EstadoEnvio.Enviado, EstadoRespuestaParticipante.SinRespuesta),
            CrearParticipante("u_2", EstadoEnvio.Error, EstadoRespuestaParticipante.SinRespuesta));

        var resultado = await Construir().ReintentarErroresAsync(CampaniaId, null, CancellationToken.None);

        resultado.Encolados.Should().Be(1);
        _encolados.Should().ContainSingle().Which.UsuarioId.Should().Be("u_2");
    }

    private ServicioEnvios Construir()
        => new(_campanias, _participantes, _usuarios, _cola, _jobs);

    private void ConfigurarCampania(EstadoCampania estado)
        => _campanias.ObtenerCampaniaPorIdAsync(CampaniaId, Arg.Any<CancellationToken>())
            .Returns(CrearCampania(estado));

    private void ConfigurarParticipantes(params ParticipanteCampania[] participantes)
        => _participantes.ListarParticipantesAsync(CampaniaId, Arg.Any<CancellationToken>())
            .Returns(participantes);

    private static Campania CrearCampania(EstadoCampania estado)
    {
        var mensaje = MensajeInicial.Crear(
            "mi_1",
            "saludo",
            "Hola {{nombre}}",
            1,
            new[] { "nombre" },
            EstadoRegistro.Activo,
            null);

        return Campania.Crear(
            CampaniaId,
            "Campania",
            "Descripcion",
            "Objetivo",
            estado,
            new[] { mensaje },
            new[] { FabricasDominio.CrearPregunta("p_1", 1) },
            "rub_1",
            null,
            "llm_1",
            ConfigMarkdown.Crear(TipoArtefactoMarkdown.Respuesta),
            ConfigConversacional.Crear(1, "Gracias."),
            LimitesSeguridad.Crear(1500, 10, 2),
            null,
            Epoca,
            Epoca);
    }

    private static ParticipanteCampania CrearParticipante(
        string usuarioId,
        EstadoEnvio estadoEnvio,
        EstadoRespuestaParticipante estadoRespuesta)
        => ParticipanteCampania.Crear(
            "pc_" + usuarioId,
            CampaniaId,
            usuarioId,
            NumeroWhatsApp.FromNormalized(Numero),
            EstadoRegistro.Activo,
            estadoEnvio,
            estadoRespuesta,
            Epoca,
            null,
            null);

    private static Usuario CrearUsuario(string id)
        => Usuario.Crear(
            id,
            "Usuario " + id,
            NumeroWhatsApp.FromNormalized(Numero),
            RolUsuario.Participante,
            EstadoRegistro.Activo,
            "Operaciones",
            "GHT",
            null,
            null,
            Epoca,
            Epoca);
}
