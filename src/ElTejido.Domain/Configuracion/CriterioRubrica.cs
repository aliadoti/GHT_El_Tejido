using ElTejido.Domain.Common;

namespace ElTejido.Domain.Configuracion;

/// <summary>
/// Criterio de una version de rubrica (03 §3.11, DT-RUB-01 §3.1).
/// <para>
/// <paramref name="Id"/> es la clave estable de emparejamiento con la salida del LLM (08 §4.1) y con
/// el snapshot de la evaluacion; <paramref name="Nombre"/> es solo la etiqueta visible y puede tener
/// espacios y tildes. <paramref name="Orden"/> determina el preview y el orden del contrato enviado
/// al modelo; el valor <c>0</c> significa "sin orden explicito" y lo resuelve
/// <see cref="ValidadorRubricaEstructurada.NormalizarOrden"/> por posicion.
/// </para>
/// </summary>
public sealed record CriterioRubrica(string Id, string Nombre, string Descripcion, decimal Peso, int Orden)
{
    /// <summary>Crea un criterio completo. El id debe venir ya en forma canonica.</summary>
    public static CriterioRubrica Crear(string id, string nombre, string descripcion, decimal peso, int orden)
    {
        if (peso <= 0)
        {
            throw new DomainValidationException(
                "PESO_CRITERIO_INVALIDO",
                "El peso del criterio debe ser mayor que cero.");
        }

        if (orden < 0)
        {
            throw new DomainValidationException(
                "ORDEN_CRITERIO_INVALIDO",
                "El orden del criterio no puede ser negativo.");
        }

        return new CriterioRubrica(
            DomainGuards.Required(id, nameof(id)),
            DomainGuards.Required(nombre, nameof(nombre)),
            descripcion?.Trim() ?? string.Empty,
            peso,
            orden);
    }

    /// <summary>
    /// Deriva el criterio a partir del nombre visible: el id se normaliza desde ese nombre y la
    /// descripcion queda vacia. Es la forma de rehidratar un documento historico que no persistio
    /// <c>id</c> (03 §3.11, compatibilidad de lectura) y de armar fixtures minimos; una escritura
    /// nueva por API siempre entrega el id explicito.
    /// </summary>
    public static CriterioRubrica Crear(string nombre, decimal peso, int orden = 0)
        => Crear(NormalizacionRubrica.NormalizarId(nombre), nombre, string.Empty, peso, orden);
}
