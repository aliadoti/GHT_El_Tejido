using ElTejido.Domain.Common;

namespace ElTejido.Domain.Configuracion;

public sealed record CriterioRubrica(string Nombre, decimal Peso)
{
    public static CriterioRubrica Crear(string nombre, decimal peso)
    {
        if (peso <= 0)
        {
            throw new DomainValidationException(
                "PESO_CRITERIO_INVALIDO",
                "El peso del criterio debe ser mayor que cero.");
        }

        return new CriterioRubrica(DomainGuards.Required(nombre, nameof(nombre)), peso);
    }
}
