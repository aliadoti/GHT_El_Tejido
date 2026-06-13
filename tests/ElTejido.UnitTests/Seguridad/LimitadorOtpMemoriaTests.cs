using ElTejido.Application.Auth;
using ElTejido.Domain.Identidad;
using ElTejido.Infrastructure.Seguridad;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace ElTejido.UnitTests.Seguridad;

public sealed class LimitadorOtpMemoriaTests
{
    private static readonly NumeroWhatsApp Numero = NumeroWhatsApp.FromNormalized("573001119999");
    private static readonly NumeroWhatsApp OtroNumero = NumeroWhatsApp.FromNormalized("573002228888");

    [Fact]
    public async Task RegistrarYPermitirAsync_PermiteHastaElMaximoLuegoBloquea()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var limitador = Crear(cache, maximo: 3);

        var resultados = new List<bool>();
        for (var i = 0; i < 4; i++)
        {
            resultados.Add(await limitador.RegistrarYPermitirAsync(Numero, CancellationToken.None));
        }

        resultados.Should().Equal(true, true, true, false);
    }

    [Fact]
    public async Task RegistrarYPermitirAsync_CuentaPorNumeroDeFormaIndependiente()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var limitador = Crear(cache, maximo: 1);

        var primeroNumeroA = await limitador.RegistrarYPermitirAsync(Numero, CancellationToken.None);
        var primeroNumeroB = await limitador.RegistrarYPermitirAsync(OtroNumero, CancellationToken.None);

        primeroNumeroA.Should().BeTrue();
        primeroNumeroB.Should().BeTrue();
    }

    private static LimitadorOtpMemoria Crear(IMemoryCache cache, int maximo)
        => new(cache, Options.Create(new OpcionesAuth
        {
            OtpSolicitudesPorVentana = maximo,
            OtpVentanaSolicitudesMinutos = 60,
        }));
}
