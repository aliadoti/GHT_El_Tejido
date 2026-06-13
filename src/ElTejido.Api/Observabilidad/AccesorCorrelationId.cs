namespace ElTejido.Api.Observabilidad;

/// <summary>
/// Acceso al correlationId de la peticion. Si el middleware aun no lo establecio (p. ej. un
/// fallo muy temprano), genera y guarda uno para garantizar que toda respuesta de error lo lleve
/// (04 §8, 10 §6.2).
/// </summary>
internal static class AccesorCorrelationId
{
    public static string ObtenerOCrear(HttpContext context)
    {
        if (context.Items.TryGetValue(CorrelacionConstantes.ClaveItems, out var valor)
            && valor is string existente
            && !string.IsNullOrWhiteSpace(existente))
        {
            return existente;
        }

        var nuevo = Generar();
        context.Items[CorrelacionConstantes.ClaveItems] = nuevo;
        return nuevo;
    }

    public static string Generar() => CorrelacionConstantes.Prefijo + Guid.NewGuid().ToString("N");
}
