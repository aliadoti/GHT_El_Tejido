using ElTejido.Domain.Campanas;
using ElTejido.Domain.Common;

namespace ElTejido.Application.WhatsApp;

/// <summary>
/// DT-P32-03 §3.2: revisión **estructural** de los mapeos `plantillaRef + idioma` que exigirían las
/// campañas si el gate P-32 se encendiera. Reutiliza <see cref="OpcionesPlantillaEnvioInicial.TryResolver"/>
/// —la misma política que aplica <c>ServicioEnvios</c> en el envío real— para que no exista una
/// segunda interpretación de "configurado".
/// <para>
/// No consulta Graph API ni secretos: no puede afirmar que la plantilla está aprobada en Meta ni que
/// sus variables coinciden. Eso queda como verificación manual (`QAS/23`).
/// </para>
/// </summary>
public static class ValidadorMapeosPlantillaMeta
{
    public const string PlantillaRefFaltante = "plantilla_ref_faltante";
    public const string NombreFaltante = "nombre_faltante";
    public const string IdiomaMetaFaltante = "idioma_meta_faltante";
    public const string ComponenteVacio = "componente_vacio";
    public const string ComponenteDuplicado = "componente_duplicado";

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
    {
        var acumulado = new Dictionary<(string PlantillaRef, string Idioma), List<CampaniaRequierePlantillaMeta>>();
        var orden = new List<(string PlantillaRef, string Idioma)>();

        foreach (var campania in campanias)
        {
            foreach (var idioma in IdiomasEnAlcance(campania, idiomas))
            {
                var hayLocalizacion = campania.TryObtenerLocalizacion(idioma, out var localizacion);
                foreach (var mensaje in campania.MensajesIniciales.Where(x => x.Estado == EstadoRegistro.Activo))
                {
                    var plantillaRef = hayLocalizacion
                        && localizacion.MensajesIniciales.TryGetValue(mensaje.Id, out var localizado)
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
            .Select(clave => Evaluar(clave.PlantillaRef, clave.Idioma, acumulado[clave], opciones))
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
        OpcionesPlantillaEnvioInicial opciones)
    {
        if (plantillaRef.Length == 0)
        {
            // El mensaje inicial activo no declara alias para ese idioma: sin alias no hay envío
            // posible con el gate ON, así que se reporta en vez de desaparecer del diagnóstico.
            return new MapeoPlantillaMetaEvaluado(
                null, idioma, Configurado: false, NombreConfigurado: false, IdiomaMetaConfigurado: false,
                Componentes: [], Problemas: [PlantillaRefFaltante], campanias);
        }

        var configurada = ObtenerConfigurada(opciones, plantillaRef, idioma);
        var nombreConfigurado = !string.IsNullOrWhiteSpace(configurada?.Nombre);
        var idiomaMetaConfigurado = !string.IsNullOrWhiteSpace(configurada?.Idioma);
        var componentes = configurada?.Componentes ?? [];

        var problemas = new List<string>();
        if (!nombreConfigurado)
        {
            problemas.Add(NombreFaltante);
        }

        if (!idiomaMetaConfigurado)
        {
            problemas.Add(IdiomaMetaFaltante);
        }

        // Una lista vacía es válida para una plantilla sin variables; lo que no puede haber es un
        // componente en blanco o repetido dentro de una lista ya configurada.
        if (componentes.Any(string.IsNullOrWhiteSpace))
        {
            problemas.Add(ComponenteVacio);
        }

        var declarados = componentes.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToArray();
        if (declarados.Distinct(StringComparer.Ordinal).Count() != declarados.Length)
        {
            problemas.Add(ComponenteDuplicado);
        }

        return new MapeoPlantillaMetaEvaluado(
            plantillaRef,
            idioma,
            // Misma política que el envío real: el par resuelve o no resuelve.
            Configurado: opciones.TryResolver(plantillaRef, idioma, out _),
            nombreConfigurado,
            idiomaMetaConfigurado,
            declarados,
            problemas,
            campanias);
    }

    private static PlantillaEnvioInicialConfigurada? ObtenerConfigurada(
        OpcionesPlantillaEnvioInicial opciones,
        string plantillaRef,
        string idioma)
        => opciones.Mapeos.TryGetValue(plantillaRef, out var porIdioma)
            && porIdioma.TryGetValue(idioma, out var configurada)
            ? configurada
            : null;
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
