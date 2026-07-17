namespace ElTejido.Application.Conversacion;

/// <summary>
/// Aporte anonimizado de otro participante de la misma campaña, recuperado por la base de conocimiento
/// colectiva (I-09, 05 §4.8). <b>Solo lleva el resumen anonimizado</b> — nunca el Markdown completo ni
/// el nombre/número del autor: el <see cref="Resumen"/> se deriva de <c>Evaluacion.temas ∪ entidades</c>
/// + un extracto sanitizado de <c>Respuesta.texto</c> (decisión de diseño 2026-07-15). Se inyecta como
/// <b>dato no confiable</b> delimitado (08 §3.2); jamás como instrucción.
/// </summary>
public sealed record AporteRelevante(
    string Resumen,
    IReadOnlyList<string> Tags,
    DateTimeOffset Fecha);
