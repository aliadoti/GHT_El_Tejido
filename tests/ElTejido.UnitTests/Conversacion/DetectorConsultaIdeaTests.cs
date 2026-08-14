using ElTejido.Application.Conversacion;
using FluentAssertions;

namespace ElTejido.UnitTests.Conversacion;

public sealed class DetectorConsultaIdeaTests
{
    private readonly DetectorConsultaIdea _detector =
        new(DetectorConsultaIdea.FrasesPorDefecto, maxCaracteres: 220);

    [Theory]
    [InlineData("Dime cómo va escrita mi idea hasta ahora")]
    [InlineData("MUÉSTRAME MI IDEA!")]
    public void ConsultaPura_SeReconoceSinImportarAcentosNiPuntuacion(string texto)
        => _detector.Coincide(texto).Should().BeTrue();

    [Fact]
    public void AporteConConsultaNoSeConfundeConUnaConsultaPura()
        => _detector.Coincide("Muéstrame mi idea y agrega que tendremos un piloto en septiembre.")
            .Should().BeFalse();

    [Fact]
    public void ConsultaQueSuperaElLimiteNoSeReconoce()
        => new DetectorConsultaIdea(DetectorConsultaIdea.FrasesPorDefecto, maxCaracteres: 4)
            .Coincide("dime mi idea")
            .Should().BeFalse();
}
