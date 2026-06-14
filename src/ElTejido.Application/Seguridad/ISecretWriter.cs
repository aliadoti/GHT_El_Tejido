namespace ElTejido.Application.Seguridad;

/// <summary>
/// Puerto de escritura write-only de secretos (10 seccion 4, 07 seccion 5).
/// Se usa para rotar/crear la API key LLM sin persistirla en Cosmos.
/// </summary>
public interface ISecretWriter
{
    Task GuardarSecretoAsync(string nombre, string valor, CancellationToken cancellationToken);
}
