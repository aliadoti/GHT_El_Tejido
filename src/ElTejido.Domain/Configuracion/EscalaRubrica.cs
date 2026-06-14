using ElTejido.Domain.Common;

namespace ElTejido.Domain.Configuracion;

public sealed record EscalaRubrica(int Min, int Max)
{
    public static EscalaRubrica Crear(int min, int max)
    {
        if (min >= max)
        {
            throw new DomainValidationException(
                "ESCALA_INVALIDA",
                "La escala de rubrica debe tener minimo menor que maximo.");
        }

        return new EscalaRubrica(min, max);
    }
}
