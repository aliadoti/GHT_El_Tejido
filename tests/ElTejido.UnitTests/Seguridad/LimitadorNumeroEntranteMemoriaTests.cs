using ElTejido.Infrastructure.Seguridad;
using ElTejido.UnitTests.Soporte;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;

namespace ElTejido.UnitTests.Seguridad;

/// <summary>
/// P-10 — el limitador por número usa una ventana deslizante de 1 minuto en memoria y queda
/// deshabilitado cuando el máximo es 0/negativo.
/// </summary>
public sealed class LimitadorNumeroEntranteMemoriaTests
{
    private const string Numero = "573001112233";
    private static readonly DateTimeOffset T0 = DateTimeOffset.UnixEpoch;

    [Fact]
    public async Task Maximo0_SiemprePermite()
    {
        var limitador = Crear(maximo: 0, out _);

        for (var i = 0; i < 50; i++)
        {
            (await limitador.RegistrarYPermitirAsync(Numero, CancellationToken.None)).Should().BeTrue();
        }
    }

    [Fact]
    public async Task ExcedeElMaximoEnLaVentana_Bloquea()
    {
        var limitador = Crear(maximo: 3, out _);

        (await limitador.RegistrarYPermitirAsync(Numero, CancellationToken.None)).Should().BeTrue();
        (await limitador.RegistrarYPermitirAsync(Numero, CancellationToken.None)).Should().BeTrue();
        (await limitador.RegistrarYPermitirAsync(Numero, CancellationToken.None)).Should().BeTrue();
        (await limitador.RegistrarYPermitirAsync(Numero, CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task TrasDeslizarLaVentana_VuelveAPermitir()
    {
        var limitador = Crear(maximo: 2, out var reloj);

        (await limitador.RegistrarYPermitirAsync(Numero, CancellationToken.None)).Should().BeTrue();
        (await limitador.RegistrarYPermitirAsync(Numero, CancellationToken.None)).Should().BeTrue();
        (await limitador.RegistrarYPermitirAsync(Numero, CancellationToken.None)).Should().BeFalse();

        // Al pasar más de un minuto las marcas viejas salen de la ventana.
        reloj.Avanzar(TimeSpan.FromSeconds(61));
        (await limitador.RegistrarYPermitirAsync(Numero, CancellationToken.None)).Should().BeTrue();
    }

    [Fact]
    public async Task ContadoresPorNumeroSonIndependientes()
    {
        var limitador = Crear(maximo: 1, out _);

        (await limitador.RegistrarYPermitirAsync("573000000001", CancellationToken.None)).Should().BeTrue();
        (await limitador.RegistrarYPermitirAsync("573000000001", CancellationToken.None)).Should().BeFalse();
        (await limitador.RegistrarYPermitirAsync("573000000002", CancellationToken.None)).Should().BeTrue();
    }

    private static LimitadorNumeroEntranteMemoria Crear(int maximo, out RelojFijo reloj)
    {
        reloj = new RelojFijo(T0);
        return new LimitadorNumeroEntranteMemoria(new MemoryCache(new MemoryCacheOptions()), maximo, reloj);
    }
}
