using ElTejido.Domain.Common;

namespace ElTejido.Domain.Configuracion;

/// <summary>
/// Rubrica versionada del contenedor config. Cubre REQ 17 y 29.8.
/// <para>
/// DT-RUB-01: la <b>estructura</b> (escala, instrucciones generales y criterios ordenados) es la
/// unica fuente de verdad. <see cref="ContenidoMarkdown"/> es una proyeccion derivada por
/// <see cref="CompiladorRubricaMarkdown"/>, no una entrada del autor, asi que no puede contradecir
/// los criterios configurados. <see cref="Crear"/> solo acepta una estructura valida;
/// <see cref="Rehidratar"/> existe para leer documentos historicos sin mutarlos.
/// </para>
/// </summary>
public sealed class Rubrica
{
    private Rubrica(
        string id,
        string nombre,
        string descripcion,
        string instruccionesGenerales,
        string contenidoMarkdown,
        string hashEstructura,
        EstadoIntegridadRubrica integridadEstructural,
        EscalaRubrica escala,
        IReadOnlyList<CriterioRubrica> criterios,
        int version,
        EstadoRubrica estado,
        DateTimeOffset creadoEn,
        DateTimeOffset actualizadoEn)
    {
        Id = id;
        Nombre = nombre;
        Descripcion = descripcion;
        InstruccionesGenerales = instruccionesGenerales;
        ContenidoMarkdown = contenidoMarkdown;
        HashEstructura = hashEstructura;
        IntegridadEstructural = integridadEstructural;
        Escala = escala;
        Criterios = criterios;
        Version = version;
        Estado = estado;
        CreadoEn = creadoEn;
        ActualizadoEn = actualizadoEn;
    }

    public string Id { get; }

    public string Nombre { get; }

    public string Descripcion { get; }

    /// <summary>Guia transversal que se inyecta al modelo junto con los criterios (03 §3.11).</summary>
    public string InstruccionesGenerales { get; }

    /// <summary>
    /// Proyeccion Markdown <b>derivada</b> de la estructura. Solo un documento legacy conserva aqui
    /// un texto que no produjo el compilador, y en ese caso la version queda marcada como no
    /// verificada.
    /// </summary>
    public string ContenidoMarkdown { get; }

    /// <summary>Huella de la representacion canonica (03 §3.11). Dos guardados iguales la repiten.</summary>
    public string HashEstructura { get; }

    public EstadoIntegridadRubrica IntegridadEstructural { get; }

    public EscalaRubrica Escala { get; }

    /// <summary>Criterios ordenados por <c>Orden</c>; es la lista canonica de todo el sistema.</summary>
    public IReadOnlyList<CriterioRubrica> Criterios { get; }

    public int Version { get; }

    public EstadoRubrica Estado { get; }

    public DateTimeOffset CreadoEn { get; }

    public DateTimeOffset ActualizadoEn { get; }

    /// <summary>
    /// <c>true</c> si la version puede asignarse a una campania nueva o activarse (DT-RUB-01 §3.2).
    /// Una version legacy o invalida se sigue leyendo y evaluando donde ya estaba configurada, pero
    /// no se propaga a configuraciones nuevas.
    /// </summary>
    public bool HabilitadaParaAsignacionNueva => IntegridadEstructural == EstadoIntegridadRubrica.Valida;

    /// <summary>
    /// Crea una version con estructura canonica valida y su Markdown derivado. Rechaza cualquier
    /// estructura que no cumpla las reglas de 03 §3.11; el llamador del API valida antes para poder
    /// devolver los motivos por campo (04 §5.5), y esta guarda impide que otra ruta persista algo
    /// incoherente.
    /// </summary>
    public static Rubrica Crear(
        string id,
        string nombre,
        string descripcion,
        EscalaRubrica escala,
        IEnumerable<CriterioRubrica> criterios,
        int version,
        EstadoRubrica estado,
        DateTimeOffset creadoEn,
        DateTimeOffset actualizadoEn,
        string? instruccionesGenerales = null)
    {
        var (fechaCreacionUtc, fechaActualizacionUtc) = ValidarVersionYFechas(version, creadoEn, actualizadoEn);

        var criteriosOrdenados = ValidadorRubricaEstructurada.NormalizarOrden(criterios);
        var validacion = ValidadorRubricaEstructurada.Validar(escala, criteriosOrdenados);
        if (!validacion.Valido)
        {
            var primero = validacion.Errores[0];
            throw new DomainValidationException(
                "RUBRICA_ESTRUCTURA_INVALIDA",
                $"La estructura de la rubrica no es valida ({primero.Campo}: {primero.Motivo}).");
        }

        var instrucciones = instruccionesGenerales?.Trim() ?? string.Empty;
        var criteriosFinales = Ordenar(criteriosOrdenados);

        return new Rubrica(
            DomainGuards.Required(id, nameof(id)),
            DomainGuards.Required(nombre, nameof(nombre)),
            DomainGuards.Required(descripcion, nameof(descripcion)),
            instrucciones,
            CompiladorRubricaMarkdown.Compilar(nombre, descripcion, instrucciones, escala, criteriosFinales),
            CompiladorRubricaMarkdown.CalcularHuella(instrucciones, escala, criteriosFinales),
            EstadoIntegridadRubrica.Valida,
            escala,
            criteriosFinales,
            version,
            estado,
            fechaCreacionUtc,
            fechaActualizacionUtc);
    }

    /// <summary>
    /// Rehidrata una version persistida <b>sin mutarla</b> (03 §3.11, compatibilidad de lectura). No
    /// rechaza una estructura historica incoherente: la marca. Si la estructura es valida y el
    /// Markdown persistido coincide con la proyeccion compilada, la version queda
    /// <see cref="EstadoIntegridadRubrica.Valida"/>; si la estructura es valida pero el Markdown no
    /// proviene del compilador, queda <see cref="EstadoIntegridadRubrica.LegacyNoVerificada"/> y
    /// conserva su texto original para no cambiar lo que ya recibia el modelo; si la estructura no
    /// cumple las reglas, queda <see cref="EstadoIntegridadRubrica.Invalida"/>.
    /// </summary>
    public static Rubrica Rehidratar(
        string id,
        string nombre,
        string descripcion,
        string? instruccionesGenerales,
        string? contenidoMarkdownPersistido,
        EscalaRubrica escala,
        IEnumerable<CriterioRubrica> criterios,
        int version,
        EstadoRubrica estado,
        DateTimeOffset creadoEn,
        DateTimeOffset actualizadoEn)
    {
        var (fechaCreacionUtc, fechaActualizacionUtc) = ValidarVersionYFechas(version, creadoEn, actualizadoEn);

        var criteriosOrdenados = Ordenar(ValidadorRubricaEstructurada.NormalizarOrden(criterios));
        var instrucciones = instruccionesGenerales?.Trim() ?? string.Empty;
        var compilado = CompiladorRubricaMarkdown.Compilar(nombre, descripcion, instrucciones, escala, criteriosOrdenados);
        var persistido = contenidoMarkdownPersistido?.Trim() ?? string.Empty;

        EstadoIntegridadRubrica integridad;
        string markdownEfectivo;
        if (!ValidadorRubricaEstructurada.Validar(escala, criteriosOrdenados).Valido)
        {
            integridad = EstadoIntegridadRubrica.Invalida;
            markdownEfectivo = persistido.Length == 0 ? compilado : persistido;
        }
        else if (string.Equals(persistido, compilado.Trim(), StringComparison.Ordinal))
        {
            integridad = EstadoIntegridadRubrica.Valida;
            markdownEfectivo = compilado;
        }
        else
        {
            integridad = EstadoIntegridadRubrica.LegacyNoVerificada;
            markdownEfectivo = persistido.Length == 0 ? compilado : persistido;
        }

        return new Rubrica(
            DomainGuards.Required(id, nameof(id)),
            DomainGuards.Required(nombre, nameof(nombre)),
            descripcion?.Trim() ?? string.Empty,
            instrucciones,
            markdownEfectivo,
            CompiladorRubricaMarkdown.CalcularHuella(instrucciones, escala, criteriosOrdenados),
            integridad,
            escala,
            criteriosOrdenados,
            version,
            estado,
            fechaCreacionUtc,
            fechaActualizacionUtc);
    }

    private static IReadOnlyList<CriterioRubrica> Ordenar(IReadOnlyList<CriterioRubrica> criterios)
        => criterios
            .OrderBy(c => c.Orden)
            .ThenBy(c => c.Id, StringComparer.Ordinal)
            .ToArray();

    private static (DateTimeOffset CreadoEn, DateTimeOffset ActualizadoEn) ValidarVersionYFechas(
        int version,
        DateTimeOffset creadoEn,
        DateTimeOffset actualizadoEn)
    {
        if (version <= 0)
        {
            throw new DomainValidationException(
                "VERSION_RUBRICA_INVALIDA",
                "La version de rubrica debe ser mayor que cero.");
        }

        var fechaCreacionUtc = creadoEn.ToUniversalTime();
        var fechaActualizacionUtc = actualizadoEn.ToUniversalTime();
        if (fechaActualizacionUtc < fechaCreacionUtc)
        {
            throw new DomainValidationException(
                "FECHA_ACTUALIZACION_INVALIDA",
                "La fecha de actualizacion no puede ser anterior a la fecha de creacion.");
        }

        return (fechaCreacionUtc, fechaActualizacionUtc);
    }
}
