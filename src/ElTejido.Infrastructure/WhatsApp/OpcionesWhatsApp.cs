using ElTejido.Application.Seguridad;

namespace ElTejido.Infrastructure.WhatsApp;

/// <summary>
/// Configuracion de la seccion <c>WhatsApp</c> (02 §6, 05 §2.6). Solo valores no secretos y los
/// <b>nombres</b> de los secretos en Key Vault (nunca sus valores). Los nombres por defecto
/// coinciden con los canonicos de la guia de Azure (<see cref="NombresSecretos"/>).
/// </summary>
public sealed class OpcionesWhatsApp
{
    public const string Seccion = "WhatsApp";

    /// <summary>Base de Graph API, p. ej. <c>https://graph.facebook.com/v20.0</c>.</summary>
    public string GraphApiBaseUrl { get; set; } = "https://graph.facebook.com/v20.0";

    /// <summary>Id del numero de telefono de WhatsApp Business.</summary>
    public string PhoneNumberId { get; set; } = string.Empty;

    /// <summary>Numeros disponibles por alias logico. Si queda vacio se conserva <see cref="PhoneNumberId"/>.</summary>
    public IReadOnlyList<NumeroWhatsAppConfigurado> Numeros { get; set; } = [];

    /// <summary>Alias usado cuando el envio no solicita un emisor concreto.</summary>
    public string AliasPredeterminado { get; set; } = string.Empty;

    /// <summary>
    /// Resuelve un alias de campania o un id de Meta recibido por webhook. La configuracion legacy de
    /// un unico <see cref="PhoneNumberId"/> sigue siendo el predeterminado.
    /// </summary>
    public string ResolverPhoneNumberId(string? emisor, out bool encontrado)
    {
        var numeros = Numeros
            .Where(numero => !string.IsNullOrWhiteSpace(numero.Alias) && !string.IsNullOrWhiteSpace(numero.PhoneNumberId))
            .ToArray();
        if (numeros.Length == 0)
        {
            encontrado = string.IsNullOrWhiteSpace(emisor) || string.Equals(emisor, PhoneNumberId, StringComparison.Ordinal);
            return PhoneNumberId;
        }

        var predeterminado = numeros.FirstOrDefault(numero =>
                                  string.Equals(numero.Alias, AliasPredeterminado, StringComparison.OrdinalIgnoreCase))
                              ?? numeros[0];
        if (string.IsNullOrWhiteSpace(emisor))
        {
            encontrado = true;
            return predeterminado.PhoneNumberId;
        }

        var seleccionado = numeros.FirstOrDefault(numero =>
            string.Equals(numero.Alias, emisor, StringComparison.OrdinalIgnoreCase)
            || string.Equals(numero.PhoneNumberId, emisor, StringComparison.Ordinal));
        encontrado = seleccionado is not null;
        return seleccionado?.PhoneNumberId ?? predeterminado.PhoneNumberId;
    }

    /// <summary>Nombre del secreto del token de verificacion del webhook (Meta).</summary>
    public string VerifyTokenSecretName { get; set; } = NombresSecretos.WaVerifyToken;

    /// <summary>Nombre del secreto del app secret (firma del webhook).</summary>
    public string AppSecretSecretName { get; set; } = NombresSecretos.WaAppSecret;

    /// <summary>Nombre del secreto del token de acceso (Authorization: Bearer).</summary>
    public string AccessTokenSecretName { get; set; } = NombresSecretos.WaToken;

    /// <summary>Maximo de reintentos ante errores transitorios de Meta (5xx / 429) (10 §2).</summary>
    public int MaxReintentos { get; set; } = 2;

    /// <summary>Base del backoff exponencial entre reintentos, en milisegundos.</summary>
    public int BackoffBaseMs { get; set; } = 200;

    /// <summary>Pausa entre envios del trabajador masivo para respetar los limites de Meta (ARQ §4.4).</summary>
    public int ThrottleEnvioMs { get; set; } = 0;
}

public sealed class NumeroWhatsAppConfigurado
{
    public string Alias { get; set; } = string.Empty;

    public string PhoneNumberId { get; set; } = string.Empty;
}
