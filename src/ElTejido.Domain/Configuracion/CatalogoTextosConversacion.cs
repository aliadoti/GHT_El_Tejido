using System.Collections.ObjectModel;
using ElTejido.Domain.Common;

namespace ElTejido.Domain.Configuracion;

public enum EstadoCatalogoTextos
{
    Borrador,
    Activo,
    Inactivo,
}

/// <summary>
/// Snapshot inmutable y versionado de los textos conversacionales de un idioma.
/// El contenido se administra fuera del binario; una version activa nunca se edita en sitio.
/// </summary>
public sealed class CatalogoTextosConversacion
{
    private CatalogoTextosConversacion(
        string familiaId,
        string idioma,
        int version,
        EstadoCatalogoTextos estado,
        IReadOnlyDictionary<string, string> mensajes,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> frases,
        string creadoPor,
        string? aprobadoPor,
        DateTimeOffset creadoEn,
        DateTimeOffset actualizadoEn,
        DateTimeOffset? activadoEn,
        string huella)
    {
        FamiliaId = familiaId;
        Idioma = idioma;
        Version = version;
        Estado = estado;
        Mensajes = mensajes;
        Frases = frases;
        CreadoPor = creadoPor;
        AprobadoPor = aprobadoPor;
        CreadoEn = creadoEn;
        ActualizadoEn = actualizadoEn;
        ActivadoEn = activadoEn;
        Huella = huella;
    }

    public string FamiliaId { get; }
    public string Idioma { get; }
    public int Version { get; }
    public EstadoCatalogoTextos Estado { get; }
    public IReadOnlyDictionary<string, string> Mensajes { get; }
    public IReadOnlyDictionary<string, IReadOnlyCollection<string>> Frases { get; }
    public string CreadoPor { get; }
    public string? AprobadoPor { get; }
    public DateTimeOffset CreadoEn { get; }
    public DateTimeOffset ActualizadoEn { get; }
    public DateTimeOffset? ActivadoEn { get; }
    public string Huella { get; }

    public static CatalogoTextosConversacion Crear(
        string familiaId,
        string idioma,
        int version,
        EstadoCatalogoTextos estado,
        IReadOnlyDictionary<string, string> mensajes,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> frases,
        string creadoPor,
        string? aprobadoPor,
        DateTimeOffset creadoEn,
        DateTimeOffset actualizadoEn,
        DateTimeOffset? activadoEn,
        string huella)
    {
        var idiomaNormalizado = DomainGuards.Required(idioma, nameof(idioma)).ToLowerInvariant();
        if (idiomaNormalizado is not ("es" or "en"))
        {
            throw new DomainValidationException("IDIOMA_CATALOGO_INVALIDO", "El idioma debe ser 'es' o 'en'.");
        }

        if (version <= 0)
        {
            throw new DomainValidationException("VERSION_CATALOGO_INVALIDA", "La version debe ser mayor que cero.");
        }

        var creadoUtc = creadoEn.ToUniversalTime();
        var actualizadoUtc = actualizadoEn.ToUniversalTime();
        if (actualizadoUtc < creadoUtc)
        {
            throw new DomainValidationException(
                "FECHA_ACTUALIZACION_INVALIDA",
                "La fecha de actualizacion no puede ser anterior a la fecha de creacion.");
        }

        if (estado == EstadoCatalogoTextos.Activo
            && (string.IsNullOrWhiteSpace(aprobadoPor) || activadoEn is null))
        {
            throw new DomainValidationException(
                "APROBACION_CATALOGO_INVALIDA",
                "Un catalogo activo debe registrar aprobador y fecha de activacion.");
        }

        return new CatalogoTextosConversacion(
            DomainGuards.Required(familiaId, nameof(familiaId)),
            idiomaNormalizado,
            version,
            estado,
            CopiarMensajes(mensajes),
            CopiarFrases(frases),
            DomainGuards.Required(creadoPor, nameof(creadoPor)),
            string.IsNullOrWhiteSpace(aprobadoPor) ? null : aprobadoPor.Trim(),
            creadoUtc,
            actualizadoUtc,
            activadoEn?.ToUniversalTime(),
            DomainGuards.Required(huella, nameof(huella)));
    }

    public CatalogoTextosConversacion CambiarEstado(
        EstadoCatalogoTextos estado,
        DateTimeOffset ahora,
        string? aprobadoPor = null)
        => Crear(
            FamiliaId,
            Idioma,
            Version,
            estado,
            Mensajes,
            Frases,
            CreadoPor,
            estado == EstadoCatalogoTextos.Activo ? aprobadoPor : AprobadoPor,
            CreadoEn,
            ahora,
            estado == EstadoCatalogoTextos.Activo ? ahora : ActivadoEn,
            Huella);

    private static IReadOnlyDictionary<string, string> CopiarMensajes(
        IReadOnlyDictionary<string, string> mensajes)
        => new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(mensajes, StringComparer.Ordinal));

    private static IReadOnlyDictionary<string, IReadOnlyCollection<string>> CopiarFrases(
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> frases)
        => new ReadOnlyDictionary<string, IReadOnlyCollection<string>>(
            frases.ToDictionary(
                item => item.Key,
                item => (IReadOnlyCollection<string>)item.Value.ToArray(),
                StringComparer.Ordinal));
}
