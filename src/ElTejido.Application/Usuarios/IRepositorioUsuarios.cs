using ElTejido.Domain.Identidad;
using ElTejido.Domain.Usuarios;

namespace ElTejido.Application.Usuarios;

/// <summary>
/// Puerto de persistencia del contenedor Cosmos <c>users</c> para Usuario y Tag.
/// Cubre REQ §12, §13, §26.3 y ARQ §8-§9 sin acoplar la aplicacion a Cosmos.
/// </summary>
public interface IRepositorioUsuarios
{
    Task GuardarUsuarioAsync(Usuario usuario, CancellationToken cancellationToken);

    Task<Usuario?> ObtenerUsuarioPorIdAsync(string id, CancellationToken cancellationToken);

    /// <summary>
    /// Devuelve el usuario <b>activo</b> de ese numero (I-08 §3.1.f, 03 §3.1). El filtro por estado vive
    /// aqui y no en cada llamador porque los 7 puntos de uso lo requieren por igual: un numero cuyo unico
    /// registro esta inactivo no debe resolver participante ni permitir login.
    /// </summary>
    Task<Usuario?> ObtenerUsuarioPorNumeroAsync(NumeroWhatsApp numero, CancellationToken cancellationToken);

    /// <summary>
    /// Devuelve el activo y el historico de titulares de un numero, ordenados por <c>creadoEn</c>
    /// (I-08 §3.1.f). Unico camino para ver inactivos: ficha del portal y auditoria de reasignaciones.
    /// </summary>
    Task<IReadOnlyCollection<Usuario>> ListarUsuariosPorNumeroAsync(
        NumeroWhatsApp numero,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reserva un bloque de <paramref name="cantidad"/> codigos consecutivos del contador
    /// <c>seq_usuario</c> (03 §3.1.1) y devuelve el <b>primero</b> del bloque. La reserva por bloque
    /// evita golpear el contador fila por fila en la carga masiva (I-08 §3.1.b).
    /// </summary>
    Task<int> ReservarCodigosUsuarioAsync(int cantidad, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Usuario>> BuscarUsuariosAsync(
        FiltroUsuarios filtro,
        CancellationToken cancellationToken);

    /// <summary>
    /// P-35: persiste <c>nombreSaludo</c> solo en documentos historicos que aun no tienen la
    /// propiedad. Los adaptadores sin documentos legacy pueden conservar el no-op por defecto.
    /// </summary>
    Task<int> CompletarNombresSaludoFaltantesAsync(CancellationToken cancellationToken)
        => Task.FromResult(0);

    Task<int> ContarNombresSaludoFaltantesAsync(CancellationToken cancellationToken)
        => Task.FromResult(0);

    /// <summary>
    /// P-34 §4.1: los participantes de un conjunto de ids, para que el servidor resuelva la identidad
    /// del listado de resultados en vez de que el portal descargue el maestro y haga el join en el
    /// navegador. Los adaptadores persistentes lo traducen a consultas acotadas por ids dentro de la
    /// particion de usuarios; la implementacion por defecto lee el maestro y filtra en memoria —el
    /// comportamiento que tenia el portal—, de modo que un doble sin la consulta nativa devuelve lo
    /// mismo. Un id sin usuario simplemente no aparece: el llamador decide como presentarlo.
    /// </summary>
    async Task<IReadOnlyCollection<Usuario>> ListarUsuariosPorIdsAsync(
        IReadOnlyCollection<string> ids,
        CancellationToken cancellationToken)
    {
        var buscados = ids.Where(id => !string.IsNullOrWhiteSpace(id)).ToHashSet(StringComparer.Ordinal);
        if (buscados.Count == 0)
        {
            return [];
        }

        return (await BuscarUsuariosAsync(new FiltroUsuarios(), cancellationToken))
            .Where(usuario => buscados.Contains(usuario.Id))
            .ToArray();
    }

    Task GuardarTagAsync(Tag tag, CancellationToken cancellationToken);

    Task<Tag?> ObtenerTagPorIdAsync(string id, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Tag>> BuscarTagsAsync(FiltroTags filtro, CancellationToken cancellationToken);

    /// <summary>
    /// Borra fisicamente los usuarios <b>no administrativos</b> (rol Participante) del contenedor
    /// <c>users</c> (P-15, purga total de datos de prueba). Conserva siempre los administrativos
    /// (<see cref="Usuario.EsAdministrativo"/>: Admin y Visor) para no dejar el portal sin acceso.
    /// No toca los Tags. Idempotente; devuelve el numero de usuarios borrados.
    /// </summary>
    Task<int> EliminarUsuariosNoAdministrativosAsync(CancellationToken cancellationToken);
}
