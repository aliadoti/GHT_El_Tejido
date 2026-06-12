using ElTejido.Domain.Common;
using ElTejido.Domain.Usuarios;
using FluentAssertions;

namespace ElTejido.UnitTests.Usuarios;

public sealed class TagTests
{
    [Fact]
    public void Crear_AllowsParameterizedTagTypes()
    {
        var creadoEn = new DateTimeOffset(2026, 6, 12, 18, 0, 0, TimeSpan.FromHours(-5));

        var tag = Tag.Crear("t_sede_bog", "Bogota", "sede", "Equipo Bogota", EstadoRegistro.Activo, creadoEn);

        tag.Id.Should().Be("t_sede_bog");
        tag.Nombre.Should().Be("Bogota");
        tag.TipoTag.Should().Be("sede");
        tag.Descripcion.Should().Be("Equipo Bogota");
        tag.Estado.Should().Be(EstadoRegistro.Activo);
        tag.CreadoEn.Offset.Should().Be(TimeSpan.Zero);
    }

    [Theory]
    [InlineData("", "Nombre", "area")]
    [InlineData("t_area", "", "area")]
    [InlineData("t_area", "Operaciones", "")]
    public void Crear_RejectsRequiredFields(string id, string nombre, string tipoTag)
    {
        var act = () => Tag.Crear(id, nombre, tipoTag, null, EstadoRegistro.Activo, DateTimeOffset.UtcNow);

        act.Should()
            .Throw<DomainValidationException>()
            .Where(exception => exception.Code == "CAMPO_OBLIGATORIO");
    }
}

