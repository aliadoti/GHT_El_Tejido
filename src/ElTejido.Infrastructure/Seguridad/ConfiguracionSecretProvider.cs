using ElTejido.Application.Seguridad;
using Microsoft.Extensions.Configuration;

namespace ElTejido.Infrastructure.Seguridad;

/// <summary>
/// Proveedor de secretos para desarrollo local (10 §4): lee de <see cref="IConfiguration"/>,
/// alimentada por <c>dotnet user-secrets</c>. Los valores viven bajo la seccion
/// <see cref="Seccion"/> con el nombre canonico del secreto como clave (p. ej. <c>Secretos:llm-key</c>).
/// Nunca persiste en disco; solo lee la configuracion ya cargada en memoria.
/// </summary>
public sealed class ConfiguracionSecretProvider : ISecretProvider
{
    /// <summary>Seccion de configuracion que agrupa los secretos locales.</summary>
    public const string Seccion = "Secretos";

    private readonly IConfiguration _configuration;

    public ConfiguracionSecretProvider(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public Task<string> ObtenerSecretoAsync(string nombre, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);

        var valor = _configuration[$"{Seccion}:{nombre}"];
        if (string.IsNullOrEmpty(valor))
        {
            // El nombre del secreto no es sensible; el valor nunca se incluye en el mensaje.
            throw new KeyNotFoundException(
                $"No se encontro el secreto '{nombre}' en la configuracion (seccion '{Seccion}').");
        }

        return Task.FromResult(valor);
    }
}
