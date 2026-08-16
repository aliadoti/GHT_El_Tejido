using ElTejido.Domain.Configuracion;
using ElTejido.Domain.Evaluacion;

namespace ElTejido.Application.Evaluacion;

/// <summary>
/// Resultado de contrastar la salida del modelo contra la version efectiva. <see cref="Motivo"/> es
/// <c>null</c> cuando la salida trae exactamente los ids esperados; en cualquier otro caso lleva uno
/// de los codigos estables de 08 §7 y <see cref="Calificaciones"/> queda vacio.
/// </summary>
public sealed record ResultadoContratoSalidaRubrica(
    IReadOnlyList<CalificacionCriterio> Calificaciones,
    string? Motivo)
{
    public bool Valido => Motivo is null;
}

/// <summary>
/// Contrato exacto de la salida del LLM contra la rubrica efectiva (08 §4.1, DT-RUB-01 §7).
/// <para>
/// El emparejamiento es <b>por <c>criterio_id</c></b>, nunca por el texto visible: el nombre lo pone
/// el snapshot del servidor, no el modelo. La salida es valida solo si contiene exactamente los ids
/// de la version —ninguno faltante, ninguno adicional, ninguno duplicado—, cada puntaje dentro de la
/// escala y una justificacion no vacia por criterio. Cualquier desviacion devuelve un codigo estable
/// y el llamador aplica el fallback seguro existente (08 §6): no se inventan notas parciales ni se
/// reintenta la llamada.
/// </para>
/// </summary>
public static class ContratoSalidaRubrica
{
    /// <summary>Tope de la justificacion por criterio; recorta superficie sin cambiar la decision.</summary>
    private const int MaxCaracteresJustificacion = 600;

    public static ResultadoContratoSalidaRubrica Emparejar(
        IReadOnlyList<SalidaCalificacionCriterio>? calificaciones,
        Rubrica rubrica)
    {
        var esperados = rubrica.Criterios
            .OrderBy(c => c.Orden)
            .ToDictionary(c => c.Id, StringComparer.Ordinal);

        var recibidas = calificaciones ?? Array.Empty<SalidaCalificacionCriterio>();
        var vistos = new HashSet<string>(StringComparer.Ordinal);
        var porId = new Dictionary<string, SalidaCalificacionCriterio>(StringComparer.Ordinal);

        foreach (var recibida in recibidas)
        {
            var id = recibida.CriterioId?.Trim() ?? string.Empty;
            if (id.Length == 0 || !esperados.ContainsKey(id))
            {
                return Rechazo("criterio_extra");
            }

            if (!vistos.Add(id))
            {
                return Rechazo("criterio_duplicado");
            }

            if (recibida.Puntaje < rubrica.Escala.Min || recibida.Puntaje > rubrica.Escala.Max)
            {
                return Rechazo("puntaje_fuera_escala");
            }

            if (string.IsNullOrWhiteSpace(recibida.Justificacion))
            {
                return Rechazo("justificacion_vacia");
            }

            porId[id] = recibida;
        }

        if (vistos.Count != esperados.Count)
        {
            return Rechazo("criterio_faltante");
        }

        // El orden de salida es el de la rubrica, no el que haya devuelto el modelo: asi el snapshot
        // persistido y el reporte son reproducibles entre corridas.
        var resultado = esperados.Values
            .Select(criterio => CalificacionCriterio.Crear(
                criterio.Id,
                criterio.Nombre,
                porId[criterio.Id].Puntaje,
                Acotar(porId[criterio.Id].Justificacion!)))
            .ToArray();

        return new ResultadoContratoSalidaRubrica(resultado, null);
    }

    /// <summary>
    /// Total de negocio calculado por el servidor (08 §4.1): <c>sum(puntaje * peso) / sum(peso)</c>
    /// en <see cref="decimal"/> y <b>sin redondear</b>, para no perder precision antes de aplicar
    /// umbrales o clasificar madurez. El formato de presentacion se decide aguas abajo.
    /// </summary>
    public static decimal CalcularTotalPonderado(
        IReadOnlyList<CalificacionCriterio> calificaciones,
        IReadOnlyList<CriterioRubrica> criterios)
    {
        var pesos = criterios.ToDictionary(c => c.Id, c => c.Peso, StringComparer.Ordinal);

        decimal numerador = 0m;
        decimal denominador = 0m;
        foreach (var calificacion in calificaciones)
        {
            if (!pesos.TryGetValue(calificacion.CriterioId, out var peso))
            {
                continue;
            }

            numerador += calificacion.Puntaje * peso;
            denominador += peso;
        }

        return denominador == 0m ? 0m : numerador / denominador;
    }

    private static ResultadoContratoSalidaRubrica Rechazo(string motivo)
        => new(Array.Empty<CalificacionCriterio>(), motivo);

    private static string Acotar(string justificacion)
    {
        var normalizada = justificacion.Trim();
        return normalizada.Length <= MaxCaracteresJustificacion
            ? normalizada
            : normalizada[..MaxCaracteresJustificacion];
    }
}
