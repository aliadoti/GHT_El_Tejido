using ElTejido.Application.Seguridad;
using ElTejido.Infrastructure.Seguridad;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace ElTejido.UnitTests.Seguridad;

public sealed class SecretProviderConCacheTests
{
    [Fact]
    public async Task ObtenerSecretoAsync_DentroDeLaVentana_CacheaYNoVuelveAConsultarElInterno()
    {
        var interno = Substitute.For<ISecretProvider>();
        interno.ObtenerSecretoAsync("llm-key", Arg.Any<CancellationToken>()).Returns("valor");
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var decorador = CrearDecorador(interno, cache, duracionMinutos: 5);

        var primero = await decorador.ObtenerSecretoAsync("llm-key", CancellationToken.None);
        var segundo = await decorador.ObtenerSecretoAsync("llm-key", CancellationToken.None);

        primero.Should().Be("valor");
        segundo.Should().Be("valor");
        await interno.Received(1).ObtenerSecretoAsync("llm-key", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ObtenerSecretoAsync_NombresDistintos_ConsultaElInternoPorCadaNombre()
    {
        var interno = Substitute.For<ISecretProvider>();
        interno.ObtenerSecretoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("v");
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var decorador = CrearDecorador(interno, cache, duracionMinutos: 5);

        await decorador.ObtenerSecretoAsync("llm-key", CancellationToken.None);
        await decorador.ObtenerSecretoAsync("jwt-sign", CancellationToken.None);

        await interno.Received(1).ObtenerSecretoAsync("llm-key", Arg.Any<CancellationToken>());
        await interno.Received(1).ObtenerSecretoAsync("jwt-sign", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ObtenerSecretoAsync_TrasExpirar_VuelveAConsultarElInterno()
    {
        var interno = Substitute.For<ISecretProvider>();
        interno.ObtenerSecretoAsync("llm-key", Arg.Any<CancellationToken>()).Returns("valor");
        var reloj = new RelojControlado { UtcNow = DateTimeOffset.UnixEpoch };
        using var cache = new MemoryCache(new MemoryCacheOptions { Clock = reloj });
        var decorador = CrearDecorador(interno, cache, duracionMinutos: 5);

        await decorador.ObtenerSecretoAsync("llm-key", CancellationToken.None);
        reloj.UtcNow = reloj.UtcNow.AddMinutes(6);
        await decorador.ObtenerSecretoAsync("llm-key", CancellationToken.None);

        await interno.Received(2).ObtenerSecretoAsync("llm-key", Arg.Any<CancellationToken>());
    }

    private static SecretProviderConCache CrearDecorador(
        ISecretProvider interno,
        IMemoryCache cache,
        int duracionMinutos)
        => new(
            interno,
            cache,
            Options.Create(new OpcionesCacheSecretos { DuracionMinutos = duracionMinutos }));

    private sealed class RelojControlado : ISystemClock
    {
        public DateTimeOffset UtcNow { get; set; }
    }
}
