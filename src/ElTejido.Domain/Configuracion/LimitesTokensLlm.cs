using ElTejido.Domain.Common;

namespace ElTejido.Domain.Configuracion;

public sealed record LimitesTokensLlm(int MaxPrompt, int MaxCompletion)
{
    public static LimitesTokensLlm Crear(int maxPrompt, int maxCompletion)
    {
        if (maxPrompt <= 0 || maxCompletion <= 0)
        {
            throw new DomainValidationException(
                "LIMITES_TOKENS_INVALIDOS",
                "Los limites de tokens deben ser mayores que cero.");
        }

        return new LimitesTokensLlm(maxPrompt, maxCompletion);
    }
}
