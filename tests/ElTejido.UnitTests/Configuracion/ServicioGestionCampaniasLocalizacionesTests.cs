using ElTejido.Application.Campanas;
using ElTejido.Application.Common;
using ElTejido.Application.Configuracion;
using ElTejido.Application.Participantes;
using ElTejido.Application.Usuarios;
using ElTejido.Domain.Campanas;
using ElTejido.Domain.Common;
using ElTejido.Domain.Identidad;
using ElTejido.Domain.Participantes;
using ElTejido.Domain.Usuarios;
using ElTejido.Infrastructure.Persistencia.Memoria;
using FluentAssertions;
using NSubstitute;

namespace ElTejido.UnitTests.Configuracion;

public sealed class ServicioGestionCampaniasLocalizacionesTests
{
    private readonly IRepositorioUsuarios _usuarios = Substitute.For<IRepositorioUsuarios>();
    private readonly IRepositorioParticipantes _participantes = Substitute.For<IRepositorioParticipantes>();
    private readonly ServicioGestionCampanias _servicio;

    public ServicioGestionCampaniasLocalizacionesTests()
        => _servicio = new ServicioGestionCampanias(
            new RepositorioCampaniasMemoria(),
            _usuarios,
            _participantes,
            TimeProvider.System,
            new OpcionesCatalogoTextos { Habilitado = true });

    [Fact]
    public async Task Activar_CuandoFaltaContenidoIngles_DevuelveValidacion()
    {
        var campania = await CrearCampaniaAsync();
        await _servicio.AgregarMensajeInicialAsync(campania.Id, Mensaje(), CancellationToken.None);
        await _servicio.AgregarPreguntaAsync(campania.Id, Pregunta(), CancellationToken.None);
        await _servicio.ActualizarLocalizacionesAsync(
            campania.Id,
            new SolicitudActualizarLocalizacionesCampania(
                ["es", "en"],
                new Dictionary<string, LocalizacionCampania>
                {
                    ["en"] = LocalizacionCampania.Crear("en", "Convention", null, null, null, null, null),
                }),
            CancellationToken.None);

        var accion = () => _servicio.CambiarEstadoCampaniaAsync(campania.Id, EstadoCampania.Activa, CancellationToken.None);

        await accion.Should().ThrowAsync<ErrorValidacion>()
            .WithMessage("*localizaciones completas*");
    }

    [Fact]
    public async Task Asociar_UsuarioConIdiomaNoHabilitado_DevuelveConflictoTipificado()
    {
        var campania = await CrearCampaniaAsync();
        _usuarios.ObtenerUsuarioPorIdAsync("u_en", Arg.Any<CancellationToken>()).Returns(UsuarioIngles());

        var accion = () => _servicio.AsociarParticipantesAsync(
            campania.Id,
            new SolicitudAsociarParticipantes(["u_en"], null),
            CancellationToken.None);

        await accion.Should().ThrowAsync<ErrorConflicto>()
            .WithMessage("IDIOMA_CAMPANIA_NO_HABILITADO*");
    }

    private Task<Campania> CrearCampaniaAsync()
        => _servicio.CrearCampaniaAsync(
            new SolicitudGuardarCampania(
                "Campania", "Descripcion", "Objetivo", "rub_1", null, "llm_1",
                ConfigMarkdown.Crear(TipoArtefactoMarkdown.Respuesta),
                ConfigConversacional.Crear(1, "Gracias."),
                LimitesSeguridad.Crear(1500, 10, 2)),
            CancellationToken.None);

    private static SolicitudGuardarMensajeInicial Mensaje()
        => new("saludo", "Hola {{nombre}}", 1, ["nombre"], EstadoRegistro.Activo, null);

    private static SolicitudGuardarPregunta Pregunta()
        => new("Pregunta", "Instruccion", "General", 1, EstadoRegistro.Activo, null, null, null, 1,
            LimitesSeguridad.ParaPregunta(1500, 2), ConfigMarkdown.Crear(TipoArtefactoMarkdown.Respuesta));

    private static Usuario UsuarioIngles()
        => Usuario.Crear("u_en", 1, "Ada", NumeroWhatsApp.FromNormalized("573001112233"), RolUsuario.Participante,
            EstadoRegistro.Activo, null, null, null, null, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, idioma: "en");
}
