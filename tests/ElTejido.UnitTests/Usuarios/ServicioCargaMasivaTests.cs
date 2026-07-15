using System.Text;
using ElTejido.Application.Common;
using ElTejido.Application.Configuracion;
using ElTejido.Application.Seguridad;
using ElTejido.Application.Usuarios;
using ElTejido.Application.Usuarios.CargaMasiva;
using ElTejido.Domain.Common;
using ElTejido.Domain.Identidad;
using ElTejido.Domain.Participantes;
using ElTejido.Domain.Seguridad;
using ElTejido.Domain.Usuarios;
using ElTejido.Infrastructure.Persistencia.Memoria;
using FluentAssertions;
using NSubstitute;

namespace ElTejido.UnitTests.Usuarios;

/// <summary>
/// I-08 — casos de uso de <see cref="ServicioCargaMasiva"/>: upsert por numero, idempotencia,
/// rechazos por fila (numero invalido, duplicado en archivo, fila incompleta) sin abortar el lote,
/// creacion de tags faltantes, asociacion opcional a campania y auditoria sin PII.
/// </summary>
public sealed class ServicioCargaMasivaTests
{
    private readonly RepositorioUsuariosMemoria _usuarios = new();
    private readonly IServicioGestionCampanias _campanias = Substitute.For<IServicioGestionCampanias>();
    private readonly IRepositorioLogSeguridad _log = Substitute.For<IRepositorioLogSeguridad>();
    private readonly IProveedorCorrelacion _correlacion = Substitute.For<IProveedorCorrelacion>();

    public ServicioCargaMasivaTests()
    {
        _correlacion.CorrelationIdActual.Returns("corr_test");
    }

    private ServicioCargaMasiva CrearServicio()
        => new(
            new ILectorArchivoParticipantes[] { new LectorCsvParticipantes() },
            _usuarios,
            new NormalizadorNumero(),
            _campanias,
            _log,
            _correlacion,
            TimeProvider.System);

    [Fact]
    public async Task Cargar_NFilasValidas_CreaNUsuarios()
    {
        var csv =
            "Nombre,WhatsApp,Area,Empresa,Tags\n" +
            "Ana,573001112233,Ops,GHT,\n" +
            "Beto,573009998877,Ventas,GHT,\n";

        var reporte = await CrearServicio().CargarAsync("roster.csv", Contenido(csv), campaniaId: null, CancellationToken.None);

        reporte.TotalFilas.Should().Be(2);
        reporte.Creados.Should().Be(2);
        reporte.Actualizados.Should().Be(0);
        reporte.Rechazados.Should().Be(0);
        reporte.Filas.Should().OnlyContain(f => f.Resultado == ResultadoCarga.Creado && f.UsuarioId != null);
        (await _usuarios.BuscarUsuariosAsync(new FiltroUsuarios(null, null, null, null, [], null), CancellationToken.None))
            .Should().HaveCount(2);
    }

    [Fact]
    public async Task Cargar_MismoArchivoDosVeces_ActualizaSinDuplicar()
    {
        var csv =
            "Nombre,WhatsApp,Area,Empresa,Tags\n" +
            "Ana,573001112233,Ops,GHT,\n";
        var servicio = CrearServicio();

        var primero = await servicio.CargarAsync("roster.csv", Contenido(csv), null, CancellationToken.None);
        var segundo = await servicio.CargarAsync("roster.csv", Contenido(csv), null, CancellationToken.None);

        primero.Creados.Should().Be(1);
        segundo.Creados.Should().Be(0);
        segundo.Actualizados.Should().Be(1);
        (await _usuarios.BuscarUsuariosAsync(new FiltroUsuarios(null, null, null, null, [], null), CancellationToken.None))
            .Should().HaveCount(1);
    }

    [Fact]
    public async Task Cargar_NumeroInvalido_RechazaYProcesaElResto()
    {
        var csv =
            "Nombre,WhatsApp,Area,Empresa,Tags\n" +
            "Mala,no-es-numero,Ops,GHT,\n" +
            "Buena,573001112233,Ops,GHT,\n";

        var reporte = await CrearServicio().CargarAsync("roster.csv", Contenido(csv), null, CancellationToken.None);

        reporte.Creados.Should().Be(1);
        reporte.Rechazados.Should().Be(1);
        reporte.Filas.Should().ContainSingle(f => f.Resultado == ResultadoCarga.Rechazado)
            .Which.Motivo.Should().Be(MotivoRechazoCarga.NumeroInvalido);
    }

    [Fact]
    public async Task Cargar_DuplicadoEnArchivo_PrimeroGana()
    {
        var csv =
            "Nombre,WhatsApp,Area,Empresa,Tags\n" +
            "Ana,573001112233,Ops,GHT,\n" +
            "Ana Bis,+57 300 111 2233,Ops,GHT,\n"; // Mismo numero tras normalizar.

        var reporte = await CrearServicio().CargarAsync("roster.csv", Contenido(csv), null, CancellationToken.None);

        reporte.Creados.Should().Be(1);
        reporte.Rechazados.Should().Be(1);
        reporte.Filas.Last().Motivo.Should().Be(MotivoRechazoCarga.DuplicadoEnArchivo);
        (await _usuarios.BuscarUsuariosAsync(new FiltroUsuarios(null, null, null, null, [], null), CancellationToken.None))
            .Should().HaveCount(1);
    }

    [Fact]
    public async Task Cargar_FilaIncompleta_Rechaza()
    {
        var csv =
            "Nombre,WhatsApp,Area,Empresa,Tags\n" +
            "SinArea,573001112233,,GHT,\n";

        var reporte = await CrearServicio().CargarAsync("roster.csv", Contenido(csv), null, CancellationToken.None);

        reporte.Rechazados.Should().Be(1);
        reporte.Filas.Single().Motivo.Should().Be(MotivoRechazoCarga.FilaIncompleta);
    }

    [Fact]
    public async Task Cargar_CreaTagsFaltantes()
    {
        var csv =
            "Nombre,WhatsApp,Area,Empresa,Tags\n" +
            "Ana,573001112233,Ops,GHT,t_nueva\n";

        await CrearServicio().CargarAsync("roster.csv", Contenido(csv), null, CancellationToken.None);

        var tag = await _usuarios.ObtenerTagPorIdAsync("t_nueva", CancellationToken.None);
        tag.Should().NotBeNull();
        tag!.TipoTag.Should().Be("importado");
    }

    [Fact]
    public async Task Cargar_ConCampania_AsociaLosCreados()
    {
        var csv =
            "Nombre,WhatsApp,Area,Empresa,Tags\n" +
            "Ana,573001112233,Ops,GHT,\n";
        IReadOnlyCollection<string>? asociados = null;
        _campanias
            .AsociarParticipantesAsync("c_1", Arg.Do<SolicitudAsociarParticipantes>(s => asociados = s.UsuarioIds), Arg.Any<CancellationToken>())
            .Returns(new[] { ParticipanteFalso() });

        var reporte = await CrearServicio().CargarAsync("roster.csv", Contenido(csv), "c_1", CancellationToken.None);

        reporte.Asociados.Should().Be(1);
        asociados.Should().ContainSingle().Which.Should().StartWith("u_");
    }

    [Fact]
    public async Task Cargar_SinCampania_NoAsocia()
    {
        var csv =
            "Nombre,WhatsApp,Area,Empresa,Tags\n" +
            "Ana,573001112233,Ops,GHT,\n";

        await CrearServicio().CargarAsync("roster.csv", Contenido(csv), null, CancellationToken.None);

        await _campanias.DidNotReceiveWithAnyArgs()
            .AsociarParticipantesAsync(default!, default!, default);
    }

    [Fact]
    public async Task Cargar_Audita_ConConteosYSinPii()
    {
        var csv =
            "Nombre,WhatsApp,Area,Empresa,Tags\n" +
            "Ana,573001112233,Ops,GHT,\n";
        LogSeguridad? registrado = null;
        await _log.RegistrarAsync(Arg.Do<LogSeguridad>(l => registrado = l), Arg.Any<CancellationToken>());

        await CrearServicio().CargarAsync("roster.csv", Contenido(csv), null, CancellationToken.None);

        registrado.Should().NotBeNull();
        registrado!.TipoEvento.Should().Be(TipoEventoSeguridad.AccionAdministrativa);
        registrado.Resultado.Should().Be("carga_masiva");
        registrado.Detalle.Should().Contain("creado=1");
        registrado.Detalle.Should().NotContain("573001112233"); // Sin PII.
        registrado.Numero.Should().BeNull();
    }

    private static ParticipanteCampania ParticipanteFalso()
        => ParticipanteCampania.Crear(
            "pc_1",
            "c_1",
            "u_1",
            NumeroWhatsApp.FromNormalized("573001112233"),
            EstadoRegistro.Activo,
            EstadoEnvio.Pendiente,
            EstadoRespuestaParticipante.SinRespuesta,
            DateTimeOffset.UnixEpoch,
            null,
            null);

    private static MemoryStream Contenido(string texto)
        => new(Encoding.UTF8.GetBytes(texto));
}
