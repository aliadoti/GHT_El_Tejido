using ElTejido.Application.Common;
using ElTejido.Application.Configuracion;
using ElTejido.Domain.Configuracion;

namespace ElTejido.Infrastructure.Persistencia.Memoria;

internal sealed class RepositorioCatalogosTextosMemoria : IRepositorioCatalogosTextos
{
    private readonly object _sync = new();
    private readonly Dictionary<string, Registro> _registros = new(StringComparer.Ordinal);

    public Task<IReadOnlyCollection<VersionCatalogoTextos>> BuscarAsync(
        string? idioma,
        EstadoCatalogoTextos? estado,
        CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            var resultado = _registros.Values
                .Where(x => idioma is null || x.Catalogo.Idioma == idioma)
                .Where(x => estado is null || x.Catalogo.Estado == estado)
                .Select(Mapear)
                .OrderBy(x => x.Catalogo.FamiliaId, StringComparer.Ordinal)
                .ThenBy(x => x.Catalogo.Idioma, StringComparer.Ordinal)
                .ThenByDescending(x => x.Catalogo.Version)
                .ToArray();
            return Task.FromResult<IReadOnlyCollection<VersionCatalogoTextos>>(resultado);
        }
    }

    public Task<IReadOnlyCollection<VersionCatalogoTextos>> ListarVersionesAsync(
        string familiaId,
        string idioma,
        CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            var resultado = _registros.Values
                .Where(x => x.Catalogo.FamiliaId == familiaId && x.Catalogo.Idioma == idioma)
                .OrderByDescending(x => x.Catalogo.Version)
                .Select(Mapear)
                .ToArray();
            return Task.FromResult<IReadOnlyCollection<VersionCatalogoTextos>>(resultado);
        }
    }

    public Task<VersionCatalogoTextos?> ObtenerAsync(
        string familiaId,
        string idioma,
        int version,
        CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            var resultado = _registros.TryGetValue(Id(familiaId, idioma, version), out var registro)
                ? Mapear(registro)
                : null;
            return Task.FromResult(resultado);
        }
    }

    public Task<VersionCatalogoTextos?> ObtenerActivoAsync(string idioma, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            var activos = _registros.Values
                .Where(x => x.Catalogo.Idioma == idioma && x.Catalogo.Estado == EstadoCatalogoTextos.Activo)
                .ToArray();
            if (activos.Length > 1)
            {
                throw new InvalidOperationException($"Hay mas de un catalogo activo para el idioma {idioma}.");
            }

            return Task.FromResult(activos.Length == 0 ? null : Mapear(activos[0]));
        }
    }

    public Task<VersionCatalogoTextos> CrearAsync(
        CatalogoTextosConversacion catalogo,
        CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            var id = Id(catalogo.FamiliaId, catalogo.Idioma, catalogo.Version);
            if (_registros.ContainsKey(id))
            {
                throw new ErrorConflicto("La version del catalogo ya existe.");
            }

            var registro = new Registro(catalogo, 1);
            _registros.Add(id, registro);
            return Task.FromResult(Mapear(registro));
        }
    }

    public Task<VersionCatalogoTextos> ReemplazarBorradorAsync(
        CatalogoTextosConversacion catalogo,
        string etag,
        CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            var id = Id(catalogo.FamiliaId, catalogo.Idioma, catalogo.Version);
            if (!_registros.TryGetValue(id, out var actual))
            {
                throw new ErrorNoEncontrado("La version del catalogo no existe.");
            }

            ValidarEtag(actual, etag);
            if (actual.Catalogo.Estado != EstadoCatalogoTextos.Borrador)
            {
                throw new ErrorConflicto("Solo se puede reemplazar un catalogo en borrador.");
            }

            var actualizado = new Registro(catalogo, actual.Revision + 1);
            _registros[id] = actualizado;
            return Task.FromResult(Mapear(actualizado));
        }
    }

    public Task<VersionCatalogoTextos> ActivarAsync(
        CatalogoTextosConversacion catalogoActivo,
        string etag,
        CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            var id = Id(catalogoActivo.FamiliaId, catalogoActivo.Idioma, catalogoActivo.Version);
            if (!_registros.TryGetValue(id, out var candidato))
            {
                throw new ErrorNoEncontrado("La version del catalogo no existe.");
            }

            ValidarEtag(candidato, etag);
            foreach (var item in _registros.Where(x =>
                         x.Value.Catalogo.Idioma == catalogoActivo.Idioma
                         && x.Value.Catalogo.Estado == EstadoCatalogoTextos.Activo).ToArray())
            {
                _registros[item.Key] = new Registro(
                    item.Value.Catalogo.CambiarEstado(EstadoCatalogoTextos.Inactivo, catalogoActivo.ActualizadoEn),
                    item.Value.Revision + 1);
            }

            var activo = new Registro(catalogoActivo, candidato.Revision + 1);
            _registros[id] = activo;
            return Task.FromResult(Mapear(activo));
        }
    }

    private static string Id(string familiaId, string idioma, int version)
        => $"{familiaId}|{idioma}|{version}";

    private static VersionCatalogoTextos Mapear(Registro registro)
        => new(registro.Catalogo, $"\"{registro.Revision}\"");

    private static void ValidarEtag(Registro actual, string etag)
    {
        if (!string.Equals($"\"{actual.Revision}\"", etag, StringComparison.Ordinal))
        {
            throw new ErrorConflicto("El catalogo cambio desde la ultima lectura. Recargalo antes de guardar.");
        }
    }

    private sealed record Registro(CatalogoTextosConversacion Catalogo, int Revision);
}
