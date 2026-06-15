using Azure;
using ElTejido.Application.Diagnostico;
using ElTejido.Application.Seguridad;
using ElTejido.Infrastructure.Diagnostico;
using FluentAssertions;
using NSubstitute;

namespace ElTejido.UnitTests.Diagnostico;

/// <summary>
/// Verifica que la comprobacion de secretos clasifique correctamente presencia, ausencia y errores
/// de acceso, y que nunca exponga el valor del secreto en el detalle.
/// </summary>
public sealed class ComprobacionSecretosTests
{
    [Fact]
    public async Task SecretoPresente_ReportaOkSinValor()
    {
        var secretos = Substitute.For<ISecretProvider>();
        secretos.ObtenerSecretoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("valor-super-secreto");

        var resultados = await new ComprobacionSecretos(secretos).ComprobarAsync(CancellationToken.None);

        resultados.Should().OnlyContain(r => r.Estado == EstadoPreparacion.Ok);
        resultados.Should().Contain(r => r.Componente == $"secreto:{NombresSecretos.WaToken}");
        resultados.Should().OnlyContain(r => !r.Detalle.Contains("valor-super-secreto"));
    }

    [Fact]
    public async Task SecretoNoEncontrado_ReportaFaltante()
    {
        var secretos = Substitute.For<ISecretProvider>();
        secretos.ObtenerSecretoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<string>(_ => throw new KeyNotFoundException("no esta"));

        var resultados = await new ComprobacionSecretos(secretos).ComprobarAsync(CancellationToken.None);

        resultados.Should().OnlyContain(r => r.Estado == EstadoPreparacion.Faltante);
    }

    [Fact]
    public async Task AccesoDenegadoKeyVault_ReportaError()
    {
        var secretos = Substitute.For<ISecretProvider>();
        secretos.ObtenerSecretoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<string>(_ => throw new RequestFailedException(403, "sin permiso"));

        var resultados = await new ComprobacionSecretos(secretos).ComprobarAsync(CancellationToken.None);

        resultados.Should().OnlyContain(r => r.Estado == EstadoPreparacion.Error);
    }

    [Fact]
    public async Task SecretoInexistenteEnKeyVault_ReportaFaltante()
    {
        var secretos = Substitute.For<ISecretProvider>();
        secretos.ObtenerSecretoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<string>(_ => throw new RequestFailedException(404, "no existe"));

        var resultados = await new ComprobacionSecretos(secretos).ComprobarAsync(CancellationToken.None);

        resultados.Should().OnlyContain(r => r.Estado == EstadoPreparacion.Faltante);
    }
}
