using System.Collections.Concurrent;
using ElTejido.Application.Common;
using ElTejido.Application.Seguridad;
using ElTejido.Domain.Configuracion;
using ElTejido.Domain.Seguridad;

namespace ElTejido.Application.Configuracion;

public sealed class ProveedorTextosConversacion : IProveedorTextosConversacion, IInvalidacionCacheCatalogosTextos
{
    private readonly IRepositorioCatalogosTextos _repositorio;
    private readonly IRepositorioLogSeguridad _auditoria;
    private readonly OpcionesCatalogoTextos _opciones;
    private readonly TimeProvider _tiempo;
    private readonly ConcurrentDictionary<string, EntradaCache> _cache = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, VersionCatalogoTextos> _ultimaVersionValida = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _bloqueos = new(StringComparer.Ordinal);

    public ProveedorTextosConversacion(
        IRepositorioCatalogosTextos repositorio,
        IRepositorioLogSeguridad auditoria,
        OpcionesCatalogoTextos opciones,
        TimeProvider tiempo)
    {
        _repositorio = repositorio;
        _auditoria = auditoria;
        _opciones = opciones;
        _tiempo = tiempo;
    }

    public Task<ResultadoTextosConversacion> ObtenerParaRuntimeAsync(
        string idioma,
        CancellationToken cancellationToken)
    {
        var normalizado = ValidarIdioma(idioma);
        return !_opciones.Habilitado
            ? Task.FromResult(new ResultadoTextosConversacion(null, OrigenTextosConversacion.Legacy))
            : ResolverAsync(normalizado, cancellationToken);
    }

    public Task<ResultadoTextosConversacion> PrevisualizarAsync(
        string idioma,
        CancellationToken cancellationToken)
        => ResolverAsync(ValidarIdioma(idioma), cancellationToken);

    public void Invalidar(string idioma)
    {
        if (!string.IsNullOrWhiteSpace(idioma))
        {
            _cache.TryRemove(idioma.Trim().ToLowerInvariant(), out _);
        }
    }

    private async Task<ResultadoTextosConversacion> ResolverAsync(
        string idioma,
        CancellationToken cancellationToken)
    {
        var ahora = _tiempo.GetUtcNow();
        if (_cache.TryGetValue(idioma, out var entrada) && entrada.ExpiraEn > ahora)
        {
            return new ResultadoTextosConversacion(entrada.Version, OrigenTextosConversacion.Cache);
        }

        var bloqueo = _bloqueos.GetOrAdd(idioma, _ => new SemaphoreSlim(1, 1));
        await bloqueo.WaitAsync(cancellationToken);
        try
        {
            ahora = _tiempo.GetUtcNow();
            if (_cache.TryGetValue(idioma, out entrada) && entrada.ExpiraEn > ahora)
            {
                return new ResultadoTextosConversacion(entrada.Version, OrigenTextosConversacion.Cache);
            }

            try
            {
                var activo = await _repositorio.ObtenerActivoAsync(idioma, cancellationToken);
                if (activo is not null)
                {
                    ValidarSnapshot(activo);
                    var ttl = TimeSpan.FromSeconds(Math.Clamp(_opciones.CacheSegundos, 1, 3600));
                    _cache[idioma] = new EntradaCache(activo, ahora.Add(ttl));
                    _ultimaVersionValida[idioma] = activo;
                    return new ResultadoTextosConversacion(activo, OrigenTextosConversacion.Catalogo);
                }

                return await DegradarAsync(idioma, "sinActivo", cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                return await DegradarAsync(idioma, "lecturaOValidacion", cancellationToken);
            }
        }
        finally
        {
            bloqueo.Release();
        }
    }

    private async Task<ResultadoTextosConversacion> DegradarAsync(
        string idioma,
        string motivo,
        CancellationToken cancellationToken)
    {
        if (_ultimaVersionValida.TryGetValue(idioma, out var ultima))
        {
            await AuditarFallbackSeguroAsync(idioma, "ultimaVersionValida", motivo, cancellationToken);
            return new ResultadoTextosConversacion(ultima, OrigenTextosConversacion.UltimaVersionValida);
        }

        var emergencia = CatalogosTextosSemilla.CrearVersionEmergencia(idioma);
        await AuditarFallbackSeguroAsync(idioma, "emergencia", motivo, cancellationToken);
        return new ResultadoTextosConversacion(emergencia, OrigenTextosConversacion.Emergencia);
    }

    private async Task AuditarFallbackSeguroAsync(
        string idioma,
        string origen,
        string motivo,
        CancellationToken cancellationToken)
    {
        try
        {
            await _auditoria.RegistrarAsync(
                LogSeguridad.Crear(
                    "catalogo_fallback_" + Guid.NewGuid().ToString("N"),
                    TipoEventoSeguridad.CatalogoTextosConversacion,
                    usuarioId: null,
                    numero: null,
                    resultado: "fallbackRuntime",
                    detalle: $"idioma={idioma};origen={origen};motivo={motivo}",
                    correlationId: null,
                    timestamp: _tiempo.GetUtcNow()),
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // La auditoria de degradacion nunca puede derribar el fallback que protege la conversacion.
        }
    }

    private static void ValidarSnapshot(VersionCatalogoTextos version)
    {
        if (version.Catalogo.Estado != EstadoCatalogoTextos.Activo)
        {
            throw new InvalidDataException("El repositorio devolvio un catalogo no activo.");
        }

        var huella = ValidadorCatalogoTextosConversacion.ValidarYCalcularHuella(
            version.Catalogo.Mensajes,
            version.Catalogo.Frases);
        if (!string.Equals(huella, version.Catalogo.Huella, StringComparison.Ordinal))
        {
            throw new InvalidDataException("La huella del catalogo activo no coincide con su contenido.");
        }
    }

    private static string ValidarIdioma(string idioma)
    {
        var valor = idioma?.Trim().ToLowerInvariant();
        if (valor is not ("es" or "en"))
        {
            throw new ErrorValidacion(
                "El idioma debe ser 'es' o 'en'.",
                new[] { new DetalleError("idioma", "valor_invalido") });
        }

        return valor;
    }

    private sealed record EntradaCache(VersionCatalogoTextos Version, DateTimeOffset ExpiraEn);
}
