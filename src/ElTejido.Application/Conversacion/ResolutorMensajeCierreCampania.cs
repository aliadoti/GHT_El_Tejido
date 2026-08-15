using ElTejido.Application.Configuracion;
using ElTejido.Domain.Campanas;

namespace ElTejido.Application.Conversacion;

/// <summary>
/// DT-P32-03 §3.1: única resolución del cierre visible de una campaña. Todas las rutas del
/// orquestador (cierre normal, umbral/tope, intención de salida, rechazo/avance, cupo LLM, fallback
/// de evaluación, inactividad y cierre visible P-33) preguntan aquí antes de componer el mensaje.
/// <para>
/// La política es deliberadamente estrecha: con el gate P-32 apagado se conserva el campo legacy tal
/// cual; con el gate encendido manda <c>localizaciones[idioma].mensajeCierre</c> y una localización
/// ausente o vacía es un fallo tipificado. <b>Nunca</b> hay respaldo cruzado entre idiomas: responder
/// en español a un hilo en inglés es el defecto que esta iniciativa cierra.
/// </para>
/// <para>DT-P32-04 absorberá este puerto en <c>IResolutorContenidoCampania</c> sin cambiar la política.</para>
/// </summary>
public interface IResolutorMensajeCierreCampania
{
    ResultadoMensajeCierreCampania Resolver(Campania campania, string? idiomaConversacion);
}

/// <summary>De dónde salió el texto resuelto; sirve para auditar sin copiar contenido.</summary>
public enum OrigenMensajeCierreCampania
{
    /// <summary>Campo histórico <c>configConversacional.mensajeCierre</c> (gate P-32 apagado).</summary>
    Legacy,

    /// <summary>Contenido editorial por idioma de la campaña (gate P-32 encendido).</summary>
    LocalizacionCampania,
}

/// <summary>Resultado explícito de la resolución: o hay texto utilizable, o hay un código de fallo.</summary>
public abstract record ResultadoMensajeCierreCampania(string Idioma)
{
    /// <summary>Texto listo para componer el turno, con el idioma efectivo y su origen.</summary>
    public sealed record Disponible(string Texto, string Idioma, OrigenMensajeCierreCampania Origen)
        : ResultadoMensajeCierreCampania(Idioma);

    /// <summary>
    /// No hay cierre utilizable para ese idioma. El llamador conserva estado e idempotencia y usa el
    /// manejo tipificado de configuración incompleta; no traduce, no llama al LLM y no cae a español.
    /// </summary>
    public sealed record NoDisponible(string Codigo, string Idioma)
        : ResultadoMensajeCierreCampania(Idioma);
}

/// <inheritdoc cref="IResolutorMensajeCierreCampania"/>
public sealed class ResolutorMensajeCierreCampania : IResolutorMensajeCierreCampania
{
    /// <summary>Código único de fallo (DT-P32-03 §3.1); el idioma viaja aparte, nunca el texto.</summary>
    public const string CodigoLocalizacionIncompleta = "LOCALIZACION_CAMPANIA_INCOMPLETA";

    private const string IdiomaLegacy = "es";

    private readonly OpcionesCatalogoTextos _opciones;

    public ResolutorMensajeCierreCampania(OpcionesCatalogoTextos opciones)
        => _opciones = opciones;

    public ResultadoMensajeCierreCampania Resolver(Campania campania, string? idiomaConversacion)
    {
        // Gate OFF: comportamiento legacy exacto, sin consultar localizaciones ni normalizar idioma.
        if (!_opciones.Habilitado)
        {
            return new ResultadoMensajeCierreCampania.Disponible(
                campania.ConfigConversacional.MensajeCierre,
                IdiomaLegacy,
                OrigenMensajeCierreCampania.Legacy);
        }

        var idioma = string.IsNullOrWhiteSpace(idiomaConversacion)
            ? IdiomaLegacy
            : idiomaConversacion.Trim().ToLowerInvariant();

        // `es` conserva su respaldo histórico dentro de la propia campaña (Campania.TryObtenerLocalizacion);
        // los demás idiomas exigen contenido propio.
        if (!campania.TryObtenerLocalizacion(idioma, out var localizacion)
            || string.IsNullOrWhiteSpace(localizacion.MensajeCierre))
        {
            return new ResultadoMensajeCierreCampania.NoDisponible(CodigoLocalizacionIncompleta, idioma);
        }

        return new ResultadoMensajeCierreCampania.Disponible(
            localizacion.MensajeCierre.Trim(),
            idioma,
            OrigenMensajeCierreCampania.LocalizacionCampania);
    }
}
