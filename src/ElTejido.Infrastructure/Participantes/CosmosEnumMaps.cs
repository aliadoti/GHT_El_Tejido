using ElTejido.Domain.Common;
using ElTejido.Domain.Participantes;

namespace ElTejido.Infrastructure.Participantes;

/// <summary>
/// Mapeo de enums de dominio del contenedor participants al contrato JSON de 03 §3.4-3.5.
/// </summary>
internal static class CosmosEnumMaps
{
    public static string FromEstadoRegistro(EstadoRegistro estado)
    {
        return estado switch
        {
            EstadoRegistro.Activo => "activo",
            EstadoRegistro.Inactivo => "inactivo",
            _ => throw new InvalidOperationException($"Estado de registro no soportado: {estado}."),
        };
    }

    public static EstadoRegistro ToEstadoRegistro(string estado)
    {
        return estado switch
        {
            "activo" => EstadoRegistro.Activo,
            "inactivo" => EstadoRegistro.Inactivo,
            _ => throw new InvalidOperationException($"Estado de registro no soportado en Cosmos: {estado}."),
        };
    }

    public static string FromEstadoEnvio(EstadoEnvio estado)
    {
        return estado switch
        {
            EstadoEnvio.Pendiente => "pendiente",
            EstadoEnvio.Enviado => "enviado",
            EstadoEnvio.Error => "error",
            _ => throw new InvalidOperationException($"Estado de envio no soportado: {estado}."),
        };
    }

    public static EstadoEnvio ToEstadoEnvio(string estado)
    {
        return estado switch
        {
            "pendiente" => EstadoEnvio.Pendiente,
            "enviado" => EstadoEnvio.Enviado,
            "error" => EstadoEnvio.Error,
            _ => throw new InvalidOperationException($"Estado de envio no soportado en Cosmos: {estado}."),
        };
    }

    public static string FromEstadoRespuesta(EstadoRespuestaParticipante estado)
    {
        return estado switch
        {
            EstadoRespuestaParticipante.SinRespuesta => "sinRespuesta",
            EstadoRespuestaParticipante.Respondio => "respondio",
            _ => throw new InvalidOperationException($"Estado de respuesta no soportado: {estado}."),
        };
    }

    public static EstadoRespuestaParticipante ToEstadoRespuesta(string estado)
    {
        return estado switch
        {
            "sinRespuesta" => EstadoRespuestaParticipante.SinRespuesta,
            "respondio" => EstadoRespuestaParticipante.Respondio,
            _ => throw new InvalidOperationException($"Estado de respuesta no soportado en Cosmos: {estado}."),
        };
    }

    public static string FromTipoEnvio(TipoEnvioMensaje tipo)
    {
        return tipo switch
        {
            TipoEnvioMensaje.Inicial => "Inicial",
            TipoEnvioMensaje.Reenvio => "Reenvio",
            TipoEnvioMensaje.Repregunta => "Repregunta",
            TipoEnvioMensaje.Cierre => "Cierre",
            TipoEnvioMensaje.Autenticacion => "Autenticacion",
            _ => throw new InvalidOperationException($"Tipo de envio no soportado: {tipo}."),
        };
    }

    public static TipoEnvioMensaje ToTipoEnvio(string tipo)
    {
        return tipo switch
        {
            "Inicial" => TipoEnvioMensaje.Inicial,
            "Reenvio" => TipoEnvioMensaje.Reenvio,
            "Repregunta" => TipoEnvioMensaje.Repregunta,
            "Cierre" => TipoEnvioMensaje.Cierre,
            "Autenticacion" => TipoEnvioMensaje.Autenticacion,
            _ => throw new InvalidOperationException($"Tipo de envio no soportado en Cosmos: {tipo}."),
        };
    }
}
