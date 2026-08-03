using ElTejido.Domain.Common;

namespace ElTejido.Domain.Conversaciones;

/// <summary>
/// P-27: estado mínimo y sin texto para la aclaración determinista de una intención de salida.
/// </summary>
public sealed class IntencionControlPendiente
{
    private IntencionControlPendiente(int intentosInvalidos, DateTimeOffset creadoEn)
    {
        IntentosInvalidos = intentosInvalidos;
        CreadoEn = creadoEn;
    }

    public string Tipo => "aclararSalida";

    public int IntentosInvalidos { get; }

    public DateTimeOffset CreadoEn { get; }

    public static IntencionControlPendiente Crear(int intentosInvalidos, DateTimeOffset creadoEn)
    {
        if (intentosInvalidos < 0)
        {
            throw new DomainValidationException(
                "INTENTOS_INVALIDOS_SALIDA_INVALIDOS",
                "Los intentos inválidos de la aclaración de salida no pueden ser negativos.");
        }

        return new IntencionControlPendiente(intentosInvalidos, creadoEn.ToUniversalTime());
    }
}
