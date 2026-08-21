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
/// I-08 v2 §7 — <see cref="ServicioCargaMasiva"/> sobre la plantilla oficial de GHT: obligatorios,
/// rechazos tipificados sin abortar el lote, identidad (<c>codigoUsuario</c> consecutivo), unicidad de
/// telefono entre activos, conflicto de titular con sus tres resoluciones, modos de carga, tag de
/// empresa derivada y auditoria sin PII.
/// </summary>
public sealed class ServicioCargaMasivaTests
{
    private const string Cabecera =
        "Empresa,ID Empresa,Sede,Nombre,Cargo,Email,Antigüedad en la empresa en años,Idioma,Telefono\n";

    private readonly RepositorioUsuariosMemoria _usuarios = new();
    private readonly IServicioGestionCampanias _campanias = Substitute.For<IServicioGestionCampanias>();
    private readonly IRepositorioLogSeguridad _log = Substitute.For<IRepositorioLogSeguridad>();
    private readonly IProveedorCorrelacion _correlacion = Substitute.For<IProveedorCorrelacion>();

    public ServicioCargaMasivaTests()
    {
        _correlacion.CorrelationIdActual.Returns("corr_test");
    }

    // ---------- Lectura y validacion ----------

    [Fact]
    public async Task Cargar_NFilasValidas_CreaNUsuarios()
    {
        var csv = Cabecera +
            "Flores El Aljibe,AL,AL,ANA PEREZ,Coordinadora,ana@ght.com,16.391666,es,573001112233\n" +
            "GHT,GHT,GHT,BETO GOMEZ,Gerente,beto@ght.com,3.5,en,573009998877\n";

        var reporte = await Cargar(csv);

        reporte.TotalFilas.Should().Be(2);
        reporte.Creados.Should().Be(2);
        reporte.Rechazados.Should().Be(0);
        reporte.Filas.Should().OnlyContain(f => f.Resultado == ResultadoCarga.Creado && f.UsuarioId != null);

        var ana = await BuscarPorNumero("573001112233");
        ana!.Nombre.Should().Be("ANA PEREZ");
        ana.Empresa.Should().Be("Flores El Aljibe");
        ana.EmpresaId.Should().Be("AL");
        ana.Sede.Should().Be("AL");
        ana.Cargo.Should().Be("Coordinadora");
        ana.Email.Should().Be("ana@ght.com");
        ana.AntiguedadAnios.Should().Be(16.391666m); // Sin redondear (I-08 §3, columna G).
        ana.Idioma.Should().Be("es");
    }

    [Fact]
    public async Task Cargar_MismoArchivoDosVeces_ActualizaSinDuplicar()
    {
        var csv = Cabecera + ",,,ANA PEREZ,,,,,573001112233\n";

        var primero = await Cargar(csv);
        var segundo = await Cargar(csv);

        primero.Creados.Should().Be(1);
        segundo.Creados.Should().Be(0);
        segundo.Actualizados.Should().Be(1);
        (await TodosLosUsuarios()).Should().HaveCount(1);
    }

    [Fact]
    public async Task Cargar_FilaSinNombreOSinTelefono_RechazaYProcesaElResto()
    {
        var csv = Cabecera +
            ",,,,,,,,573001112233\n" +          // Sin Nombre.
            ",,,SIN TELEFONO,,,,,\n" +          // Sin Telefono.
            ",,,ANA PEREZ,,,,,573009998877\n";

        var reporte = await Cargar(csv);

        reporte.Creados.Should().Be(1);
        reporte.Rechazados.Should().Be(2);
        reporte.Filas.Take(2).Should()
            .OnlyContain(f => f.Motivo == MotivoRechazoCarga.FilaIncompleta);
    }

    [Fact]
    public async Task Cargar_FilaSinEmailNiCargoNiSedeNiAntiguedad_SeCreaIgual()
    {
        var csv = Cabecera + ",,,ANA PEREZ,,,,,573001112233\n";

        var reporte = await Cargar(csv);

        reporte.Creados.Should().Be(1);
        var ana = await BuscarPorNumero("573001112233");
        ana!.Email.Should().BeNull();
        ana.Cargo.Should().BeNull();
        ana.Sede.Should().BeNull();
        ana.AntiguedadAnios.Should().BeNull();
        ana.Idioma.Should().Be("es"); // Default (I-08 §3, columna H).
    }

    [Fact]
    public async Task Cargar_NumeroInvalido_Rechaza()
    {
        var csv = Cabecera + ",,,MALA,,,,,no-es-numero\n";

        var reporte = await Cargar(csv);

        reporte.Filas.Single().Motivo.Should().Be(MotivoRechazoCarga.NumeroInvalido);
    }

    [Fact]
    public async Task Cargar_DuplicadoEnArchivo_PrimeroGana()
    {
        var csv = Cabecera +
            ",,,ANA PEREZ,,,,,573001112233\n" +
            ",,,ANA PEREZ,,,,,+57 300 111 2233\n"; // Mismo numero tras normalizar.

        var reporte = await Cargar(csv);

        reporte.Creados.Should().Be(1);
        reporte.Filas.Last().Motivo.Should().Be(MotivoRechazoCarga.DuplicadoEnArchivo);
        (await TodosLosUsuarios()).Should().HaveCount(1);
    }

    [Fact]
    public async Task Cargar_IdiomaFueraDelCatalogo_Rechaza()
    {
        var csv = Cabecera + ",,,ANA PEREZ,,,,fr,573001112233\n";

        var reporte = await Cargar(csv);

        reporte.Filas.Single().Motivo.Should().Be(MotivoRechazoCarga.IdiomaInvalido);
    }

    [Fact]
    public async Task Cargar_AntiguedadNoNumerica_Rechaza()
    {
        var csv = Cabecera + ",,,ANA PEREZ,,,no-es-numero,,573001112233\n";

        var reporte = await Cargar(csv);

        reporte.Filas.Single().Motivo.Should().Be(MotivoRechazoCarga.AntiguedadInvalida);
    }

    [Fact]
    public async Task Cargar_EmailYaUsadoPorOtroActivo_Rechaza()
    {
        var csv = Cabecera +
            ",,,ANA PEREZ,,ana@ght.com,,,573001112233\n" +
            ",,,BETO GOMEZ,,ana@ght.com,,,573009998877\n";

        var reporte = await Cargar(csv);

        reporte.Creados.Should().Be(1);
        reporte.Filas.Last().Motivo.Should().Be(MotivoRechazoCarga.EmailDuplicado);
    }

    [Fact]
    public async Task Cargar_EmailDeUnInactivo_NoBloquea()
    {
        await SembrarAsync("u_viejo", 1, "OTRO TITULAR", "573005554444", EstadoRegistro.Inactivo, "ana@ght.com");
        var csv = Cabecera + ",,,ANA PEREZ,,ana@ght.com,,,573001112233\n";

        var reporte = await Cargar(csv);

        reporte.Creados.Should().Be(1);
    }

    [Fact]
    public async Task Cargar_CabeceraDistinta_NoProcesaElLote()
    {
        var csv = "Nombre,WhatsApp,Area,Empresa,Tags\nAna,573001112233,Ops,GHT,\n";

        var act = () => Cargar(csv);

        await act.Should().ThrowAsync<ErrorValidacion>();
        (await TodosLosUsuarios()).Should().BeEmpty();
    }

    // ---------- Identidad y estado ----------

    [Fact]
    public async Task Cargar_AsignaCodigosConsecutivosYConsumeExactamenteN()
    {
        var csv = Cabecera +
            ",,,ANA PEREZ,,,,,573001112233\n" +
            ",,,MALA,,,,,no-es-numero\n" +          // No consume codigo.
            ",,,BETO GOMEZ,,,,,573009998877\n";

        var reporte = await Cargar(csv);

        reporte.Filas.Where(f => f.Resultado == ResultadoCarga.Creado)
            .Select(f => f.CodigoUsuario)
            .Should().Equal(1, 2);
        // El contador quedo justo despues de las 2 altas: la fila rechazada no gasto un valor.
        (await _usuarios.ReservarCodigosUsuarioAsync(1, CancellationToken.None)).Should().Be(3);
    }

    [Fact]
    public async Task Cargar_AlActualizar_ConservaCodigoUsuarioYUsuarioWhatsapp()
    {
        await SembrarAsync("u_1", 77, "ANA PEREZ", "573001112233", usuarioWhatsapp: "ana.perez");
        var csv = Cabecera + ",,,ANA PEREZ,Gerente,,,,573001112233\n";

        var reporte = await Cargar(csv);

        reporte.Actualizados.Should().Be(1);
        reporte.Filas.Single().CodigoUsuario.Should().Be(77);
        var ana = await BuscarPorNumero("573001112233");
        ana!.CodigoUsuario.Should().Be(77);
        ana.UsuarioWhatsapp.Should().Be("ana.perez");
        ana.Cargo.Should().Be("Gerente");
    }

    [Fact]
    public async Task Cargar_TelefonoDeUnInactivo_CreaUsuarioNuevoSinReactivarAlAnterior()
    {
        await SembrarAsync("u_viejo", 1, "TITULAR ANTERIOR", "573001112233", EstadoRegistro.Inactivo);
        var csv = Cabecera + ",,,ANA PEREZ,,,,,573001112233\n";

        var reporte = await Cargar(csv);

        reporte.Creados.Should().Be(1);
        var historico = await _usuarios.ListarUsuariosPorNumeroAsync(
            NumeroWhatsApp.FromNormalized("573001112233"),
            CancellationToken.None);
        historico.Should().HaveCount(2);
        historico.Single(u => u.Id == "u_viejo").Estado.Should().Be(EstadoRegistro.Inactivo);
    }

    [Fact]
    public async Task Cargar_CampoOpcionalVacio_NoBorraElValorPrevio()
    {
        await SembrarAsync("u_1", 5, "ANA PEREZ", "573001112233", cargo: "Coordinadora", email: "ana@ght.com");
        var csv = Cabecera + ",,,ANA PEREZ,,,,,573001112233\n";

        await Cargar(csv);

        var ana = await BuscarPorNumero("573001112233");
        ana!.Cargo.Should().Be("Coordinadora");
        ana.Email.Should().Be("ana@ght.com");
    }

    [Fact]
    public async Task Cargar_ConservaRolEstadoYTagsManuales()
    {
        await SembrarAsync("u_admin", 1, "ANA PEREZ", "573001112233", rol: RolUsuario.Admin, tags: ["t_manual"]);
        var csv = Cabecera + ",AL,,ANA PEREZ,,,,,573001112233\n";

        await Cargar(csv);

        var ana = await BuscarPorNumero("573001112233");
        ana!.Rol.Should().Be(RolUsuario.Admin); // No degrada un admin (I-08 §4.2).
        ana.Tags.Should().Contain("t_manual").And.Contain("t_emp_al");
    }

    // ---------- Conflicto de titular (§4.4) ----------

    [Fact]
    public async Task Cargar_TelefonoExistenteConNombreMuySimilar_ActualizaSinConflicto()
    {
        await SembrarAsync("u_1", 7, "ANA PEREZ", "573001112233");
        var csv = Cabecera + ",,,ANA PERES,,,,,573001112233\n"; // Typo.

        var reporte = await Cargar(csv);

        reporte.Actualizados.Should().Be(1);
        (await BuscarPorNumero("573001112233"))!.Id.Should().Be("u_1");
    }

    [Fact]
    public async Task Cargar_ActualizacionConservaNombreSaludoCorregidoManualmente()
    {
        await SembrarAsync(
            "u_1",
            7,
            "ARENAS CHAVES JUAN PABLO",
            "573001112233",
            nombreSaludo: "Juan");
        var csv = Cabecera + ",,,ARENAS CHAVES JUAN PABLO,Gerente,,,,573001112233\n";

        await Cargar(csv);

        var usuario = await BuscarPorNumero("573001112233");
        usuario!.Nombre.Should().Be("ARENAS CHAVES JUAN PABLO");
        usuario.NombreSaludo.Should().Be("Juan");
        usuario.Cargo.Should().Be("Gerente");
    }

    [Fact]
    public async Task Cargar_TelefonoExistenteConNombreDistinto_RechazaYNoEscribeNada()
    {
        await SembrarAsync("u_1", 7, "ANA PEREZ", "573001112233", cargo: "Coordinadora");
        var csv = Cabecera + ",,,CARLOS RODRIGUEZ,Gerente,,,,573001112233\n";

        var reporte = await Cargar(csv);

        var fila = reporte.Filas.Single();
        fila.Motivo.Should().Be(MotivoRechazoCarga.ConflictoTitular);
        fila.UsuarioIdAnterior.Should().Be("u_1");
        fila.CodigoUsuarioAnterior.Should().Be(7);
        fila.NombreActual.Should().Be("ANA PEREZ");
        fila.NombrePropuesto.Should().Be("CARLOS RODRIGUEZ");

        var ana = await BuscarPorNumero("573001112233");
        ana!.Nombre.Should().Be("ANA PEREZ");
        ana.Cargo.Should().Be("Coordinadora"); // Nada se escribio para esa fila.
    }

    [Fact]
    public async Task Cargar_ResueltoComoCorregirNombre_ConservaIdYCodigo()
    {
        await SembrarAsync("u_1", 7, "ANA PEREZ", "573001112233");
        var csv = Cabecera + ",,,CARLOS RODRIGUEZ,,,,,573001112233\n";

        var reporte = await Cargar(csv, resoluciones: [new ResolucionConflictoTitular(2, AccionConflictoTitular.CorregirNombre)]);

        reporte.Actualizados.Should().Be(1);
        var titular = await BuscarPorNumero("573001112233");
        titular!.Id.Should().Be("u_1");
        titular.CodigoUsuario.Should().Be(7);
        titular.Nombre.Should().Be("CARLOS RODRIGUEZ");
    }

    [Fact]
    public async Task Cargar_ResueltoComoReasignar_InactivaAlAnteriorYCreaAlNuevo()
    {
        await SembrarAsync("u_1", 7, "ANA PEREZ", "573001112233", rol: RolUsuario.Admin, tags: ["t_manual"]);
        var csv = Cabecera + ",,,CARLOS RODRIGUEZ,,,,,573001112233\n";

        var reporte = await Cargar(csv, resoluciones: [new ResolucionConflictoTitular(2, AccionConflictoTitular.Reasignar)]);

        reporte.Reasignados.Should().Be(1);
        var fila = reporte.Filas.Single();
        fila.Resultado.Should().Be(ResultadoCarga.Reasignado);
        fila.UsuarioIdAnterior.Should().Be("u_1");
        fila.CodigoUsuarioAnterior.Should().Be(7);

        var nuevo = await BuscarPorNumero("573001112233");
        nuevo!.Id.Should().NotBe("u_1");
        nuevo.CodigoUsuario.Should().NotBe(7);
        nuevo.Rol.Should().Be(RolUsuario.Participante); // No hereda rol...
        nuevo.Tags.Should().NotContain("t_manual");     // ...ni tags.

        var anterior = await _usuarios.ObtenerUsuarioPorIdAsync("u_1", CancellationToken.None);
        anterior!.Estado.Should().Be(EstadoRegistro.Inactivo);
        anterior.WhatsappNormalizado.Valor.Should().Be("573001112233"); // Conserva su numero.
    }

    [Fact]
    public async Task Cargar_ResueltoComoOmitir_DejaLaFilaRechazada()
    {
        await SembrarAsync("u_1", 7, "ANA PEREZ", "573001112233");
        var csv = Cabecera + ",,,CARLOS RODRIGUEZ,,,,,573001112233\n";

        var reporte = await Cargar(csv, resoluciones: [new ResolucionConflictoTitular(2, AccionConflictoTitular.Omitir)]);

        reporte.Rechazados.Should().Be(1);
        reporte.Filas.Single().Motivo.Should().Be(MotivoRechazoCarga.ConflictoTitular);
        (await BuscarPorNumero("573001112233"))!.Id.Should().Be("u_1");
    }

    [Fact]
    public async Task Cargar_Reasignacion_QuedaAuditadaSinPii()
    {
        await SembrarAsync("u_1", 7, "ANA PEREZ", "573001112233");
        var registros = new List<LogSeguridad>();
        await _log.RegistrarAsync(Arg.Do<LogSeguridad>(registros.Add), Arg.Any<CancellationToken>());
        var csv = Cabecera + ",,,CARLOS RODRIGUEZ,,,,,573001112233\n";

        await Cargar(csv, resoluciones: [new ResolucionConflictoTitular(2, AccionConflictoTitular.Reasignar)]);

        var auditoria = registros.Should().ContainSingle(l => l.Resultado == "reasignacion_numero").Subject;
        auditoria.UsuarioId.Should().Be("u_1");
        auditoria.Detalle.Should().Contain("codigoAnterior=7");
        auditoria.Detalle.Should().NotContain("573001112233");
        auditoria.Detalle.Should().NotContain("CARLOS");
        auditoria.Numero.Should().BeNull();
    }

    // ---------- Modos ----------

    [Fact]
    public async Task Cargar_SoloActualizar_ConTelefonoInexistente_RechazaSinCrear()
    {
        var csv = Cabecera + ",,,ANA PEREZ,,,,,573001112233\n";

        var reporte = await Cargar(csv, modo: ModoCargaMasiva.SoloActualizar);

        reporte.Creados.Should().Be(0);
        reporte.Filas.Single().Motivo.Should().Be(MotivoRechazoCarga.NoEncontrado);
        (await TodosLosUsuarios()).Should().BeEmpty();
    }

    [Fact]
    public async Task Cargar_SoloActualizar_ConTelefonoExistente_Actualiza()
    {
        await SembrarAsync("u_1", 7, "ANA PEREZ", "573001112233");
        var csv = Cabecera + ",,,ANA PEREZ,,,,en,573001112233\n";

        var reporte = await Cargar(csv, modo: ModoCargaMasiva.SoloActualizar);

        reporte.Actualizados.Should().Be(1);
        (await BuscarPorNumero("573001112233"))!.Idioma.Should().Be("en");
    }

    [Fact]
    public async Task Cargar_ModoDesconocido_EsErrorDeValidacion()
    {
        var act = () => Cargar(Cabecera, modo: "reemplazar_todo");

        await act.Should().ThrowAsync<ErrorValidacion>();
    }

    // ---------- Tags, campania y auditoria ----------

    [Fact]
    public async Task Cargar_AseguraLaTagDeEmpresaDerivada()
    {
        var csv = Cabecera + "Flores El Aljibe,AL,,ANA PEREZ,,,,,573001112233\n";

        await Cargar(csv);

        var tag = await _usuarios.ObtenerTagPorIdAsync("t_emp_al", CancellationToken.None);
        tag.Should().NotBeNull();
        tag!.TipoTag.Should().Be("empresa");
        (await BuscarPorNumero("573001112233"))!.Tags.Should().Contain("t_emp_al");
    }

    [Fact]
    public async Task Cargar_ConCampania_AsociaLosCreados()
    {
        var csv = Cabecera + ",,,ANA PEREZ,,,,,573001112233\n";
        IReadOnlyCollection<string>? asociados = null;
        _campanias
            .AsociarParticipantesAsync(
                "c_1",
                Arg.Do<SolicitudAsociarParticipantes>(s => asociados = s.UsuarioIds),
                Arg.Any<CancellationToken>())
            .Returns(new[] { ParticipanteFalso() });

        var reporte = await Cargar(csv, campaniaId: "c_1");

        reporte.Asociados.Should().Be(1);
        asociados.Should().ContainSingle().Which.Should().StartWith("u_");
    }

    [Fact]
    public async Task Cargar_SinCampania_NoAsocia()
    {
        await Cargar(Cabecera + ",,,ANA PEREZ,,,,,573001112233\n");

        await _campanias.DidNotReceiveWithAnyArgs()
            .AsociarParticipantesAsync(default!, default!, default);
    }

    [Fact]
    public async Task Cargar_Audita_ConConteosYSinPii()
    {
        var registros = new List<LogSeguridad>();
        await _log.RegistrarAsync(Arg.Do<LogSeguridad>(registros.Add), Arg.Any<CancellationToken>());

        await Cargar(Cabecera + ",,,ANA PEREZ,,ana@ght.com,,,573001112233\n");

        var auditoria = registros.Should().ContainSingle(l => l.Resultado == "carga_masiva").Subject;
        auditoria.TipoEvento.Should().Be(TipoEventoSeguridad.AccionAdministrativa);
        auditoria.Detalle.Should().Contain("creado=1").And.Contain("modo=upsert");
        auditoria.Detalle.Should().NotContain("573001112233");
        auditoria.Detalle.Should().NotContain("ana@ght.com");
        auditoria.Numero.Should().BeNull();
    }

    // ---------- Soporte ----------

    private Task<ReporteCargaMasiva> Cargar(
        string csv,
        string? campaniaId = null,
        string modo = ModoCargaMasiva.Upsert,
        IReadOnlyCollection<ResolucionConflictoTitular>? resoluciones = null)
        => CrearServicio().CargarAsync(
            "roster.csv",
            new MemoryStream(Encoding.UTF8.GetBytes(csv)),
            campaniaId,
            modo,
            resoluciones ?? [],
            CancellationToken.None);

    private ServicioCargaMasiva CrearServicio()
        => new(
            new ILectorArchivoParticipantes[] { new LectorCsvParticipantes() },
            _usuarios,
            new NormalizadorNumero(),
            _campanias,
            _log,
            _correlacion,
            TimeProvider.System);

    private async Task SembrarAsync(
        string id,
        int codigoUsuario,
        string nombre,
        string numero,
        EstadoRegistro estado = EstadoRegistro.Activo,
        string? email = null,
        string? cargo = null,
        string? usuarioWhatsapp = null,
        RolUsuario rol = RolUsuario.Participante,
        IEnumerable<string>? tags = null,
        string? nombreSaludo = null)
    {
        var usuario = Usuario.Crear(
            id,
            codigoUsuario,
            nombre,
            NumeroWhatsApp.FromNormalized(numero),
            rol,
            estado,
            area: null,
            empresa: null,
            tags,
            propiedadesDinamicas: null,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch,
            usuarioWhatsapp,
            empresaId: null,
            sede: null,
            cargo,
            email,
            nombreSaludo: nombreSaludo);

        await _usuarios.GuardarUsuarioAsync(usuario, CancellationToken.None);
    }

    private Task<Usuario?> BuscarPorNumero(string numero)
        => _usuarios.ObtenerUsuarioPorNumeroAsync(
            NumeroWhatsApp.FromNormalized(numero),
            CancellationToken.None);

    private Task<IReadOnlyCollection<Usuario>> TodosLosUsuarios()
        => _usuarios.BuscarUsuariosAsync(
            new FiltroUsuarios(null, null, null, null, [], null),
            CancellationToken.None);

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
}
