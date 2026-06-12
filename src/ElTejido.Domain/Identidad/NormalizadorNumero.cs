using System.Text;
using ElTejido.Domain.Common;

namespace ElTejido.Domain.Identidad;

public sealed class NormalizadorNumero : INormalizadorNumero
{
    public NumeroWhatsApp Normalizar(string numeroCrudo)
    {
        if (string.IsNullOrWhiteSpace(numeroCrudo))
        {
            throw new DomainValidationException(
                "NUMERO_E164_INVALIDO",
                "El numero de WhatsApp es obligatorio.");
        }

        var digits = new StringBuilder(numeroCrudo.Length);

        foreach (var character in numeroCrudo)
        {
            if (character is >= '0' and <= '9')
            {
                digits.Append(character);
            }
        }

        return NumeroWhatsApp.FromNormalized(digits.ToString());
    }

    public bool TryNormalizar(string numeroCrudo, out NumeroWhatsApp? numero)
    {
        try
        {
            numero = Normalizar(numeroCrudo);
            return true;
        }
        catch (DomainValidationException)
        {
            numero = null;
            return false;
        }
    }
}

