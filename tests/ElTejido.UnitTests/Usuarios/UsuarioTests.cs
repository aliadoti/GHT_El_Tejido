using ElTejido.Domain.Common;
using ElTejido.Domain.Identidad;
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
        var usuario = Usuario.Crear(
            "u_admin",
            "Admin",
            Numero,
            rol,
            EstadoRegistro.Activo,
            "TI",
            "GHT",
            [],
            null,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        usuario.EsAdministrativo.Should().BeTrue();
    }

    [Fact]
    public void EsAdministrativo_IsFalseForParticipantRole()
    {
        var usuario = Usuario.Crear(
            "u_part",
            "Participante",
            Numero,
            RolUsuario.Participante,
            EstadoRegistro.Activo,
            "Operaciones",
            "GHT",
            [],
            null,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        usuario.EsAdministrativo.Should().BeFalse();
    }

    [Fact]
    public void Crear_RejectsUpdatedDateBeforeCreatedDate()
    {
        var creadoEn = DateTimeOffset.UtcNow;
        var actualizadoEn = creadoEn.AddTicks(-1);

        var act = () => Usuario.Crear(
            "u_1",
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
}

