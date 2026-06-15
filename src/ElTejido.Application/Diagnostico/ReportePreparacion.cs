namespace ElTejido.Application.Diagnostico;

/// <summary>
/// Resultado de una comprobacion individual. <paramref name="Detalle"/> es texto seguro para
/// mostrar al operador (descripcion del estado); nunca contiene valores de secretos ni PII.
/// </summary>
public sealed record ResultadoComprobacion(string Componente, EstadoPreparacion Estado, string Detalle);

/// <summary>
/// Reporte agregado de preparacion del despliegue. <paramref name="Estado"/> resume el conjunto:
/// <see cref="EstadoPreparacion.Error"/> si alguna comprobacion fallo, si no
/// <see cref="EstadoPreparacion.Faltante"/> si alguna falta, en otro caso <see cref="EstadoPreparacion.Ok"/>.
/// Las comprobaciones <see cref="EstadoPreparacion.NoAplica"/> no afectan el agregado.
/// </summary>
public sealed record ReportePreparacion(EstadoPreparacion Estado, IReadOnlyList<ResultadoComprobacion> Componentes);
