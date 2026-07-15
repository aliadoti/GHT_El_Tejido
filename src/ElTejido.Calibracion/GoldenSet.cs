namespace ElTejido.Calibracion;

/// <summary>
/// Banco de calibración (D5): conjunto versionado de respuestas representativas usadas como golden set
/// para medir la regresión de comportamiento del LLM antes de tocar prompts/rúbrica/umbral. Es
/// <b>dato</b> (se amplía sin tocar el harness), no lógica. Se carga desde un JSON versionado
/// (p. ej. <c>tests/Calibracion/golden-set.json</c>) con <see cref="CargadorGoldenSet"/>.
/// </summary>
public sealed record GoldenSet(
    string Version,
    string? Descripcion,
    IReadOnlyList<EntradaGoldenSet> Entradas);

/// <summary>
/// Una entrada del golden set (03 §3.1 de la spec D5): un texto de respuesta representativo con el
/// comportamiento esperado. El texto es <b>dato no confiable</b> (puede contener instrucciones
/// embebidas de prompt-injection); el harness lo trata como respuesta del participante, nunca como
/// instrucción.
/// </summary>
public sealed record EntradaGoldenSet(
    string Id,
    string Categoria,
    string TextoRespuesta,
    EsperadoGoldenSet? Esperado,
    string? Notas);

/// <summary>
/// Comportamiento esperado de una entrada (referencia cualitativa, no aserción dura: el modelo es no
/// determinista). Todos los campos son opcionales salvo <see cref="EsHostil"/>; sirven para anotar y
/// para comparaciones futuras, no para forzar un único resultado.
/// </summary>
public sealed record EsperadoGoldenSet(
    string? EjeDebil,
    string? Decision,
    IReadOnlyList<string>? IdeasEsperadas,
    bool EsHostil);

/// <summary>Decisiones válidas del eje cerrar/repreguntar (08 §4), en minúscula como en el contrato.</summary>
public static class DecisionCalibracion
{
    public const string Cerrar = "cerrar";
    public const string Repreguntar = "repreguntar";

    public static bool EsValida(string? valor)
        => valor is Cerrar or Repreguntar;
}
