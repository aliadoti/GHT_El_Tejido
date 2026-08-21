using System.Net;
using ElTejido.Application.Common;
using ElTejido.Application.Usuarios;
using ElTejido.Domain.Common;
using ElTejido.Domain.Identidad;
using ElTejido.Domain.Usuarios;
using Microsoft.Azure.Cosmos;

namespace ElTejido.Infrastructure.Usuarios;

/// <summary>
/// Adaptador Cosmos del contenedor users para Usuario y Tag.
/// Cubre REQ 12, 13, 26.3 y ARQ 8-9 conservando el dominio libre de DTOs Cosmos.
/// </summary>
public sealed class RepositorioUsuariosCosmos : IRepositorioUsuarios
{
    /// <summary>Reintentos de la reserva de codigos ante 412/409 del contador (03 §3.1.1).</summary>
    private const int MaxIntentosSecuencia = 8;

    /// <summary>P-34 §4.1: ids por consulta al resolver identidades en bloque.</summary>
    private const int TamanoBloqueIds = 200;

    private readonly IUsersCosmosContainer _container;
    private readonly TimeProvider _tiempo;

    public RepositorioUsuariosCosmos(Container container)
        : this(new UsersCosmosContainer(container), TimeProvider.System)
    {
    }

    internal RepositorioUsuariosCosmos(IUsersCosmosContainer container, TimeProvider? tiempo = null)
    {
        _container = container;
        _tiempo = tiempo ?? TimeProvider.System;
    }

    public async Task GuardarUsuarioAsync(Usuario usuario, CancellationToken cancellationToken)
    {
        var document = UsuarioCosmosDocument.FromDomain(usuario);
        try
        {
            await _container.UpsertUsuarioAsync(document, document.Pk, cancellationToken);
        }
        catch (CosmosException exception) when (exception.StatusCode == HttpStatusCode.Conflict)
        {
            // La clave unica de `users` (/claveUnicidad) ya tiene un usuario activo con ese numero: se
            // traduce el conflicto de almacenamiento a un error de dominio limpio (409) en vez de un
            // 500. Cubre la carrera con el chequeo previo de unicidad y la latencia del indice
            // (07 §1, 04 §3, I-08 §3.1.e).
            throw new ErrorConflicto("Ya existe un usuario activo con ese numero de WhatsApp.");
        }
    }

    public async Task<Usuario?> ObtenerUsuarioPorIdAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var document = await _container.ReadUsuarioByIdAsync(id.Trim(), cancellationToken);
        return document?.ToDomain();
    }

    public async Task<Usuario?> ObtenerUsuarioPorNumeroAsync(
        NumeroWhatsApp numero,
        CancellationToken cancellationToken)
    {
        // El filtro por estado activo va aqui, no en los llamadores (I-08 §3.1.f): un numero reasignado
        // conserva a sus titulares anteriores como inactivos y no debe resolverlos nunca.
        var documents = await _container.QueryUsuariosAsync(
            new FiltroUsuariosCosmos(
                numero.Valor,
                null,
                UsuarioCosmosDocument.ToCosmosEstado(EstadoRegistro.Activo),
                null,
                null,
                [],
                null),
            cancellationToken);

        return documents.FirstOrDefault()?.ToDomain();
    }

    public async Task<IReadOnlyCollection<Usuario>> ListarUsuariosPorNumeroAsync(
        NumeroWhatsApp numero,
        CancellationToken cancellationToken)
    {
        var documents = await _container.QueryUsuariosAsync(
            new FiltroUsuariosCosmos(
                numero.Valor,
                null,
                null,
                null,
                null,
                [],
                null),
            cancellationToken);

        return documents
            .Select(document => document.ToDomain())
            .OrderBy(usuario => usuario.CreadoEn)
            .ThenBy(usuario => usuario.CodigoUsuario)
            .ToArray();
    }

    public async Task<int> ReservarCodigosUsuarioAsync(int cantidad, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(cantidad, 1);

        // Concurrencia optimista sobre un unico documento contador (03 §3.1.1): se relee y se reintenta
        // ante 412 (otro lo movio) o 409 (otro lo creo primero). Un solo viaje por bloque, no por fila.
        for (var intento = 0; intento < MaxIntentosSecuencia; intento++)
        {
            var actual = await _container.ReadSecuenciaAsync(
                SecuenciaCosmosDocument.IdUsuario,
                cancellationToken);
            var ultimoValor = actual?.UltimoValor ?? 0;
            var siguiente = SecuenciaCosmosDocument.Crear(
                SecuenciaCosmosDocument.IdUsuario,
                ultimoValor + cantidad,
                _tiempo.GetUtcNow());

            try
            {
                await _container.GuardarSecuenciaAsync(siguiente, actual?.ETag, cancellationToken);
                return ultimoValor + 1;
            }
            catch (CosmosException exception)
                when (exception.StatusCode is HttpStatusCode.PreconditionFailed or HttpStatusCode.Conflict)
            {
                // Otro lote gano la carrera; se relee el contador y se vuelve a intentar.
            }
        }

        throw new ErrorConflicto(
            "No fue posible reservar codigos de usuario por concurrencia en el contador.");
    }

    public async Task<IReadOnlyCollection<Usuario>> BuscarUsuariosAsync(
        FiltroUsuarios filtro,
        CancellationToken cancellationToken)
    {
        var documents = await _container.QueryUsuariosAsync(
            new FiltroUsuariosCosmos(
                null,
                filtro.Rol is null ? null : UsuarioCosmosDocument.ToCosmosRol(filtro.Rol.Value),
                filtro.Estado is null ? null : UsuarioCosmosDocument.ToCosmosEstado(filtro.Estado.Value),
                filtro.Area,
                filtro.Empresa,
                filtro.Tags,
                filtro.Busqueda,
                filtro.EmpresaId,
                filtro.Sede,
                filtro.Idioma),
            cancellationToken);

        return documents
            .Select(document => document.ToDomain())
            .ToArray();
    }

    public async Task<int> CompletarNombresSaludoFaltantesAsync(CancellationToken cancellationToken)
    {
        var documents = await _container.QueryUsuariosAsync(
            new FiltroUsuariosCosmos(null, null, null, null, null, [], null),
            cancellationToken);
        var faltantes = documents
            .Where(document => string.IsNullOrWhiteSpace(document.NombreSaludo))
            .ToArray();

        foreach (var document in faltantes)
        {
            var completo = UsuarioCosmosDocument.FromDomain(document.ToDomain());
            await _container.UpsertUsuarioAsync(completo, completo.Pk, cancellationToken);
        }

        return faltantes.Length;
    }

    public async Task<int> ContarNombresSaludoFaltantesAsync(CancellationToken cancellationToken)
    {
        var documents = await _container.QueryUsuariosAsync(
            new FiltroUsuariosCosmos(null, null, null, null, null, [], null),
            cancellationToken);
        return documents.Count(document => string.IsNullOrWhiteSpace(document.NombreSaludo));
    }

    /// <summary>
    /// P-34 §4.1: identidad del listado de resultados. Los ids viajan en bloques para que la consulta
    /// no crezca sin limite: con las 1.000 ideas previstas para la convencion son a lo sumo cinco
    /// consultas dentro de la particion de usuarios, en vez de una lectura puntual por participante.
    /// </summary>
    public async Task<IReadOnlyCollection<Usuario>> ListarUsuariosPorIdsAsync(
        IReadOnlyCollection<string> ids,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var unicos = ids
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (unicos.Length == 0)
        {
            return [];
        }

        var encontrados = new List<Usuario>(unicos.Length);
        for (var inicio = 0; inicio < unicos.Length; inicio += TamanoBloqueIds)
        {
            var bloque = unicos.Skip(inicio).Take(TamanoBloqueIds).ToArray();
            var documents = await _container.QueryUsuariosAsync(
                new FiltroUsuariosCosmos(null, null, null, null, null, [], null, Ids: bloque),
                cancellationToken);
            encontrados.AddRange(documents.Select(document => document.ToDomain()));
        }

        return encontrados;
    }

    public async Task GuardarTagAsync(Tag tag, CancellationToken cancellationToken)
    {
        var document = TagCosmosDocument.FromDomain(tag);
        await _container.UpsertTagAsync(document, document.Pk, cancellationToken);
    }

    public async Task<Tag?> ObtenerTagPorIdAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var document = await _container.ReadTagByIdAsync(id.Trim(), cancellationToken);
        return document?.ToDomain();
    }

    public async Task<IReadOnlyCollection<Tag>> BuscarTagsAsync(
        FiltroTags filtro,
        CancellationToken cancellationToken)
    {
        var documents = await _container.QueryTagsAsync(
            new FiltroTagsCosmos(
                filtro.TipoTag,
                filtro.Estado is null ? null : UsuarioCosmosDocument.ToCosmosEstado(filtro.Estado.Value)),
            cancellationToken);

        return documents
            .Select(document => document.ToDomain())
            .ToArray();
    }

    public async Task<int> EliminarUsuariosNoAdministrativosAsync(CancellationToken cancellationToken)
    {
        // Trae todos los usuarios (filtro vacio) y borra solo los no administrativos, mapeando a dominio
        // para respetar EsAdministrativo (conserva Admin y Visor aunque aparezcan roles nuevos a futuro).
        var documents = await _container.QueryUsuariosAsync(
            new FiltroUsuariosCosmos(null, null, null, null, null, [], null),
            cancellationToken);

        var aBorrar = documents.Where(d => !d.ToDomain().EsAdministrativo).ToArray();
        foreach (var documento in aBorrar)
        {
            await _container.DeleteUsuarioAsync(documento.Id, cancellationToken);
        }

        return aBorrar.Length;
    }
}
