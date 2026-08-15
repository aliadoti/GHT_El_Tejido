using ElTejido.Application.Campanas;
using ElTejido.Application.Common;
using ElTejido.Application.Participantes;
using ElTejido.Application.Usuarios;
using ElTejido.Application.WhatsApp;
using ElTejido.Domain.Campanas;
using ElTejido.Domain.Common;
using ElTejido.Domain.Participantes;
using ElTejido.Domain.Usuarios;

namespace ElTejido.Application.Configuracion;

/// <summary>
/// Casos de uso administrativos de campanias, mensajes, preguntas y participantes
/// (04 seccion 5.3, 07 seccion 2; REQ 11, 14-16).
/// </summary>
public sealed class ServicioGestionCampanias : IServicioGestionCampanias
{
    private readonly IRepositorioCampanias _campanias;
    private readonly IRepositorioUsuarios _usuarios;
    private readonly IRepositorioParticipantes _participantes;
    private readonly TimeProvider _tiempo;
    private readonly OpcionesCatalogoTextos _opcionesCatalogoTextos;
    private readonly IDisponibilidadCatalogoTextos? _catalogosTextos;
    private readonly OpcionesPlantillaEnvioInicial _plantillaEnvioInicial;

    public ServicioGestionCampanias(
        IRepositorioCampanias campanias,
        IRepositorioUsuarios usuarios,
        IRepositorioParticipantes participantes,
        TimeProvider tiempo,
        OpcionesCatalogoTextos? opcionesCatalogoTextos = null,
        IDisponibilidadCatalogoTextos? catalogosTextos = null,
        OpcionesPlantillaEnvioInicial? plantillaEnvioInicial = null)
    {
        _campanias = campanias;
        _usuarios = usuarios;
        _participantes = participantes;
        _tiempo = tiempo;
        _opcionesCatalogoTextos = opcionesCatalogoTextos ?? new OpcionesCatalogoTextos();
        _catalogosTextos = catalogosTextos;
        _plantillaEnvioInicial = plantillaEnvioInicial ?? new OpcionesPlantillaEnvioInicial();
    }

    public Task<IReadOnlyCollection<Campania>> BuscarCampaniasAsync(
        FiltroCampanias filtro,
        CancellationToken cancellationToken)
        => _campanias.BuscarCampaniasAsync(filtro, cancellationToken);

    public async Task<Campania> ObtenerCampaniaAsync(string id, CancellationToken cancellationToken)
    {
        var campania = await _campanias.ObtenerCampaniaPorIdAsync(RequerirId(id), cancellationToken);
        return campania ?? throw new ErrorNoEncontrado("La campania no existe.");
    }

    public async Task<Campania> CrearCampaniaAsync(
        SolicitudGuardarCampania solicitud,
        CancellationToken cancellationToken)
    {
        var ahora = _tiempo.GetUtcNow();
        var campania = Campania.Crear(
            "c_" + Guid.NewGuid().ToString("N"),
            solicitud.Nombre,
            solicitud.Descripcion,
            solicitud.Objetivo,
            EstadoCampania.Borrador,
            null,
            null,
            solicitud.RubricaRef,
            solicitud.PromptRefs,
            solicitud.ConfigLlmRef,
            solicitud.ConfigMarkdown,
            solicitud.ConfigConversacional,
            solicitud.ConfigSeguridad,
            null,
            ahora,
            ahora);

        await _campanias.GuardarCampaniaAsync(campania, cancellationToken);
        return campania;
    }

    public async Task<Campania> ActualizarCampaniaAsync(
        string id,
        SolicitudActualizarCampania solicitud,
        CancellationToken cancellationToken)
    {
        var existente = await ObtenerCampaniaAsync(id, cancellationToken);
        var actualizado = CopiarCampania(
            existente,
            nombre: solicitud.Nombre ?? existente.Nombre,
            descripcion: solicitud.Descripcion ?? existente.Descripcion,
            objetivo: solicitud.Objetivo ?? existente.Objetivo,
            rubricaRef: solicitud.RubricaRef ?? existente.RubricaRef,
            promptRefs: solicitud.PromptRefs ?? existente.PromptRefs,
            configLlmRef: solicitud.ConfigLlmRef ?? existente.ConfigLlmRef,
            configMarkdown: solicitud.ConfigMarkdown ?? existente.ConfigMarkdown,
            configConversacional: solicitud.ConfigConversacional ?? existente.ConfigConversacional,
            configSeguridad: solicitud.ConfigSeguridad ?? existente.ConfigSeguridad);

        await _campanias.GuardarCampaniaAsync(actualizado, cancellationToken);
        return actualizado;
    }

    public async Task<Campania> ActualizarLocalizacionesAsync(
        string id,
        SolicitudActualizarLocalizacionesCampania solicitud,
        CancellationToken cancellationToken)
    {
        var existente = await ObtenerCampaniaAsync(id, cancellationToken);
        var actualizado = CopiarCampania(
            existente,
            idiomasHabilitados: solicitud.IdiomasHabilitados,
            localizaciones: solicitud.Localizaciones);
        await _campanias.GuardarCampaniaAsync(actualizado, cancellationToken);
        return actualizado;
    }

    public async Task<Campania> CambiarEstadoCampaniaAsync(
        string id,
        EstadoCampania estado,
        CancellationToken cancellationToken)
    {
        var existente = await ObtenerCampaniaAsync(id, cancellationToken);
        ValidarTransicion(existente.Estado, estado);

        if (EsBilingue(existente) && estado == EstadoCampania.Activa)
        {
            var errores = ValidadorLocalizacionesCampania.Validar(existente);
            if (errores.Count > 0)
            {
                throw new ErrorValidacion("La campaña no tiene localizaciones completas para activarse.", errores);
            }

            // DT-P32-02 §5: además de las localizaciones propias, cada idioma habilitado necesita una
            // versión global activa y válida. Aplica aunque el gate esté OFF; una campaña monolingüe
            // española legacy no entra por esta rama y conserva su compatibilidad.
            await RequerirCatalogosActivosAsync(existente, cancellationToken);
        }

        if (estado == EstadoCampania.Activa)
        {
            RequerirMapeosMetaPropios(existente);
        }

        var actualizado = CopiarCampania(existente, estado: estado);
        await _campanias.GuardarCampaniaAsync(actualizado, cancellationToken);
        return actualizado;
    }

    public async Task<Campania> DuplicarCampaniaAsync(string id, CancellationToken cancellationToken)
    {
        var original = await ObtenerCampaniaAsync(id, cancellationToken);
        var ahora = _tiempo.GetUtcNow();
        var copia = Campania.Crear(
            "c_" + Guid.NewGuid().ToString("N"),
            original.Nombre + " (copia)",
            original.Descripcion,
            original.Objetivo,
            EstadoCampania.Borrador,
            original.MensajesIniciales,
            original.Preguntas,
            original.RubricaRef,
            original.PromptRefs,
            original.ConfigLlmRef,
            original.ConfigMarkdown,
            original.ConfigConversacional,
            original.ConfigSeguridad,
            null,
            ahora,
            ahora,
            original.IdiomasHabilitados,
            original.Localizaciones);

        await _campanias.GuardarCampaniaAsync(copia, cancellationToken);
        return copia;
    }

    public async Task<MensajeInicial> AgregarMensajeInicialAsync(
        string campaniaId,
        SolicitudGuardarMensajeInicial solicitud,
        CancellationToken cancellationToken)
    {
        var campania = await ObtenerCampaniaAsync(campaniaId, cancellationToken);
        var mensaje = MensajeInicial.Crear(
            "mi_" + Guid.NewGuid().ToString("N"),
            solicitud.NombreInterno,
            solicitud.Texto,
            solicitud.Orden,
            solicitud.VariablesDinamicas,
            solicitud.Estado,
            solicitud.PlantillaWhatsApp);

        var actualizado = CopiarCampania(
            campania,
            mensajesIniciales: campania.MensajesIniciales.Append(mensaje).OrderBy(m => m.Orden).ToArray());
        await _campanias.GuardarCampaniaAsync(actualizado, cancellationToken);
        return mensaje;
    }

    public async Task<MensajeInicial> ActualizarMensajeInicialAsync(
        string campaniaId,
        string mensajeId,
        SolicitudActualizarMensajeInicial solicitud,
        CancellationToken cancellationToken)
    {
        var campania = await ObtenerCampaniaAsync(campaniaId, cancellationToken);
        var actual = campania.MensajesIniciales.FirstOrDefault(m => m.Id == mensajeId)
            ?? throw new ErrorNoEncontrado("El mensaje inicial no existe.");
        var reemplazo = MensajeInicial.Crear(
            actual.Id,
            solicitud.NombreInterno ?? actual.NombreInterno,
            solicitud.Texto ?? actual.Texto,
            solicitud.Orden ?? actual.Orden,
            solicitud.VariablesDinamicas ?? actual.VariablesDinamicas,
            solicitud.Estado ?? actual.Estado,
            solicitud.PlantillaWhatsApp ?? actual.PlantillaWhatsApp);
        var mensajes = campania.MensajesIniciales.Select(m => m.Id == mensajeId ? reemplazo : m).OrderBy(m => m.Orden).ToArray();
        await _campanias.GuardarCampaniaAsync(CopiarCampania(campania, mensajesIniciales: mensajes), cancellationToken);
        return reemplazo;
    }

    public async Task EliminarMensajeInicialAsync(string campaniaId, string mensajeId, CancellationToken cancellationToken)
    {
        var campania = await ObtenerCampaniaAsync(campaniaId, cancellationToken);
        if (!campania.MensajesIniciales.Any(m => m.Id == mensajeId))
        {
            throw new ErrorNoEncontrado("El mensaje inicial no existe.");
        }

        await _campanias.GuardarCampaniaAsync(
            CopiarCampania(campania, mensajesIniciales: campania.MensajesIniciales.Where(m => m.Id != mensajeId).ToArray()),
            cancellationToken);
    }

    public async Task<Pregunta> AgregarPreguntaAsync(
        string campaniaId,
        SolicitudGuardarPregunta solicitud,
        CancellationToken cancellationToken)
    {
        var campania = await ObtenerCampaniaAsync(campaniaId, cancellationToken);
        var pregunta = Pregunta.Crear(
            "p_" + Guid.NewGuid().ToString("N"),
            solicitud.Texto,
            solicitud.Instruccion,
            solicitud.Categoria,
            solicitud.Orden,
            solicitud.Estado,
            solicitud.RubricaRef,
            solicitud.VersionRubrica,
            solicitud.PromptRefs,
            solicitud.MaxRepreguntas,
            solicitud.LimitesSeguridad,
            solicitud.ConfigMarkdown,
            solicitud.UmbralCierreAnticipado, solicitud.UmbralResumenConsolidacion);

        await _campanias.GuardarCampaniaAsync(
            CopiarCampania(campania, preguntas: campania.Preguntas.Append(pregunta).OrderBy(p => p.Orden).ToArray()),
            cancellationToken);
        return pregunta;
    }

    public async Task<Pregunta> ActualizarPreguntaAsync(
        string campaniaId,
        string preguntaId,
        SolicitudActualizarPregunta solicitud,
        CancellationToken cancellationToken)
    {
        var campania = await ObtenerCampaniaAsync(campaniaId, cancellationToken);
        var actual = campania.Preguntas.FirstOrDefault(p => p.Id == preguntaId)
            ?? throw new ErrorNoEncontrado("La pregunta no existe.");
        var reemplazo = Pregunta.Crear(
            actual.Id,
            solicitud.Texto ?? actual.Texto,
            solicitud.Instruccion ?? actual.Instruccion,
            solicitud.Categoria ?? actual.Categoria,
            solicitud.Orden ?? actual.Orden,
            solicitud.Estado ?? actual.Estado,
            solicitud.RubricaRef ?? actual.RubricaRef,
            solicitud.VersionRubrica ?? actual.VersionRubrica,
            solicitud.PromptRefs ?? actual.PromptRefs,
            solicitud.MaxRepreguntas ?? actual.MaxRepreguntas,
            solicitud.LimitesSeguridad ?? actual.LimitesSeguridad,
            solicitud.ConfigMarkdown ?? actual.ConfigMarkdown,
            solicitud.UmbralCierreAnticipado ?? actual.UmbralCierreAnticipado,
            solicitud.UmbralResumenConsolidacion ?? actual.UmbralResumenConsolidacion);
        var preguntas = campania.Preguntas.Select(p => p.Id == preguntaId ? reemplazo : p).OrderBy(p => p.Orden).ToArray();
        await _campanias.GuardarCampaniaAsync(CopiarCampania(campania, preguntas: preguntas), cancellationToken);
        return reemplazo;
    }

    public async Task EliminarPreguntaAsync(string campaniaId, string preguntaId, CancellationToken cancellationToken)
    {
        var campania = await ObtenerCampaniaAsync(campaniaId, cancellationToken);
        if (!campania.Preguntas.Any(p => p.Id == preguntaId))
        {
            throw new ErrorNoEncontrado("La pregunta no existe.");
        }

        await _campanias.GuardarCampaniaAsync(
            CopiarCampania(campania, preguntas: campania.Preguntas.Where(p => p.Id != preguntaId).ToArray()),
            cancellationToken);
    }

    public Task<IReadOnlyCollection<ParticipanteCampania>> ListarParticipantesAsync(
        string campaniaId,
        CancellationToken cancellationToken)
        => _participantes.ListarParticipantesAsync(RequerirId(campaniaId), cancellationToken);

    public async Task<IReadOnlyCollection<ParticipanteCampania>> AsociarParticipantesAsync(
        string campaniaId,
        SolicitudAsociarParticipantes solicitud,
        CancellationToken cancellationToken)
    {
        var campania = await ObtenerCampaniaAsync(campaniaId, cancellationToken);
        var usuarios = await ResolverUsuariosAsync(solicitud.UsuarioIds, solicitud.Filtro, cancellationToken);
        if (usuarios.Count == 0)
        {
            throw new ErrorValidacion("No hay usuarios activos para asociar.", new[] { new DetalleError("participantes", "vacio") });
        }

        var erroresLocalizacion = EsBilingue(campania)
            ? ValidadorLocalizacionesCampania.Validar(campania)
            : Array.Empty<DetalleError>();
        if (erroresLocalizacion.Count > 0)
        {
            throw new ErrorConflicto(
                "CAMPANIA_IDIOMA_INCOMPLETA: la campaña bilingüe no tiene localizaciones completas.");
        }

        var ahora = _tiempo.GetUtcNow();
        var asociados = new List<ParticipanteCampania>();
        foreach (var usuario in usuarios)
        {
            if (_opcionesCatalogoTextos.Habilitado
                && !campania.IdiomasHabilitados.Contains(usuario.Idioma, StringComparer.Ordinal))
            {
                throw new ErrorConflicto(
                    "IDIOMA_CAMPANIA_NO_HABILITADO: el idioma del participante no esta habilitado en la campania.");
            }

            var existente = await _participantes.ObtenerParticipantePorUsuarioAsync(campania.Id, usuario.Id, cancellationToken);
            var participante = ParticipanteCampania.Crear(
                existente?.Id ?? $"pc_{campania.Id}_{usuario.Id}",
                campania.Id,
                usuario.Id,
                usuario.WhatsappNormalizado,
                EstadoRegistro.Activo,
                existente?.EstadoEnvio ?? EstadoEnvio.Pendiente,
                existente?.EstadoRespuesta ?? EstadoRespuestaParticipante.SinRespuesta,
                existente?.FechaInclusion ?? ahora,
                existente?.FechaPrimerEnvio,
                existente?.FechaUltimaRespuesta);
            await _participantes.GuardarParticipanteAsync(participante, cancellationToken);
            asociados.Add(participante);
        }

        var habilitados = campania.UsuariosHabilitados.Concat(asociados.Select(p => p.UsuarioId)).Distinct(StringComparer.Ordinal).ToArray();
        await _campanias.GuardarCampaniaAsync(CopiarCampania(campania, usuariosHabilitados: habilitados), cancellationToken);
        return asociados;
    }

    public async Task<IReadOnlyCollection<ParticipantePreview>> PreviewParticipantesAsync(
        SolicitudFiltroParticipantes solicitud,
        CancellationToken cancellationToken)
    {
        var usuarios = await BuscarUsuariosElegiblesAsync(solicitud, cancellationToken);
        return usuarios.Select(u => new ParticipantePreview(
            u.Id,
            u.Nombre,
            u.WhatsappNormalizado.Valor,
            // El DTO de vista previa conserva su forma (04): area/empresa vacias viajan como cadena
            // vacia en vez de null, que es lo que el portal ya sabe pintar.
            u.Area ?? string.Empty,
            u.Empresa ?? string.Empty,
            u.Tags)).ToArray();
    }

    public async Task DesasociarParticipanteAsync(string campaniaId, string usuarioId, CancellationToken cancellationToken)
    {
        var campania = await ObtenerCampaniaAsync(campaniaId, cancellationToken);
        var participante = await _participantes.ObtenerParticipantePorUsuarioAsync(campania.Id, RequerirId(usuarioId), cancellationToken)
            ?? throw new ErrorNoEncontrado("El participante no esta asociado a la campania.");

        var inactivo = ParticipanteCampania.Crear(
            participante.Id,
            participante.CampaniaId,
            participante.UsuarioId,
            participante.WhatsappNormalizado,
            EstadoRegistro.Inactivo,
            participante.EstadoEnvio,
            participante.EstadoRespuesta,
            participante.FechaInclusion,
            participante.FechaPrimerEnvio,
            participante.FechaUltimaRespuesta);
        await _participantes.GuardarParticipanteAsync(inactivo, cancellationToken);
        await _campanias.GuardarCampaniaAsync(
            CopiarCampania(campania, usuariosHabilitados: campania.UsuariosHabilitados.Where(u => u != usuarioId).ToArray()),
            cancellationToken);
    }

    private async Task<IReadOnlyCollection<Usuario>> ResolverUsuariosAsync(
        IReadOnlyCollection<string>? usuarioIds,
        SolicitudFiltroParticipantes? filtro,
        CancellationToken cancellationToken)
    {
        var resultado = new Dictionary<string, Usuario>(StringComparer.Ordinal);
        foreach (var id in usuarioIds ?? Array.Empty<string>())
        {
            var usuario = await _usuarios.ObtenerUsuarioPorIdAsync(RequerirId(id), cancellationToken)
                ?? throw new ErrorNoEncontrado("Un usuario solicitado no existe.");
            if (EsElegible(usuario))
            {
                resultado[usuario.Id] = usuario;
            }
        }

        if (filtro is not null)
        {
            foreach (var usuario in await BuscarUsuariosElegiblesAsync(filtro, cancellationToken))
            {
                resultado[usuario.Id] = usuario;
            }
        }

        return resultado.Values.ToArray();
    }

    private async Task<IReadOnlyCollection<Usuario>> BuscarUsuariosElegiblesAsync(
        SolicitudFiltroParticipantes filtro,
        CancellationToken cancellationToken)
    {
        var usuarios = await _usuarios.BuscarUsuariosAsync(
            new FiltroUsuarios(
                RolUsuario.Participante,
                EstadoRegistro.Activo,
                filtro.Area,
                filtro.Empresa,
                filtro.Tags,
                filtro.Busqueda),
            cancellationToken);
        return usuarios.Where(EsElegible).ToArray();
    }

    private static bool EsElegible(Usuario usuario)
        => usuario.Rol == RolUsuario.Participante && usuario.Estado == EstadoRegistro.Activo;

    private Campania CopiarCampania(
        Campania actual,
        string? nombre = null,
        string? descripcion = null,
        string? objetivo = null,
        EstadoCampania? estado = null,
        IReadOnlyCollection<MensajeInicial>? mensajesIniciales = null,
        IReadOnlyCollection<Pregunta>? preguntas = null,
        string? rubricaRef = null,
        IReadOnlyDictionary<string, string>? promptRefs = null,
        string? configLlmRef = null,
        ConfigMarkdown? configMarkdown = null,
        ConfigConversacional? configConversacional = null,
        LimitesSeguridad? configSeguridad = null,
        IReadOnlyCollection<string>? usuariosHabilitados = null,
        IReadOnlyCollection<string>? idiomasHabilitados = null,
        IReadOnlyDictionary<string, LocalizacionCampania>? localizaciones = null)
        => Campania.Crear(
            actual.Id,
            nombre ?? actual.Nombre,
            descripcion ?? actual.Descripcion,
            objetivo ?? actual.Objetivo,
            estado ?? actual.Estado,
            mensajesIniciales ?? actual.MensajesIniciales,
            preguntas ?? actual.Preguntas,
            rubricaRef ?? actual.RubricaRef,
            promptRefs ?? actual.PromptRefs,
            configLlmRef ?? actual.ConfigLlmRef,
            configMarkdown ?? actual.ConfigMarkdown,
            configConversacional ?? actual.ConfigConversacional,
            configSeguridad ?? actual.ConfigSeguridad,
            usuariosHabilitados ?? actual.UsuariosHabilitados,
            actual.CreadoEn,
            _tiempo.GetUtcNow(),
            idiomasHabilitados ?? actual.IdiomasHabilitados,
            localizaciones ?? actual.Localizaciones);

    private static void ValidarTransicion(EstadoCampania actual, EstadoCampania destino)
    {
        if (actual == destino)
        {
            return;
        }

        var permitida = (actual, destino) switch
        {
            (EstadoCampania.Borrador, EstadoCampania.Activa) => true,
            (EstadoCampania.Activa, EstadoCampania.Cerrada) => true,
            (EstadoCampania.Cerrada, EstadoCampania.Archivada) => true,
            _ => false,
        };

        if (!permitida)
        {
            throw new ErrorConflicto("La transicion de estado de la campania no es valida.");
        }
    }

    private static bool EsBilingue(Campania campania)
        => campania.IdiomasHabilitados.Any(idioma => !string.Equals(idioma, "es", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// DT-P32-02 §5: bloquea la activación bilingüe con `catalogosTextos.{idioma}: activo_requerido`
    /// cuando falta el catálogo global de algún idioma habilitado, en vez de dejar que el runtime
    /// caiga de inglés a español.
    /// </summary>
    private async Task RequerirCatalogosActivosAsync(Campania campania, CancellationToken cancellationToken)
    {
        if (_catalogosTextos is null)
        {
            return;
        }

        var faltantes = await _catalogosTextos.ObtenerIdiomasSinCatalogoActivoAsync(
            campania.IdiomasHabilitados,
            cancellationToken);
        if (faltantes.Count == 0)
        {
            return;
        }

        throw new ErrorValidacion(
            "La campaña bilingüe necesita un catálogo de textos activo por idioma.",
            faltantes.Select(idioma => new DetalleError($"catalogosTextos.{idioma}", "activo_requerido")).ToArray());
    }

    /// <summary>
    /// DT-P32-03-01 §4: con el gate ON el envío inicial resuelve la plantilla por
    /// `plantillaRef + idioma` (`ServicioEnvios`), así que activar una campaña sin ese mapeo dejaría
    /// el lote inicial fallando para todos sus participantes. Se revisa **solo** la campaña objetivo
    /// —ningún otro borrador la bloquea— con la misma política estructural que readiness. Es local y
    /// determinista: no consulta Graph API, no envía nada y no verifica la aprobación en Meta.
    /// </summary>
    private void RequerirMapeosMetaPropios(Campania campania)
    {
        if (!_opcionesCatalogoTextos.Habilitado)
        {
            return;
        }

        var idiomas = campania.IdiomasHabilitados
            .Select(idioma => idioma.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var errores = ValidadorMapeosPlantillaMeta.Evaluar([campania], idiomas, _plantillaEnvioInicial)
            .Where(mapeo => !mapeo.Listo)
            .SelectMany(mapeo => mapeo.Campanias.SelectMany(requirente => mapeo.Problemas.Select(
                problema => new DetalleError($"mapeosMeta.{requirente.MensajeInicialId}.{mapeo.Idioma}", problema))))
            .ToArray();
        if (errores.Length == 0)
        {
            return;
        }

        throw new ErrorValidacion(
            "La campaña necesita una plantilla de WhatsApp configurada por mensaje inicial e idioma.",
            errores);
    }

    private static string RequerirId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ErrorValidacion("El id es obligatorio.", new[] { new DetalleError("id", "obligatorio") });
        }

        return id.Trim();
    }
}
