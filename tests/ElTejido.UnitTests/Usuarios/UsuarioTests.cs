using ElTejido.Domain.Common;
using ElTejido.Domain.Identidad;
using ElTejido.Domain.Localizacion;
using ElTejido.Domain.Usuarios;
using FluentAssertions;

namespace ElTejido.UnitTests.Usuarios;

public sealed class UsuarioTests
{
    private static readonly NumeroWhatsApp Numero = NumeroWhatsApp.FromNormalized("573001112233");

    [Fact]
    public void Crear_PreservesCoreUserFieldsAndNormalizesCollections()
    {
        var creadoEn = new DateTimeOffset(2026, 6, 12, 18, 0, 0, TimeSpan.FromHours(-5));
        var actualizadoEn = creadoEn.AddMinutes(15);
        var propiedades = new Dictionary<string, object?>
        {
            [" cargo "] = "Coordinadora",
            [""] = "omitido",
        };

        var usuario = Usuario.Crear(
            " u_1 ",
            42,
            " Ana Perez ",
            Numero,
            RolUsuario.Participante,
            EstadoRegistro.Activo,
            " Operaciones ",
            " GHT ",
            ["t_area_oper", "t_emp_ght", "t_area_oper", " "],
            propiedades,
            creadoEn,
            actualizadoEn);

        usuario.Id.Should().Be("u_1");
        usuario.CodigoUsuario.Should().Be(42);
        usuario.Nombre.Should().Be("Ana Perez");
        usuario.WhatsappNormalizado.Should().Be(Numero);
        usuario.Area.Should().Be("Operaciones");
        usuario.Empresa.Should().Be("GHT");
        usuario.Tags.Should().Equal("t_area_oper", "t_emp_ght");
        usuario.PropiedadesDinamicas.Should().ContainKey("cargo");
        usuario.PropiedadesDinamicas.Should().NotContainKey("");
        usuario.CreadoEn.Offset.Should().Be(TimeSpan.Zero);
        usuario.ActualizadoEn.Offset.Should().Be(TimeSpan.Zero);
    }

    [Theory]
    [InlineData(RolUsuario.Admin)]
    [InlineData(RolUsuario.Visor)]
    public void EsAdministrativo_IsTrueForPortalRoles(RolUsuario rol)
    {
        var usuario = Crear(rol: rol);

        usuario.EsAdministrativo.Should().BeTrue();
    }

    [Fact]
    public void EsAdministrativo_IsFalseForParticipantRole()
    {
        var usuario = Crear(rol: RolUsuario.Participante);

        usuario.EsAdministrativo.Should().BeFalse();
    }

    [Fact]
    public void Crear_RejectsUpdatedDateBeforeCreatedDate()
    {
        var creadoEn = DateTimeOffset.UtcNow;
        var actualizadoEn = creadoEn.AddTicks(-1);

        var act = () => Usuario.Crear(
            "u_1",
            1,
            "Ana",
            Numero,
            RolUsuario.Participante,
            EstadoRegistro.Activo,
            "Operaciones",
            "GHT",
            [],
            null,
            creadoEn,
            actualizadoEn);

        act.Should()
            .Throw<DomainValidationException>()
            .Where(exception => exception.Code == "FECHA_ACTUALIZACION_INVALIDA");
    }

    // --- I-08 v2: maestro alineado a la plantilla oficial de GHT (03 §3.1) ---

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Crear_RejectsCodigoUsuarioNotAssignedBySequence(int codigoUsuario)
    {
        var act = () => Crear(codigoUsuario: codigoUsuario);

        act.Should()
            .Throw<DomainValidationException>()
            .Where(exception => exception.Code == "CODIGO_USUARIO_INVALIDO");
    }

    [Fact]
    public void CodigoUsuarioLegible_UsesPaddedFormat()
    {
        Crear(codigoUsuario: 42).CodigoUsuarioLegible.Should().Be("U-000042");
        Usuario.FormatearCodigo(1).Should().Be("U-000001");
    }

    [Fact]
    public void Crear_AcceptsUserWithoutAreaEmpresaOrEmail()
    {
        // La plantilla oficial no trae Area y puede traer Empresa/Email vacios (I-08 §3).
        var usuario = Crear(area: "  ", empresa: null, email: "   ");

        usuario.Area.Should().BeNull();
        usuario.Empresa.Should().BeNull();
        usuario.Email.Should().BeNull();
    }

    [Fact]
    public void Crear_NormalizesNombreEmailAndOptionalProfileFields()
    {
        var usuario = Crear(
            nombre: "  JUAN   CARLOS   PEREZ ",
            email: "  Ana.Perez@GHT.com  ",
            empresaId: " AL ",
            sede: " FF - ADM ",
            cargo: " Gerente ",
            usuarioWhatsapp: " ana.perez ");

        // Se colapsan espacios pero no se re-capitaliza: el archivo llega en mayusculas (I-08 §3, col. D).
        usuario.Nombre.Should().Be("JUAN CARLOS PEREZ");
        usuario.Email.Should().Be("ana.perez@ght.com");
        usuario.EmpresaId.Should().Be("AL");
        usuario.Sede.Should().Be("FF - ADM");
        usuario.Cargo.Should().Be("Gerente");
        usuario.UsuarioWhatsapp.Should().Be("ana.perez");
    }

    [Theory]
    [InlineData("ARENAS CHAVES JUAN PABLO", "Juan Pablo")]
    [InlineData("PEREZ GOMEZ ANA MARÍA", "Ana María")]
    [InlineData("ANA", "Ana")]
    [InlineData("Ana Pérez", "Ana Pérez")]
    public void Crear_CalculaNombreSaludoSinCambiarNombreCompleto(string nombre, string esperado)
    {
        var usuario = Crear(nombre: nombre);

        usuario.Nombre.Should().Be(nombre);
        usuario.NombreSaludo.Should().Be(esperado);
    }

    [Fact]
    public void Crear_ConservaNombreSaludoCorregidoManualmente()
    {
        var usuario = Usuario.Crear(
            "u_1",
            1,
            "DE LA CRUZ PEREZ ANA",
            Numero,
            RolUsuario.Participante,
            EstadoRegistro.Activo,
            null,
            null,
            [],
            null,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch,
            nombreSaludo: "Ana María");

        usuario.Nombre.Should().Be("DE LA CRUZ PEREZ ANA");
        usuario.NombreSaludo.Should().Be("Ana María");
    }

    [Fact]
    public void Crear_KeepsAntiguedadDecimalWithoutRounding()
    {
        Crear(antiguedadAnios: 16.391666m).AntiguedadAnios.Should().Be(16.391666m);
        Crear().AntiguedadAnios.Should().BeNull();
    }

    [Theory]
    [InlineData(null, "es")]
    [InlineData("", "es")]
    [InlineData(" ES ", "es")]
    [InlineData("en", "en")]
    public void Crear_AppliesSpanishAsDefaultLanguage(string? idioma, string esperado)
    {
        var usuario = Crear(idioma: idioma);

        usuario.Idioma.Should().Be(esperado);
        usuario.IdiomaInterno.Should().Be(IdiomaConversacion.Crear(esperado));
    }

    [Fact]
    public void Crear_RejectsUnsupportedLanguage()
    {
        var act = () => Crear(idioma: "fr");

        act.Should()
            .Throw<DomainValidationException>()
            .Where(exception => exception.Code == "IDIOMA_NO_SOPORTADO");

        Usuario.EsIdiomaSoportado("fr").Should().BeFalse();
        Usuario.EsIdiomaSoportado(" EN ").Should().BeTrue();
    }

    private static Usuario Crear(
        int codigoUsuario = 1,
        string nombre = "Ana",
        RolUsuario rol = RolUsuario.Participante,
        EstadoRegistro estado = EstadoRegistro.Activo,
        string? area = "Operaciones",
        string? empresa = "GHT",
        string? usuarioWhatsapp = null,
        string? empresaId = null,
        string? sede = null,
        string? cargo = null,
        string? email = null,
        decimal? antiguedadAnios = null,
        string? idioma = null)
        => Usuario.Crear(
            "u_1",
            codigoUsuario,
            nombre,
            Numero,
            rol,
            estado,
            area,
            empresa,
            [],
            null,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch,
            usuarioWhatsapp,
            empresaId,
            sede,
            cargo,
            email,
            antiguedadAnios,
            idioma);
}
