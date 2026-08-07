namespace ElTejido.Application.Usuarios.CargaMasiva;

/// <summary>
/// Resultado por fila del lote (I-08 §4.4, 04 §5.1).
/// <see cref="Resultado"/> ∈ <c>creado|actualizado|reasignado|rechazado</c>; <see cref="Motivo"/> solo
/// se llena cuando es <c>rechazado</c>.
/// <para>
/// Los campos <c>...Anterior</c> y los nombres solo aparecen en un <c>conflicto_titular</c> o en una
/// reasignacion, para que el portal pueda mostrar <i>actual vs. propuesto</i> y el admin decida por
/// fila. Van en la <b>respuesta al admin</b>, nunca en la auditoria: el log se queda con conteos y
/// motivos (10 §seguridad).
/// </para>
/// </summary>
public sealed record ResultadoFilaCarga(
    int Fila,
    string Resultado,
    string? UsuarioId,
    string? Motivo,
    int? CodigoUsuario = null,
    string? UsuarioIdAnterior = null,
    int? CodigoUsuarioAnterior = null,
    string? NombreActual = null,
    string? NombrePropuesto = null);

/// <summary>
/// Reporte agregado de una carga masiva (I-08). Una fila mala no aborta el lote; queda como
/// <c>rechazado</c> con su motivo tipificado.
/// </summary>
public sealed record ReporteCargaMasiva(
    int TotalFilas,
    int Creados,
    int Actualizados,
    int Reasignados,
    int Rechazados,
    int Asociados,
    IReadOnlyList<ResultadoFilaCarga> Filas);

/// <summary>Resultados posibles de una fila.</summary>
public static class ResultadoCarga
{
    public const string Creado = "creado";
    public const string Actualizado = "actualizado";
    public const string Reasignado = "reasignado";
    public const string Rechazado = "rechazado";
}

/// <summary>Motivos tipificados de rechazo (sin PII), I-08 §4.</summary>
public static class MotivoRechazoCarga
{
    /// <summary>Falta <c>Nombre</c> o <c>Telefono</c>, los dos unicos obligatorios.</summary>
    public const string FilaIncompleta = "fila_incompleta";

    public const string NumeroInvalido = "numero_invalido";
    public const string EmailInvalido = "email_invalido";

    /// <summary>Mismo telefono repetido dentro del archivo; el primero gana.</summary>
    public const string DuplicadoEnArchivo = "duplicado_en_archivo";

    /// <summary>El email ya pertenece a otro usuario <b>activo</b>.</summary>
    public const string EmailDuplicado = "email_duplicado";

    /// <summary>Telefono existente con un nombre claramente distinto: lo resuelve el admin (§4.4).</summary>
    public const string ConflictoTitular = "conflicto_titular";

    public const string IdiomaInvalido = "idioma_invalido";
    public const string AntiguedadInvalida = "antiguedad_invalida";

    /// <summary>Solo en <c>modo=solo_actualizar</c>: no hay usuario activo con ese telefono.</summary>
    public const string NoEncontrado = "no_encontrado";

    /// <summary>Se inactivo al titular pero fallo el alta del nuevo y no se pudo revertir (§6).</summary>
    public const string ReasignacionIncompleta = "reasignacion_incompleta";
}

/// <summary>Modos de carga (I-08 §4.3).</summary>
public static class ModoCargaMasiva
{
    /// <summary>Crea los que no existen y actualiza los que si. Es el modo por defecto.</summary>
    public const string Upsert = "upsert";

    /// <summary>Solo actualiza por telefono; una fila sin usuario activo se rechaza sin crear nada.</summary>
    public const string SoloActualizar = "solo_actualizar";

    public static bool EsValido(string modo)
        => modo is Upsert or SoloActualizar;
}

/// <summary>Acciones con las que el admin resuelve un <c>conflicto_titular</c> (I-08 §4.4).</summary>
public static class AccionConflictoTitular
{
    /// <summary>Era un typo: actualiza el registro existente conservando <c>id</c> y <c>codigoUsuario</c>.</summary>
    public const string CorregirNombre = "corregir_nombre";

    /// <summary>Cambio de titular: inactiva al actual y crea un usuario nuevo con el mismo numero.</summary>
    public const string Reasignar = "reasignar";

    /// <summary>Dejar la fila sin tocar; sigue reportandose como rechazada.</summary>
    public const string Omitir = "omitir";

    public static bool EsValida(string accion)
        => accion is CorregirNombre or Reasignar or Omitir;
}

/// <summary>Decision del admin para una fila en conflicto, en la segunda pasada (I-08 §4.4).</summary>
public sealed record ResolucionConflictoTitular(int Fila, string Accion);
