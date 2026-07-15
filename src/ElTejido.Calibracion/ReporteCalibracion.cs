namespace ElTejido.Calibracion;

/// <summary>
/// Reporte determinista de un corrido de calibración (D5 §3.2). Es la referencia que se congela como
/// baseline y contra la que se comparan corridos futuros (D5 §3.3). Sin secretos ni PII: solo refs de
/// versión, agregados numéricos e ideas detectadas.
/// </summary>
public sealed record ReporteCalibracion(
    MetadatosCorrido Metadatos,
    int TotalEntradas,
    int TotalMuestras,
    DistribucionEje DistribucionTotal,
    IReadOnlyList<DistribucionEje> DistribucionPorEje,
    ConteoDecisiones Decisiones,
    ResumenInvalidos Invalidos,
    ResumenTokens Tokens,
    IReadOnlyList<IdeasEntrada> IdeasPorEntrada);

/// <summary>Identidad reproducible del corrido (ARQ §8.3): tripleta versionada + N + timestamp + precio opcional.</summary>
public sealed record MetadatosCorrido(
    string CampaniaId,
    string RubricaRef,
    int VersionRubrica,
    string PromptRef,
    int VersionPrompt,
    string ConfigLlmRef,
    string Modelo,
    int N,
    DateTimeOffset Timestamp,
    PrecioTokens? Precio = null);

/// <summary>Precio por 1000 tokens (staging) para estimar el costo del corrido; opcional.</summary>
public sealed record PrecioTokens(decimal PorMilPrompt, decimal PorMilCompletion);

/// <summary>Distribución de un score (un criterio/eje, o el total) sobre las muestras válidas.</summary>
public sealed record DistribucionEje(
    string Eje,
    int Muestras,
    decimal Min,
    decimal Max,
    double Media,
    double Desviacion);

/// <summary>Conteo de la decisión sugerida por el modelo, sobre muestras válidas (no fallback).</summary>
public sealed record ConteoDecisiones(int Cerrar, int Repreguntar)
{
    public int Total => Cerrar + Repreguntar;

    public double ProporcionCerrar => Total == 0 ? 0d : (double)Cerrar / Total;
}

/// <summary>Un motivo de fallback y cuántas veces ocurrió (p. ej. <c>salida_invalida:no_json</c>).</summary>
public sealed record MotivoInvalido(string Motivo, int Conteo);

/// <summary>% de salida inválida (tasa de fallback) con desglose por motivo (D5 §3.2).</summary>
public sealed record ResumenInvalidos(
    int TotalMuestras,
    int Invalidas,
    double Porcentaje,
    IReadOnlyList<MotivoInvalido> PorMotivo);

/// <summary>Tokens por entrada (suma de sus repeticiones) para el corrido.</summary>
public sealed record UsoTokensEntrada(string EntradaId, int PromptTokens, int CompletionTokens)
{
    public int Total => PromptTokens + CompletionTokens;
}

/// <summary>Tokens totales del corrido + costo estimado (si hay precio) + desglose por entrada.</summary>
public sealed record ResumenTokens(
    int PromptTokens,
    int CompletionTokens,
    int Total,
    decimal? CostoEstimado,
    IReadOnlyList<UsoTokensEntrada> PorEntrada);

/// <summary>Ideas/temas/entidades distintas detectadas para una entrada a lo largo de sus repeticiones.</summary>
public sealed record IdeasEntrada(string EntradaId, string Categoria, IReadOnlyList<string> Ideas);
