using System.Text.Json;
using System.Text.Json.Serialization;

namespace ElTejido.Calibracion;

/// <summary>
/// Serializa/deserializa el <see cref="ReporteCalibracion"/> a JSON determinista (camelCase, indentado).
/// El JSON es el formato del <b>baseline congelado</b> (D5 §3.3) y del reporte de cada corrido.
/// </summary>
public static class EscritorReporteJson
{
    private static readonly JsonSerializerOptions Opciones = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Serializar(ReporteCalibracion reporte)
        => JsonSerializer.Serialize(reporte, Opciones);

    public static ReporteCalibracion Deserializar(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new CalibracionException("El reporte/baseline está vacío.");
        }

        ReporteCalibracion? reporte;
        try
        {
            reporte = JsonSerializer.Deserialize<ReporteCalibracion>(json, Opciones);
        }
        catch (JsonException ex)
        {
            throw new CalibracionException("El reporte/baseline no es JSON válido: " + ex.Message, ex);
        }

        return reporte ?? throw new CalibracionException("El reporte/baseline deserializó a null.");
    }

    public static ReporteCalibracion DeserializarArchivo(string ruta)
    {
        if (!File.Exists(ruta))
        {
            throw new CalibracionException($"No se encontró el reporte/baseline en '{ruta}'.");
        }

        return Deserializar(File.ReadAllText(ruta));
    }
}
