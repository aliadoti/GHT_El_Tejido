using System.Globalization;
using System.Text;

namespace ElTejido.Calibracion;

/// <summary>
/// Renderiza el <see cref="ReporteCalibracion"/> a Markdown legible (D5 §3.2) para revisión humana.
/// Determinista (las listas vienen ya ordenadas del agregador); sin secretos ni PII.
/// </summary>
public static class EscritorReporteMarkdown
{
    private static readonly CultureInfo Cultura = CultureInfo.InvariantCulture;

    public static string Renderizar(ReporteCalibracion reporte)
    {
        ArgumentNullException.ThrowIfNull(reporte);
        var meta = reporte.Metadatos;
        var sb = new StringBuilder();

        sb.AppendLine("# Reporte de calibración — D5");
        sb.AppendLine();
        sb.AppendLine($"- Campaña: `{meta.CampaniaId}`");
        sb.AppendLine($"- Rúbrica: `{meta.RubricaRef}` v{meta.VersionRubrica}");
        sb.AppendLine($"- Prompt: `{meta.PromptRef}` v{meta.VersionPrompt}");
        sb.AppendLine($"- ConfigLLM: `{meta.ConfigLlmRef}` · modelo `{meta.Modelo}`");
        sb.AppendLine($"- N (repeticiones por entrada): {meta.N}");
        sb.AppendLine($"- Timestamp: {meta.Timestamp.ToString("O", Cultura)}");
        sb.AppendLine($"- Entradas: {reporte.TotalEntradas} · Muestras: {reporte.TotalMuestras}");
        sb.AppendLine();

        sb.AppendLine("## Distribución de scores (muestras válidas)");
        sb.AppendLine();
        sb.AppendLine("| Eje | Muestras | Min | Max | Media | Desv |");
        sb.AppendLine("|---|---:|---:|---:|---:|---:|");
        AppendFilaDistribucion(sb, "**total**", reporte.DistribucionTotal);
        foreach (var eje in reporte.DistribucionPorEje)
        {
            AppendFilaDistribucion(sb, eje.Eje, eje);
        }

        sb.AppendLine();
        sb.AppendLine("## Decisión (cerrar vs repreguntar, muestras válidas)");
        sb.AppendLine();
        sb.AppendLine($"- Cerrar: {reporte.Decisiones.Cerrar}");
        sb.AppendLine($"- Repreguntar: {reporte.Decisiones.Repreguntar}");
        sb.AppendLine($"- Proporción cerrar: {Num(reporte.Decisiones.ProporcionCerrar)}");

        sb.AppendLine();
        sb.AppendLine("## Salida inválida (fallback)");
        sb.AppendLine();
        sb.AppendLine($"- Inválidas: {reporte.Invalidos.Invalidas}/{reporte.Invalidos.TotalMuestras} ({Num(reporte.Invalidos.Porcentaje)}%)");
        foreach (var motivo in reporte.Invalidos.PorMotivo)
        {
            sb.AppendLine($"  - `{motivo.Motivo}`: {motivo.Conteo}");
        }

        sb.AppendLine();
        sb.AppendLine("## Tokens / costo");
        sb.AppendLine();
        sb.AppendLine($"- Prompt: {reporte.Tokens.PromptTokens} · Completion: {reporte.Tokens.CompletionTokens} · Total: {reporte.Tokens.Total}");
        if (reporte.Tokens.CostoEstimado is { } costo)
        {
            sb.AppendLine($"- Costo estimado: {costo.ToString(Cultura)}");
        }

        sb.AppendLine();
        sb.AppendLine("## Ideas detectadas por entrada");
        sb.AppendLine();
        foreach (var entrada in reporte.IdeasPorEntrada)
        {
            var ideas = entrada.Ideas.Count == 0 ? "—" : string.Join(", ", entrada.Ideas);
            sb.AppendLine($"- `{entrada.EntradaId}` ({entrada.Categoria}): {ideas}");
        }

        return sb.ToString();
    }

    private static void AppendFilaDistribucion(StringBuilder sb, string etiqueta, DistribucionEje eje)
        => sb.AppendLine(
            $"| {etiqueta} | {eje.Muestras} | {eje.Min.ToString(Cultura)} | {eje.Max.ToString(Cultura)} | {Num(eje.Media)} | {Num(eje.Desviacion)} |");

    private static string Num(double valor) => valor.ToString("0.####", Cultura);
}
