using ElTejido.Api.Errores;
using ElTejido.Application.Common;
using ElTejido.Domain.Common;
using FluentAssertions;

namespace ElTejido.UnitTests.Errores;

public sealed class MapeadorErroresTests
{
    public static TheoryData<ExcepcionAplicacion, int, string> ExcepcionesTipadas() => new()
    {
        { new ErrorValidacion("x"), 400, "VALIDATION_ERROR" },
        { new ErrorNoAutenticado("x"), 401, "UNAUTHENTICATED" },
        { new ErrorProhibido("x"), 403, "FORBIDDEN" },
        { new ErrorNoEncontrado("x"), 404, "NOT_FOUND" },
        { new ErrorConflicto("x"), 409, "CONFLICT" },
        { new ErrorReglaNegocio("x"), 422, "BUSINESS_RULE" },
        { new ErrorLimiteTasa("x"), 429, "RATE_LIMITED" },
        { new ErrorUpstream("x"), 502, "UPSTREAM_ERROR" },
    };

    [Theory]
    [MemberData(nameof(ExcepcionesTipadas))]
    public void Map_ExcepcionTipada_DevuelveEstadoYCodigo(
        ExcepcionAplicacion excepcion,
        int estadoEsperado,
        string codigoEsperado)
    {
        var resultado = MapeadorErrores.Map(excepcion);

        resultado.Status.Should().Be(estadoEsperado);
        resultado.Code.Should().Be(codigoEsperado);
        resultado.Message.Should().Be("x");
    }

    [Fact]
    public void Map_ErrorValidacion_PropagaDetalles()
    {
        var excepcion = new ErrorValidacion(
            "El numero no tiene formato E.164.",
            new[] { new DetalleError("numero", "formato") });

        var resultado = MapeadorErrores.Map(excepcion);

        resultado.Details.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new CampoErrorRespuesta("numero", "formato"));
    }

    [Fact]
    public void Map_DomainValidationException_MapeaA400ConCodigoDeDominioComoIssue()
    {
        var excepcion = new DomainValidationException("CAMPO_OBLIGATORIO", "El campo numero es obligatorio.");

        var resultado = MapeadorErrores.Map(excepcion);

        resultado.Status.Should().Be(400);
        resultado.Code.Should().Be("VALIDATION_ERROR");
        resultado.Message.Should().Be("El campo numero es obligatorio.");
        resultado.Details.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new CampoErrorRespuesta(null, "CAMPO_OBLIGATORIO"));
    }

    [Fact]
    public void Map_ExcepcionNoControlada_MapeaA500SinFiltrarMensaje()
    {
        var excepcion = new InvalidOperationException("detalle interno sensible");

        var resultado = MapeadorErrores.Map(excepcion);

        resultado.Status.Should().Be(500);
        resultado.Code.Should().Be("INTERNAL_ERROR");
        resultado.Message.Should().NotContain("sensible");
        resultado.Details.Should().BeEmpty();
    }
}
