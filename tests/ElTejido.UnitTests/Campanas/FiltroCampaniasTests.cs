using ElTejido.Application.Campanas;
using ElTejido.Domain.Campanas;
using FluentAssertions;

namespace ElTejido.UnitTests.Campanas;

public sealed class FiltroCampaniasTests
{
    [Fact]
    public void Constructor_NormalizesCampaignListFilters()
    {
        var filtro = new FiltroCampanias(EstadoCampania.Activa, " Convencion ");

        filtro.Estado.Should().Be(EstadoCampania.Activa);
        filtro.Busqueda.Should().Be("Convencion");
    }

    [Fact]
    public void Constructor_TreatsBlankSearchAsUnfiltered()
    {
        var filtro = new FiltroCampanias(busqueda: " ");

        filtro.Estado.Should().BeNull();
        filtro.Busqueda.Should().BeNull();
    }
}
