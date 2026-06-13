using ElTejido.Application.Seguridad;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace ElTejido.Infrastructure.Seguridad;

/// <summary>
/// Decorador que cachea en memoria los secretos resueltos por un proveedor interno con
/// expiracion corta (10 §4), para no golpear Key Vault en cada llamada. Nunca persiste en disco.
/// </summary>
public sealed class SecretProviderConCache : ISecretProvider
{
    private const string PrefijoClave = "secreto:";

    private readonly ISecretProvider _inner;
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _duracion;

    public SecretProviderConCache(
        ISecretProvider inner,
        IMemoryCache cache,
        IOptions<OpcionesCacheSecretos> opciones)
    {
        _inner = inner;
        _cache = cache;
        _duracion = TimeSpan.FromMinutes(Math.Max(1, opciones.Value.DuracionMinutos));
    }

    /// <summary>Proveedor interno decorado (expuesto para verificar la seleccion por configuracion).</summary>
    internal ISecretProvider Inner => _inner;

    public async Task<string> ObtenerSecretoAsync(string nombre, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);

        var clave = PrefijoClave + nombre;
        if (_cache.TryGetValue(clave, out string? cacheado) && cacheado is not null)
        {
            return cacheado;
        }

        var valor = await _inner.ObtenerSecretoAsync(nombre, cancellationToken);
        _cache.Set(
            clave,
            valor,
            new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = _duracion });
        return valor;
    }
}
