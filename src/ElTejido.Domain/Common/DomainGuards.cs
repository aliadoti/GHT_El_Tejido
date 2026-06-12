namespace ElTejido.Domain.Common;

internal static class DomainGuards
{
    public static string Required(string value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainValidationException(
                "CAMPO_OBLIGATORIO",
                $"El campo {field} es obligatorio.");
        }

        return value.Trim();
    }
}

