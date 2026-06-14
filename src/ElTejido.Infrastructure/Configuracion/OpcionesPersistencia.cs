using Microsoft.Extensions.Configuration;

namespace ElTejido.Infrastructure.Configuracion;

public static class OpcionesPersistencia
{
    public const string Seccion = "Persistencia";
    public const string ModoCosmos = "Cosmos";
    public const string ModoMemoria = "Memoria";

    public static string Resolver(IConfiguration configuration)
    {
        var modo = configuration[$"{Seccion}:Modo"];
        if (!string.IsNullOrWhiteSpace(modo))
        {
            return modo.Trim();
        }

        return string.IsNullOrWhiteSpace(configuration["Cosmos:AccountEndpoint"])
            ? string.Empty
            : ModoCosmos;
    }

    public static bool EsMemoria(IConfiguration configuration)
        => string.Equals(Resolver(configuration), ModoMemoria, StringComparison.OrdinalIgnoreCase);

    public static bool EsCosmos(IConfiguration configuration)
        => string.Equals(Resolver(configuration), ModoCosmos, StringComparison.OrdinalIgnoreCase);

    public static bool HayAlmacen(IConfiguration configuration)
        => EsMemoria(configuration) || EsCosmos(configuration);
}
