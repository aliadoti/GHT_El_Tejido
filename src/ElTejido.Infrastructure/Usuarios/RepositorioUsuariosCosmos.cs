using System.Net;
using ElTejido.Application.Common;
using ElTejido.Application.Usuarios;
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
    private readonly IUsersCosmosContainer _container;

    public RepositorioUsuariosCosmos(Container container)
        : this(new UsersCosmosContainer(container))
    {
    }

    internal RepositorioUsuariosCosmos(IUsersCosmosContainer container)
    {
        _container = container;
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
            // La clave unica de `users` (/whatsappNormalizado) ya tiene ese numero: se traduce el
            // conflicto de almacenamiento a un error de dominio limpio (409) en vez de un 500. Cubre
            // la carrera con el chequeo previo de unicidad y la latencia del indice (07 §1, 04 §3).
            throw new ErrorConflicto("Ya existe un usuario con ese numero de WhatsApp.");
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

        return documents.FirstOrDefault()?.ToDomain();
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
                filtro.Busqueda),
            cancellationToken);

        return documents
            .Select(document => document.ToDomain())
            .ToArray();
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
