using ElTejido.Application.Seguridad;

namespace ElTejido.Infrastructure.Seguridad;

public sealed class SecretWriterMemoria : ISecretWriter
{
    private readonly Dictionary<string, string> _secretos = new(StringComparer.Ordinal);

    public Task GuardarSecretoAsync(string nombre, string valor, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        ArgumentException.ThrowIfNullOrWhiteSpace(valor);

        _secretos[nombre] = valor;
        return Task.CompletedTask;
    }

    internal IReadOnlyDictionary<string, string> Secretos => _secretos;
}
