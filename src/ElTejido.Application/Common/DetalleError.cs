namespace ElTejido.Application.Common;

/// <summary>
/// Detalle granular de un error, alineado con el arreglo <c>details</c> del modelo de
/// errores uniforme (04 §3): cada item identifica el campo afectado y el problema.
/// </summary>
/// <param name="Campo">Campo del request afectado; puede ser nulo cuando el error no es de un campo concreto.</param>
/// <param name="Problema">Descripcion corta y estable del problema (no PII, no secretos).</param>
public sealed record DetalleError(string? Campo, string Problema);
