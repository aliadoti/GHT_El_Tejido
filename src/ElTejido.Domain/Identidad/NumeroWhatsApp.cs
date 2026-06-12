using ElTejido.Domain.Common;

namespace ElTejido.Domain.Identidad;

public sealed record NumeroWhatsApp
{
    private NumeroWhatsApp(string valor)
    {
        Valor = valor;
    }

    public string Valor { get; }

    public static NumeroWhatsApp FromNormalized(string valor)
    {
        if (!IsPlausibleE164Digits(valor))
        {
            throw new DomainValidationException(
                "NUMERO_E164_INVALIDO",
                "El numero debe estar en formato E.164 sin simbolos.");
        }

        return new NumeroWhatsApp(valor);
    }

    public override string ToString() => Valor;

    private static bool IsPlausibleE164Digits(string value)
    {
        if (value.Length is < 8 or > 15 || value[0] == '0')
        {
            return false;
        }

        foreach (var character in value)
        {
            if (character is < '0' or > '9')
            {
                return false;
            }
        }

        return true;
    }
}

