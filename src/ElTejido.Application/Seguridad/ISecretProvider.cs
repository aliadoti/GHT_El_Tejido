namespace ElTejido.Application.Seguridad;

/// <summary>
/// Puerto de acceso a secretos (10 §4). El secreto nunca vive en BD ni en codigo; solo se
/// referencia por nombre canonico (ver <see cref="NombresSecretos"/>) y se resuelve aqui.
/// Las implementaciones (Key Vault por Managed Identity, configuracion local, cache corta)
/// viven en Infrastructure. El valor devuelto NO debe loguearse ni persistirse en disco.
/// </summary>
public interface ISecretProvider
{
    /// <summary>
    /// Obtiene el valor del secreto identificado por <paramref name="nombre"/> (nombre canonico
    /// de Key Vault, p. ej. <c>llm-key</c>).
    /// </summary>
    Task<string> ObtenerSecretoAsync(string nombre, CancellationToken cancellationToken);
}
