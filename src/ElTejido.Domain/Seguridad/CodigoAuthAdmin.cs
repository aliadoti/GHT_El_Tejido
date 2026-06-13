using ElTejido.Domain.Common;
using ElTejido.Domain.Identidad;

namespace ElTejido.Domain.Seguridad;

/// <summary>
/// Codigo OTP de un solo uso para login administrativo (contenedor security, TTL nativo).
/// Cubre 03 seccion 3.14, 06 seccion 4 y REQ 10.3.
/// </summary>
public sealed class CodigoAuthAdmin
{
    private CodigoAuthAdmin(
        string id,
        string usuarioId,
        NumeroWhatsApp numero,
        string hashCodigo,
        DateTimeOffset expiracion,
        int intentosRestantes,
        bool usado,
        DateTimeOffset creadoEn,
        int ttl)
    {
        Id = id;
        UsuarioId = usuarioId;
        Numero = numero;
        HashCodigo = hashCodigo;
        Expiracion = expiracion;
        IntentosRestantes = intentosRestantes;
        Usado = usado;
        CreadoEn = creadoEn;
        Ttl = ttl;
    }

    public string Id { get; }

    public string UsuarioId { get; }

    public NumeroWhatsApp Numero { get; }

    public string HashCodigo { get; }

    public DateTimeOffset Expiracion { get; }

    public int IntentosRestantes { get; }

    public bool Usado { get; }

    public DateTimeOffset CreadoEn { get; }

    public int Ttl { get; }

    public bool EstaExpirado(DateTimeOffset ahora) => ahora.ToUniversalTime() >= Expiracion;

    public bool EsVigente(DateTimeOffset ahora) => !Usado && IntentosRestantes > 0 && !EstaExpirado(ahora);

    public static CodigoAuthAdmin Crear(
        string id,
        string usuarioId,
        NumeroWhatsApp numero,
        string hashCodigo,
        DateTimeOffset expiracion,
        int intentosRestantes,
        bool usado,
        DateTimeOffset creadoEn,
        int ttl)
    {
        if (intentosRestantes < 0)
        {
            throw new DomainValidationException(
                "INTENTOS_INVALIDOS",
                "Los intentos restantes no pueden ser negativos.");
        }

        if (ttl <= 0)
        {
            throw new DomainValidationException(
                "TTL_INVALIDO",
                "El ttl del codigo OTP debe ser mayor que cero.");
        }

        var creadoEnUtc = creadoEn.ToUniversalTime();
        var expiracionUtc = expiracion.ToUniversalTime();

        if (expiracionUtc <= creadoEnUtc)
        {
            throw new DomainValidationException(
                "EXPIRACION_INVALIDA",
                "La expiracion debe ser posterior a la fecha de creacion.");
        }

        return new CodigoAuthAdmin(
            DomainGuards.Required(id, nameof(id)),
            DomainGuards.Required(usuarioId, nameof(usuarioId)),
            numero,
            DomainGuards.Required(hashCodigo, nameof(hashCodigo)),
            expiracionUtc,
            intentosRestantes,
            usado,
            creadoEnUtc,
            ttl);
    }

    /// <summary>Devuelve una copia con un intento menos (REQ 10.3.4).</summary>
    public CodigoAuthAdmin ConIntentoConsumido()
    {
        var restantes = IntentosRestantes > 0 ? IntentosRestantes - 1 : 0;
        return new CodigoAuthAdmin(
            Id,
            UsuarioId,
            Numero,
            HashCodigo,
            Expiracion,
            restantes,
            Usado,
            CreadoEn,
            Ttl);
    }

    /// <summary>Devuelve una copia marcada como usada (un solo uso, REQ 10.3.4).</summary>
    public CodigoAuthAdmin ComoUsado()
    {
        return new CodigoAuthAdmin(
            Id,
            UsuarioId,
            Numero,
            HashCodigo,
            Expiracion,
            IntentosRestantes,
            true,
            CreadoEn,
            Ttl);
    }
}
