using System.Text.Json;
using System.Text.Json.Serialization;

namespace ElTejido.Calibracion;

/// <summary>
/// Carga y valida el golden set desde JSON versionado (D5 §3.1). El JSON es <b>dato</b>: el harness
/// no se recompila al ampliar el set. Valida invariantes mínimos (ids no vacíos y únicos, decisión
/// esperada en {cerrar, repreguntar}, set no vacío) para fallar temprano y con mensaje claro; no
/// impone PII-scrub (eso es revisión humana antes de versionar, D5 §5).
/// </summary>
public static class CargadorGoldenSet
{
    private static readonly JsonSerializerOptions Opciones = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Carga desde una ruta de archivo; error claro si no existe.</summary>
    public static GoldenSet CargarDesdeArchivo(string ruta)
    {
        if (!File.Exists(ruta))
        {
            throw new CalibracionException($"No se encontró el golden set en '{ruta}'.");
        }

        return Cargar(File.ReadAllText(ruta));
    }

    /// <summary>Deserializa y valida el contenido JSON del golden set.</summary>
    public static GoldenSet Cargar(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new CalibracionException("El golden set está vacío.");
        }

        GoldenSet? set;
        try
        {
            set = JsonSerializer.Deserialize<GoldenSet>(json, Opciones);
        }
        catch (JsonException ex)
        {
            throw new CalibracionException("El golden set no es JSON válido: " + ex.Message, ex);
        }

        if (set is null)
        {
            throw new CalibracionException("El golden set deserializó a null.");
        }

        return Validar(set);
    }

    private static GoldenSet Validar(GoldenSet set)
    {
        if (string.IsNullOrWhiteSpace(set.Version))
        {
            throw new CalibracionException("El golden set no declara 'version'.");
        }

        if (set.Entradas is null || set.Entradas.Count == 0)
        {
            throw new CalibracionException("El golden set no tiene entradas.");
        }

        var vistos = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entrada in set.Entradas)
        {
            if (entrada is null)
            {
                throw new CalibracionException("El golden set contiene una entrada null.");
            }

            if (string.IsNullOrWhiteSpace(entrada.Id))
            {
                throw new CalibracionException("Hay una entrada sin 'id'.");
            }

            if (!vistos.Add(entrada.Id))
            {
                throw new CalibracionException($"Id de entrada duplicado: '{entrada.Id}'.");
            }

            if (string.IsNullOrWhiteSpace(entrada.TextoRespuesta))
            {
                throw new CalibracionException($"La entrada '{entrada.Id}' no tiene 'textoRespuesta'.");
            }

            var decision = entrada.Esperado?.Decision;
            if (decision is not null && !DecisionCalibracion.EsValida(decision))
            {
                throw new CalibracionException(
                    $"La entrada '{entrada.Id}' tiene una decisión esperada inválida: '{decision}' " +
                    $"(debe ser '{DecisionCalibracion.Cerrar}' o '{DecisionCalibracion.Repreguntar}').");
            }
        }

        return set;
    }
}
