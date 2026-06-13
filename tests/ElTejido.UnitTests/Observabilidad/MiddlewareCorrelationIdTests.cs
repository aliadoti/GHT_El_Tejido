using ElTejido.Api.Observabilidad;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace ElTejido.UnitTests.Observabilidad;

public sealed class MiddlewareCorrelationIdTests
{
    [Fact]
    public async Task InvokeAsync_SinCabecera_GeneraCorrelationIdConPrefijo()
    {
        var context = new DefaultHttpContext();
        var middleware = new MiddlewareCorrelationId(_ => Task.CompletedTask, NullLogger<MiddlewareCorrelationId>.Instance);

        await middleware.InvokeAsync(context);

        var generado = context.Items[CorrelacionConstantes.ClaveItems].Should().BeOfType<string>().Subject;
        generado.Should().StartWith(CorrelacionConstantes.Prefijo);
        context.Response.Headers[CorrelacionConstantes.Header].ToString().Should().Be(generado);
    }

    [Fact]
    public async Task InvokeAsync_ConCabeceraEntrante_ReutilizaElValor()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelacionConstantes.Header] = "corr_entrante123";
        var middleware = new MiddlewareCorrelationId(_ => Task.CompletedTask, NullLogger<MiddlewareCorrelationId>.Instance);

        await middleware.InvokeAsync(context);

        context.Items[CorrelacionConstantes.ClaveItems].Should().Be("corr_entrante123");
        context.Response.Headers[CorrelacionConstantes.Header].ToString().Should().Be("corr_entrante123");
    }
}
