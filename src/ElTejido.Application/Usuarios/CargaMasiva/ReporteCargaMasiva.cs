namespace ElTejido.Application.Usuarios.CargaMasiva;

/// <summary>
/// Resultado por fila del lote (I-08). <see cref="Resultado"/> ∈ <c>creado|actualizado|rechazado</c>.
/// <see cref="Motivo"/> solo se llena cuando <see cref="Resultado"/> es <c>rechazado</c>. No contiene
/// PII: solo el <see cref="UsuarioId"/> (cuando aplica), el numero de fila y un motivo tipificado.
/// </summary>
public sealed record ResultadoFilaCarga(
    int Fila,
    string Resultado,
    string? UsuarioId,
    string? Motivo);

/// <summary>
/// Reporte agregado de una carga masiva (I-08): conteos + el detalle por fila. Una fila mala no aborta
/// el lote; queda como <c>rechazado</c> con su motivo.
/// </summary>
public sealed record ReporteCargaMasiva(
    int TotalFilas,
    int Creados,
    int Actualizados,
    int Rechazados,
    int Asociados,
    IReadOnlyList<ResultadoFilaCarga> Filas);

/// <summary>Resultados posibles de una fila.</summary>
public static class ResultadoCarga
{
    public const string Creado = "creado";
    public const string Actualizado = "actualizado";
    public const string Rechazado = "rechazado";
}

/// <summary>Motivos tipificados de rechazo (sin PII).</summary>
public static class MotivoRechazoCarga
{
    public const string FilaIncompleta = "fila_incompleta";
    public const string NumeroInvalido = "numero_invalido";
    public const string DuplicadoEnArchivo = "duplicado_en_archivo";
}
