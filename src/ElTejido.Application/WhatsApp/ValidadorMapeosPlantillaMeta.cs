using ElTejido.Application.Campanas;
using ElTejido.Domain.Campanas;
using ElTejido.Domain.Common;
using ElTejido.Domain.Localizacion;

namespace ElTejido.Application.WhatsApp;

/// <summary>
/// DT-P32-03 §3.2: revisión **estructural** de los mapeos `plantillaRef + idioma` que exigirían las
/// campañas si el gate P-32 se encendiera. Reutiliza <see cref="IResolverPlantillaCanal"/>
/// —la misma política que aplica <c>ServicioEnvios</c> en el envío real— para que no exista una
/// segunda interpretación de "configurado".
/// <para>
/// No consulta Graph API ni secretos: no puede afirmar que la plantilla está aprobada en Meta ni que
/// sus variables coinciden. Eso queda como verificación manual (`QAS/23`).
/// </para>
/// </summary>
public static class ValidadorMapeosPlantillaMeta
{
    public const string PlantillaRefFaltante = ProblemasPlantillaCanal.PlantillaRefFaltante;
    public const string NombreFaltante = ProblemasPlantillaCanal.NombreFaltante;
    public const string IdiomaMetaFaltante = ProblemasPlantillaCanal.IdiomaMetaFaltante;
    public const string ComponenteVacio = ProblemasPlantillaCanal.ComponenteVacio;
    public const string ComponenteDuplicado = ProblemasPlantillaCanal.ComponenteDuplicado;

    /// <summary>Etiqueta con la que viaja <see cref="EstadoCampania.Activa"/> en el diagnóstico.</summary>
    internal static readonly string EstadoActiva = EstadoCampania.Activa.ToString().ToLowerInvariant();

    /// <summary>
    /// Enumera los pares requeridos por las campañas dadas (se esperan `activa|borrador`) para los
    /// idiomas en alcance, deduplicados por alias + idioma y con las campañas que los exigen.
    /// </summary>
    public static IReadOnlyList<MapeoPlantillaMetaEvaluado> Evaluar(
        IEnumerable<Campania> campanias,
        IReadOnlyCollection<string> idiomas,
        OpcionesPlantillaEnvioInicial opciones)
        => Evaluar(
            campanias,
            idiomas,
            new ResolverPlantillaCanal(opciones),
            new ResolutorContenidoCampania());

    public static IReadOnlyList<MapeoPlantillaMetaEvaluado> Evaluar(
        IEnumerable<Campania> campanias,
        IReadOnlyCollection<string> idiomas,
        IResolverPlantillaCanal resolutor,
        IResolutorContenidoCampania contenidoCampania)
    {
        var acumulado = new Dictionary<(string PlantillaRef, string Idioma), List<CampaniaRequierePlantillaMeta>>();
        var orden = new List<(string PlantillaRef, string Idioma)>();

        foreach (var campania in campanias)
        {
            foreach (var idioma in IdiomasEnAlcance(campania, idiomas))
            {
                var idiomaInterno = IdiomaConversacion.Crear(idioma);
                var contenido = contenidoCampania.Resolver(
                    new ContextoLocalizacion(campania, idiomaInterno, CatalogoTextosHabilitado: true));
                var contenidoDisponible = contenido as ResultadoContenidoCampania.Disponible;
                foreach (var mensaje in campania.MensajesIniciales.Where(x => x.Estado == EstadoRegistro.Activo))
                {
                    var plantillaRef = contenidoDisponible?.Contenido.MensajesIniciales
                        .TryGetValue(mensaje.Id, out var localizado) == true
                        ? localizado.PlantillaRef
                        : null;
                    var clave = (plantillaRef?.Trim() ?? string.Empty, idioma);
                    if (!acumulado.TryGetValue(clave, out var requirentes))
                    {
                        requirentes = [];
                        acumulado[clave] = requirentes;
                        orden.Add(clave);
                    }

                    var requirente = new CampaniaRequierePlantillaMeta(
                        campania.Id,
                        campania.Nombre,
                        campania.Estado.ToString().ToLowerInvariant(),
                        mensaje.Id);
                    if (!requirentes.Contains(requirente))
                    {
                        requirentes.Add(requirente);
                    }
                }
            }
        }

        return orden
            .Select(clave => Evaluar(clave.PlantillaRef, clave.Idioma, acumulado[clave], resolutor))
            .ToArray();
    }

    private static IEnumerable<string> IdiomasEnAlcance(Campania campania, IReadOnlyCollection<string> idiomas)
        => campania.IdiomasHabilitados
            .Select(idioma => idioma.Trim().ToLowerInvariant())
            .Where(idioma => idiomas.Contains(idioma, StringComparer.Ordinal))
            .Distinct(StringComparer.Ordinal);

    private static MapeoPlantillaMetaEvaluado Evaluar(
        string plantillaRef,
        string idioma,
        IReadOnlyList<CampaniaRequierePlantillaMeta> campanias,
        IResolverPlantillaCanal resolutor)
    {
        if (plantillaRef.Length == 0)
        {
            // El mensaje inicial activo no declara alias para ese idioma: sin alias no hay envío
            // posible con el gate ON, así que se reporta en vez de desaparecer del diagnóstico.
            return new MapeoPlantillaMetaEvaluado(
                null, idioma, Configurado: false, NombreConfigurado: false, IdiomaMetaConfigurado: false,
                Componentes: [], Problemas: [PlantillaRefFaltante], campanias);
        }

        var idiomaInterno = IdiomaConversacion.Crear(idioma);
        var resultado = resolutor.Resolver(plantillaRef, idiomaInterno);
        var disponible = resultado as ResultadoPlantillaCanal.Disponible;
        var noDisponible = resultado as ResultadoPlantillaCanal.NoDisponible;

        return new MapeoPlantillaMetaEvaluado(
            plantillaRef,
            idioma,
            Configurado: disponible is not null,
            disponible is not null || noDisponible!.NombreConfigurado,
            disponible is not null || noDisponible!.IdiomaMetaConfigurado,
            disponible?.Componentes ?? noDisponible!.Componentes,
            noDisponible?.Problemas ?? [],
            campanias);
    }
}

/// <summary>Par `plantillaRef + idioma` requerido, con su diagnóstico estructural.</summary>
public sealed record MapeoPlantillaMetaEvaluado(
    string? PlantillaRef,
    string Idioma,
    bool Configurado,
    bool NombreConfigurado,
    bool IdiomaMetaConfigurado,
    IReadOnlyList<string> Componentes,
    IReadOnlyList<string> Problemas,
    IReadOnlyList<CampaniaRequierePlantillaMeta> Campanias)
{
    /// <summary>Estructuralmente listo; no implica aprobación ni verificación en Meta.</summary>
    public bool Listo => Configurado && Problemas.Count == 0;

    /// <summary>
    /// DT-P32-03-01 §2: el par solo condiciona el gate global si al menos una campaña **activa** lo
    /// exige. Un borrador a medio construir es un estado normal de trabajo —y sin transición a
    /// `archivada`— así que se sigue enumerando como pendiente pero no mantiene la señal en `false`
    /// de forma indefinida para las campañas que ya están operando.
    /// </summary>
    public bool BloqueaGateOn => Campanias.Any(campania =>
        string.Equals(campania.Estado, ValidadorMapeosPlantillaMeta.EstadoActiva, StringComparison.Ordinal));
}

/// <summary>Campaña y mensaje inicial que exigen el par. Sin teléfonos ni contenido del participante.</summary>
public sealed record CampaniaRequierePlantillaMeta(
    string CampaniaId,
    string Nombre,
    string Estado,
    string MensajeInicialId);
