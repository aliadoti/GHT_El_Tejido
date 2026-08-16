namespace ElTejido.Domain.Configuracion;

/// <summary>
/// Integridad estructural de una version de rubrica (03 §3.11, DT-RUB-01 §3.2). Es una lectura
/// derivada, no una decision del autor: describe si la version se puede usar como fuente canonica.
/// </summary>
public enum EstadoIntegridadRubrica
{
    /// <summary>
    /// Estructura valida y <c>contenidoMarkdown</c> igual a la proyeccion compilada desde ella. Es la
    /// unica integridad que habilita asignar la version a una campania nueva o activarla.
    /// </summary>
    Valida = 0,

    /// <summary>
    /// Estructura valida pero el Markdown persistido no proviene del compilador: no se puede afirmar
    /// que ambas representaciones digan lo mismo. Se lee y se sigue evaluando con lo ya configurado,
    /// pero no habilita una asignacion o activacion nueva hasta crear una version estructurada.
    /// </summary>
    LegacyNoVerificada = 1,

    /// <summary>
    /// La estructura no cumple las reglas canonicas (sin criterios, ids/nombres repetidos, pesos que
    /// no suman uno, orden ambiguo o escala invalida). Se lee para no perder historia; no se activa.
    /// </summary>
    Invalida = 2,
}
