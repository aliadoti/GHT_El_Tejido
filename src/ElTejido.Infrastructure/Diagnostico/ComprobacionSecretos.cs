using Azure;
using ElTejido.Application.Diagnostico;
using ElTejido.Application.Seguridad;

namespace ElTejido.Infrastructure.Diagnostico;

/// <summary>
/// Comprueba que cada secreto canonico (10 §4, <see cref="NombresSecretos"/>) sea legible por el
/// proveedor activo (Key Vault por Managed Identity o configuracion local). Solo reporta
/// presencia/ausencia y errores de acceso; <b>nunca</b> incluye el valor del secreto (REQ §22.4.9).
/// Util para confirmar, tras cargar los secretos de WhatsApp, que la identidad del App Service
/// puede leerlos (guia de Azure §11, guia de WhatsApp §6).
/// </summary>
public sealed class ComprobacionSecretos : IComprobacionPreparacion
{
    private static readonly string[] SecretosRequeridos =
    {
        NombresSecretos.JwtSign,
        NombresSecretos.OtpSalt,
        NombresSecretos.WaVerifyToken,
        NombresSecretos.WaToken,
        NombresSecretos.WaAppSecret,
        NombresSecretos.LlmKey,
    };

    private readonly ISecretProvider _secretos;

    public ComprobacionSecretos(ISecretProvider secretos)
    {
        _secretos = secretos;
    }

    public async Task<IReadOnlyList<ResultadoComprobacion>> ComprobarAsync(CancellationToken cancellationToken)
    {
        var resultados = new List<ResultadoComprobacion>(SecretosRequeridos.Length);

        foreach (var nombre in SecretosRequeridos)
        {
            resultados.Add(await ComprobarSecretoAsync(nombre, cancellationToken));
        }

        return resultados;
    }

    private async Task<ResultadoComprobacion> ComprobarSecretoAsync(string nombre, CancellationToken cancellationToken)
    {
        var componente = $"secreto:{nombre}";
        try
        {
            var valor = await _secretos.ObtenerSecretoAsync(nombre, cancellationToken);
            return string.IsNullOrWhiteSpace(valor)
                ? new ResultadoComprobacion(componente, EstadoPreparacion.Faltante, "El secreto existe pero esta vacio.")
                : new ResultadoComprobacion(componente, EstadoPreparacion.Ok, "Secreto presente y legible.");
        }
        catch (KeyNotFoundException)
        {
            return new ResultadoComprobacion(componente, EstadoPreparacion.Faltante, "Secreto no encontrado.");
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return new ResultadoComprobacion(componente, EstadoPreparacion.Faltante, "Secreto no existe en Key Vault.");
        }
        catch (RequestFailedException ex)
        {
            // 403/401: el secreto puede existir pero la identidad no tiene rol "Key Vault Secrets User".
            return new ResultadoComprobacion(
                componente,
                EstadoPreparacion.Error,
                $"Acceso a Key Vault fallo (HTTP {ex.Status}). Revisa el rol de la identidad administrada.");
        }
        catch (Exception ex)
        {
            return new ResultadoComprobacion(
                componente,
                EstadoPreparacion.Error,
                $"Fallo al leer el secreto: {ex.GetType().Name}.");
        }
    }
}
