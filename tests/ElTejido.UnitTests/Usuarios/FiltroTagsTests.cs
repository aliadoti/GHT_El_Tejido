using ElTejido.Application.Usuarios;
using ElTejido.Domain.Common;
using FluentAssertions;

namespace ElTejido.UnitTests.Usuarios;

public sealed class FiltroTagsTests
{
    [Fact]
    public void Constructor_NormalizesOptionalTagType()
    {
        var filtro = new FiltroTags(" area ", EstadoRegistro.Activo);

        filtro.TipoTag.Should().Be("area");
        filtro.Estado.Should().Be(EstadoRegistro.Activo);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_TreatsBlankTagTypeAsUnfiltered(string? tipoTag)
    {
        var filtro = new FiltroTags(tipoTag);

        filtro.TipoTag.Should().BeNull();
    }
}
