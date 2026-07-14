using ElTejido.Application.WhatsApp;
using Microsoft.Extensions.Caching.Memory;

namespace ElTejido.Infrastructure.Seguridad;

/// <summary>
/// P-10 — límite de mensajes entrantes por número con <b>ventana deslizante</b> de 1 minuto en
/// memoria (10 §2). Suficiente para el MVP en un solo proceso (02 §5); en varias instancias el
/// límite es por instancia (limitación documentada en <c>SUPUESTOS.md#rate-numero-entrante</c>).
/// Con <c>maximoPorMinuto &lt;= 0</c> queda deshabilitado (permite todo): patrón D1, nace apagado.
/// </summary>
public sealed class LimitadorNumeroEntranteMemoria : ILimitadorNumeroEntrante
{
    private const string PrefijoClave = "rate-numero:";
    private static readonly TimeSpan Ventana = TimeSpan.FromMinutes(1);

    private readonly IMemoryCache _cache;
    private readonly int _maximo;
    private readonly TimeProvider _tiempo;

    public LimitadorNumeroEntranteMemoria(IMemoryCache cache, int maximoPorMinuto, TimeProvider tiempo)
    {
        _cache = cache;
        _maximo = maximoPorMinuto;
        _tiempo = tiempo;
    }

    public Task<bool> RegistrarYPermitirAsync(string numero, CancellationToken cancellationToken)
    {
        if (_maximo <= 0)
        {
            // Deshabilitado: no cuenta ni bloquea.
            return Task.FromResult(true);
        }

        var registros = _cache.GetOrCreate(PrefijoClave + numero, entry =>
        {
            // El bucket muere tras un minuto sin actividad; cada mensaje renueva el sliding.
            entry.SlidingExpiration = Ventana;
            return new VentanaDeslizante();
        })!;

        var ahora = _tiempo.GetUtcNow();
        lock (registros.Sync)
        {
            var limite = ahora - Ventana;
            while (registros.Marcas.Count > 0 && registros.Marcas.Peek() <= limite)
            {
                registros.Marcas.Dequeue();
            }

            if (registros.Marcas.Count >= _maximo)
            {
                return Task.FromResult(false);
            }

            registros.Marcas.Enqueue(ahora);
            return Task.FromResult(true);
        }
    }

    private sealed class VentanaDeslizante
    {
        public object Sync { get; } = new();

        public Queue<DateTimeOffset> Marcas { get; } = new();
    }
}
