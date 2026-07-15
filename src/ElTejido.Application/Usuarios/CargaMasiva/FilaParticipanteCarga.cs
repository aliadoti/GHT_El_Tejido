namespace ElTejido.Application.Usuarios.CargaMasiva;

/// <summary>
/// Una fila cruda del archivo de carga masiva (I-08), ya separada en columnas pero <b>sin validar</b>.
/// El <see cref="Numero"/> llega tal cual del archivo; la normalizacion E.164 (06 §2) y la validacion
/// las hace <c>ServicioCargaMasiva</c>. <see cref="Fila"/> es el numero de linea 1-based del archivo
/// (la cabecera es la fila 1) para poder reportar por fila sin exponer PII.
/// </summary>
public sealed record FilaParticipanteCarga(
    int Fila,
    string? Nombre,
    string? Numero,
    string? Area,
    string? Empresa,
    IReadOnlyCollection<string> Tags);
