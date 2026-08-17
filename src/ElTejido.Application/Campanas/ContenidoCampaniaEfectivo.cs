using ElTejido.Domain.Campanas;
using ElTejido.Domain.Common;
using ElTejido.Domain.Localizacion;

namespace ElTejido.Application.Campanas;

/// <summary>
/// Snapshot inmutable que un consumidor entrega al resolutor. No consulta repositorios ni
/// configuración: el gate y el idioma ya vienen fijados por el hilo o la frontera de envío.
/// </summary>
public sealed record ContextoLocalizacion(
    Campania Campania,
    IdiomaConversacion Idioma,
    bool CatalogoTextosHabilitado)
{
    public string? PreguntaId { get; init; }

    public string? MensajeInicialId { get; init; }

    /// <summary>Solo telemetría; nunca participa en la selección del contenido.</summary>
    public string? CorrelationId { get; init; }
}

public enum OrigenContenidoCampania
{
    Legacy,
    Localizacion,
}

public sealed record ContenidoMensajeInicialEfectivo(string Texto, string? PlantillaRef);

public sealed record ContenidoPreguntaEfectiva(string Texto, string Instruccion);

/// <summary>Contenido completo de una única fuente editorial; nunca mezcla legacy y localización.</summary>
public sealed record ContenidoCampaniaEfectivo(
    IdiomaConversacion Idioma,
    OrigenContenidoCampania Origen,
    string Nombre,
    string Descripcion,
    string Objetivo,
    string MensajeCierre,
    IReadOnlyDictionary<string, ContenidoMensajeInicialEfectivo> MensajesIniciales,
    IReadOnlyDictionary<string, ContenidoPreguntaEfectiva> Preguntas);

public sealed record ProblemaContenidoCampania(string Codigo, string Ruta);

public abstract record ResultadoContenidoCampania(IdiomaConversacion Idioma)
{
    public sealed record Disponible(ContenidoCampaniaEfectivo Contenido)
        : ResultadoContenidoCampania(Contenido.Idioma);

    public sealed record NoDisponible(
        IdiomaConversacion Idioma,
        IReadOnlyList<ProblemaContenidoCampania> Problemas)
        : ResultadoContenidoCampania(Idioma)
    {
        public string CodigoPrincipal => Problemas[0].Codigo;
    }
}

public interface IResolutorContenidoCampania
{
    ResultadoContenidoCampania Resolver(ContextoLocalizacion contexto);
}

/// <summary>DT-P32-04 corte 2/3: única política de contenido editorial propio de campaña.</summary>
public sealed class ResolutorContenidoCampania : IResolutorContenidoCampania
{
    public const string CodigoIdiomaNoHabilitado = "IDIOMA_CAMPANIA_NO_HABILITADO";
    public const string CodigoLocalizacionIncompleta = "LOCALIZACION_CAMPANIA_INCOMPLETA";

    public ResultadoContenidoCampania Resolver(ContextoLocalizacion contexto)
    {
        if (!contexto.CatalogoTextosHabilitado)
        {
            return new ResultadoContenidoCampania.Disponible(ConstruirLegacy(contexto.Campania));
        }

        var campania = contexto.Campania;
        var idioma = contexto.Idioma;
        if (!campania.IdiomasInternosHabilitados.Contains(idioma))
        {
            return NoDisponible(
                idioma,
                new ProblemaContenidoCampania(
                    CodigoIdiomaNoHabilitado,
                    $"localizaciones.{idioma.Codigo}"));
        }

        if (!campania.TryObtenerLocalizacion(idioma.Codigo, out var localizacion))
        {
            return NoDisponible(
                idioma,
                new ProblemaContenidoCampania(
                    CodigoLocalizacionIncompleta,
                    $"localizaciones.{idioma.Codigo}"));
        }

        var problemas = ValidarLocalizacion(campania, localizacion, idioma);
        if (problemas.Count > 0)
        {
            return new ResultadoContenidoCampania.NoDisponible(idioma, problemas);
        }

        var mensajes = campania.MensajesIniciales
            .Where(mensaje => mensaje.Estado == EstadoRegistro.Activo)
            .ToDictionary(
                mensaje => mensaje.Id,
                mensaje =>
                {
                    var localizado = localizacion.MensajesIniciales[mensaje.Id];
                    return new ContenidoMensajeInicialEfectivo(localizado.Texto!, localizado.PlantillaRef);
                },
                StringComparer.Ordinal);
        var preguntas = campania.Preguntas
            .Where(pregunta => pregunta.Estado == EstadoRegistro.Activo)
            .ToDictionary(
                pregunta => pregunta.Id,
                pregunta =>
                {
                    var localizada = localizacion.Preguntas[pregunta.Id];
                    return new ContenidoPreguntaEfectiva(localizada.Texto!, localizada.Instruccion!);
                },
                StringComparer.Ordinal);
        var origen = campania.Localizaciones.ContainsKey(idioma.Codigo)
            ? OrigenContenidoCampania.Localizacion
            : OrigenContenidoCampania.Legacy;

        return new ResultadoContenidoCampania.Disponible(
            new ContenidoCampaniaEfectivo(
                idioma,
                origen,
                localizacion.Nombre!,
                localizacion.Descripcion!,
                localizacion.Objetivo!,
                localizacion.MensajeCierre!,
                mensajes,
                preguntas));
    }

    private static ContenidoCampaniaEfectivo ConstruirLegacy(Campania campania)
    {
        var mensajes = campania.MensajesIniciales
            .Where(mensaje => mensaje.Estado == EstadoRegistro.Activo)
            .ToDictionary(
                mensaje => mensaje.Id,
                mensaje => new ContenidoMensajeInicialEfectivo(mensaje.Texto, null),
                StringComparer.Ordinal);
        var preguntas = campania.Preguntas
            .Where(pregunta => pregunta.Estado == EstadoRegistro.Activo)
            .ToDictionary(
                pregunta => pregunta.Id,
                pregunta => new ContenidoPreguntaEfectiva(pregunta.Texto, pregunta.Instruccion),
                StringComparer.Ordinal);

        return new ContenidoCampaniaEfectivo(
            IdiomaConversacion.Espanol,
            OrigenContenidoCampania.Legacy,
            campania.Nombre,
            campania.Descripcion,
            campania.Objetivo,
            campania.ConfigConversacional.MensajeCierre,
            mensajes,
            preguntas);
    }

    private static IReadOnlyList<ProblemaContenidoCampania> ValidarLocalizacion(
        Campania campania,
        LocalizacionCampania localizacion,
        IdiomaConversacion idioma)
    {
        var raiz = $"localizaciones.{idioma.Codigo}";
        var problemas = new List<ProblemaContenidoCampania>();
        Requerir(localizacion.Nombre, $"{raiz}.nombre", problemas);
        Requerir(localizacion.Descripcion, $"{raiz}.descripcion", problemas);
        Requerir(localizacion.Objetivo, $"{raiz}.objetivo", problemas);
        Requerir(localizacion.MensajeCierre, $"{raiz}.mensajeCierre", problemas);

        foreach (var mensaje in campania.MensajesIniciales.Where(
                     mensaje => mensaje.Estado == EstadoRegistro.Activo))
        {
            var ruta = $"{raiz}.mensajesIniciales.{mensaje.Id}";
            if (!localizacion.MensajesIniciales.TryGetValue(mensaje.Id, out var localizado))
            {
                problemas.Add(new ProblemaContenidoCampania(CodigoLocalizacionIncompleta, ruta));
                continue;
            }

            Requerir(localizado.Texto, $"{ruta}.texto", problemas);
            Requerir(localizado.PlantillaRef, $"{ruta}.plantillaRef", problemas);
        }

        foreach (var pregunta in campania.Preguntas.Where(
                     pregunta => pregunta.Estado == EstadoRegistro.Activo))
        {
            var ruta = $"{raiz}.preguntas.{pregunta.Id}";
            if (!localizacion.Preguntas.TryGetValue(pregunta.Id, out var localizada))
            {
                problemas.Add(new ProblemaContenidoCampania(CodigoLocalizacionIncompleta, ruta));
                continue;
            }

            Requerir(localizada.Texto, $"{ruta}.texto", problemas);
            Requerir(localizada.Instruccion, $"{ruta}.instruccion", problemas);
        }

        return problemas;
    }

    private static void Requerir(
        string? valor,
        string ruta,
        ICollection<ProblemaContenidoCampania> problemas)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            problemas.Add(new ProblemaContenidoCampania(CodigoLocalizacionIncompleta, ruta));
        }
    }

    private static ResultadoContenidoCampania.NoDisponible NoDisponible(
        IdiomaConversacion idioma,
        ProblemaContenidoCampania problema)
        => new(idioma, [problema]);
}
