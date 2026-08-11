using ElTejido.Domain.Common;

namespace ElTejido.Domain.Campanas;

/// <summary>
/// Contenido editorial de una campaña para un idioma. Es deliberadamente independiente de los
/// identificadores técnicos de mensajes y preguntas: estos permanecen únicos en la campaña.
/// Los campos pueden quedar incompletos mientras la campaña es borrador; la validación de
/// completitud se ejecuta antes de activar o enviar en modo multidioma.
/// </summary>
public sealed class LocalizacionCampania
{
    private LocalizacionCampania(
        string idioma,
        string? nombre,
        string? descripcion,
        string? objetivo,
        string? mensajeCierre,
        IReadOnlyDictionary<string, LocalizacionMensajeInicial> mensajesIniciales,
        IReadOnlyDictionary<string, LocalizacionPregunta> preguntas)
    {
        Idioma = idioma;
        Nombre = nombre;
        Descripcion = descripcion;
        Objetivo = objetivo;
        MensajeCierre = mensajeCierre;
        MensajesIniciales = mensajesIniciales;
        Preguntas = preguntas;
    }

    public string Idioma { get; }
    public string? Nombre { get; }
    public string? Descripcion { get; }
    public string? Objetivo { get; }
    public string? MensajeCierre { get; }
    public IReadOnlyDictionary<string, LocalizacionMensajeInicial> MensajesIniciales { get; }
    public IReadOnlyDictionary<string, LocalizacionPregunta> Preguntas { get; }

    public static LocalizacionCampania Crear(
        string idioma,
        string? nombre,
        string? descripcion,
        string? objetivo,
        string? mensajeCierre,
        IReadOnlyDictionary<string, LocalizacionMensajeInicial>? mensajesIniciales,
        IReadOnlyDictionary<string, LocalizacionPregunta>? preguntas)
        => new(
            NormalizarIdioma(idioma),
            Opcional(nombre),
            Opcional(descripcion),
            Opcional(objetivo),
            Opcional(mensajeCierre),
            NormalizarMapa(mensajesIniciales),
            NormalizarMapa(preguntas));

    private static string NormalizarIdioma(string idioma)
    {
        var normalizado = DomainGuards.Required(idioma, nameof(idioma)).ToLowerInvariant();
        if (normalizado is not ("es" or "en"))
        {
            throw new DomainValidationException("IDIOMA_NO_SOPORTADO", "El idioma de la localizacion debe ser 'es' o 'en'.");
        }

        return normalizado;
    }

    private static string? Opcional(string? valor)
        => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static IReadOnlyDictionary<string, T> NormalizarMapa<T>(IReadOnlyDictionary<string, T>? valores)
    {
        if (valores is null || valores.Count == 0)
        {
            return new Dictionary<string, T>(StringComparer.Ordinal);
        }

        return valores
            .Where(par => !string.IsNullOrWhiteSpace(par.Key))
            .ToDictionary(par => par.Key.Trim(), par => par.Value, StringComparer.Ordinal);
    }
}

/// <summary>Texto y alias lógico de la plantilla Meta de un mensaje inicial por idioma.</summary>
public sealed class LocalizacionMensajeInicial
{
    public LocalizacionMensajeInicial(string? texto, string? plantillaRef)
    {
        Texto = Opcional(texto);
        PlantillaRef = Opcional(plantillaRef);
    }

    public string? Texto { get; }
    public string? PlantillaRef { get; }

    private static string? Opcional(string? valor)
        => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}

/// <summary>Texto e instrucción de una pregunta por idioma.</summary>
public sealed class LocalizacionPregunta
{
    public LocalizacionPregunta(string? texto, string? instruccion)
    {
        Texto = Opcional(texto);
        Instruccion = Opcional(instruccion);
    }

    public string? Texto { get; }
    public string? Instruccion { get; }

    private static string? Opcional(string? valor)
        => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
