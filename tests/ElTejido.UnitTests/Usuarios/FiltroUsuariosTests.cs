using ElTejido.Application.Usuarios;
using ElTejido.Domain.Common;
using ElTejido.Domain.Usuarios;
using FluentAssertions;

namespace ElTejido.UnitTests.Usuarios;

public sealed class FiltroUsuariosTests
{
    [Fact]
    public void Constructor_NormalizesFiltersForUsersCatalogQueries()
    {
        var filtro = new FiltroUsuarios(
            RolUsuario.Participante,
            EstadoRegistro.Activo,
            " Operaciones ",
            " GHT ",
            ["t_area_oper", " ", "t_emp_ght", "t_area_oper"],
            " Ana ");

        filtro.Rol.Should().Be(RolUsuario.Participante);
        filtro.Estado.Should().Be(EstadoRegistro.Activo);
        filtro.Area.Should().Be("Operaciones");
        filtro.Empresa.Should().Be("GHT");
        filtro.Tags.Should().Equal("t_area_oper", "t_emp_ght");
        filtro.Busqueda.Should().Be("Ana");
    }

    [Fact]
    public void Constructor_TreatsBlankOptionalFiltersAsUnfiltered()
    {
        var filtro = new FiltroUsuarios(area: " ", empresa: "", busqueda: null, tags: null);

        filtro.Area.Should().BeNull();
        filtro.Empresa.Should().BeNull();
        filtro.Busqueda.Should().BeNull();
        filtro.Tags.Should().BeEmpty();
    }
}
