using ElTejido.Application.Diagnostico;
using Microsoft.Azure.Cosmos;

namespace ElTejido.Infrastructure.Diagnostico;

/// <summary>
/// Comprueba que la base de datos Cosmos sea accesible por la identidad administrada (02 §6,
/// guia de Azure §2/§7). En modo memoria (sin <c>CosmosClient</c> registrado) reporta
/// <see cref="EstadoPreparacion.NoAplica"/>. Verifica leyendo la base, lo que ejercita a la vez la
/// conectividad y el rol de datos (Built-in Data Contributor).
/// </summary>
public sealed class ComprobacionCosmos : IComprobacionPreparacion
{
    private const string Componente = "cosmos";

    private readonly CosmosClient? _cliente;
    private readonly string _database;

    public ComprobacionCosmos(CosmosClient? cliente, string database)
    {
        _cliente = cliente;
        _database = database;
    }

    public async Task<IReadOnlyList<ResultadoComprobacion>> ComprobarAsync(CancellationToken cancellationToken)
    {
        if (_cliente is null)
        {
            return new[]
            {
                new ResultadoComprobacion(Componente, EstadoPreparacion.NoAplica, "Modo memoria o sin Cosmos configurado."),
            };
        }

        try
        {
            await _cliente.GetDatabase(_database).ReadAsync(cancellationToken: cancellationToken);
            return new[]
            {
                new ResultadoComprobacion(Componente, EstadoPreparacion.Ok, $"Base '{_database}' accesible."),
            };
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return new[]
            {
                new ResultadoComprobacion(Componente, EstadoPreparacion.Faltante, $"La base '{_database}' no existe."),
            };
        }
        catch (CosmosException ex) when (ex.StatusCode is System.Net.HttpStatusCode.Forbidden or System.Net.HttpStatusCode.Unauthorized)
        {
            return new[]
            {
                new ResultadoComprobacion(
                    Componente,
                    EstadoPreparacion.Error,
                    $"Acceso denegado (HTTP {(int)ex.StatusCode}). Revisa el rol de datos de la identidad administrada."),
            };
        }
        catch (Exception ex)
        {
            return new[]
            {
                new ResultadoComprobacion(Componente, EstadoPreparacion.Error, $"Cosmos inaccesible: {ex.GetType().Name}."),
            };
        }
    }
}
