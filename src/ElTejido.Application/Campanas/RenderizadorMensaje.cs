using System.Globalization;
using ElTejido.Domain.Campanas;
using ElTejido.Domain.Common;
using ElTejido.Domain.Usuarios;

namespace ElTejido.Application.Campanas;

/// <summary>
/// Renderiza el texto de un <see cref="MensajeInicial"/> de campania reemplazando las variables
/// <c>{{clave}}</c> con los datos del usuario y de la campania (REQ §15.3). Compartido por el envio
/// masivo (<c>ServicioEnvios</c>) y por el saludo del primer entrante del orquestador, para que ambos
/// usen exactamente las mismas variables y el mismo texto guardado en la base de datos de la campania.
/// </summary>
public static class RenderizadorMensaje
{
    /// <summary>
    /// Mensaje inicial activo de la campania (el de menor <c>Orden</c>), o <c>null</c> si la campania
    /// no tiene ninguno activo. Es la fuente del saludo de primer contacto guardado por campania.
    /// </summary>
    public static MensajeInicial? MensajeInicialActivo(Campania campania)
        => campania.MensajesIniciales
            .Where(mensaje => mensaje.Estado == EstadoRegistro.Activo)
            .OrderBy(mensaje => mensaje.Orden)
            .FirstOrDefault();

    /// <summary>Variables disponibles para reemplazo (nombre/area/empresa/campania + dinamicas del usuario).</summary>
    public static IReadOnlyDictionary<string, string> ConstruirVariables(Usuario usuario, Campania campania)
    {
        var variables = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["nombre"] = usuario.Nombre,
            ["area"] = usuario.Area,
            ["empresa"] = usuario.Empresa,
            ["campaña"] = campania.Nombre,
            ["campania"] = campania.Nombre,
        };

        foreach (var propiedad in usuario.PropiedadesDinamicas)
        {
            variables[propiedad.Key] = Convert.ToString(propiedad.Value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        return variables;
    }

    /// <summary>Reemplaza los marcadores <c>{{clave}}</c> del texto por sus valores.</summary>
    public static string Reemplazar(string texto, IReadOnlyDictionary<string, string> variables)
    {
        var resultado = texto;
        foreach (var variable in variables)
        {
            resultado = resultado.Replace("{{" + variable.Key + "}}", variable.Value, StringComparison.Ordinal);
        }

        return resultado;
    }

    /// <summary>Texto del mensaje inicial ya con sus variables resueltas para un usuario/campania.</summary>
    public static string Renderizar(MensajeInicial mensaje, Usuario usuario, Campania campania)
        => Reemplazar(mensaje.Texto, ConstruirVariables(usuario, campania));
}
