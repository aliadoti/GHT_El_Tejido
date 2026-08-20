using System.Globalization;
using System.Text;
using ElTejido.Application.Campanas;
using ElTejido.Application.Common;
using ElTejido.Application.Configuracion;
using ElTejido.Application.Identidad;
using ElTejido.Application.Respuestas;
using ElTejido.Application.Seguridad;
using ElTejido.Application.WhatsApp;
using ElTejido.Domain.Campanas;
using ElTejido.Domain.Common;
using ElTejido.Domain.Conversaciones;
using ElTejido.Domain.Participantes;
using ElTejido.Domain.Seguridad;
using ElTejido.Domain.Usuarios;
using DominioConversacion = ElTejido.Domain.Conversaciones.Conversacion;

namespace ElTejido.Application.Conversacion;

/// <summary>
/// P-26 cortes 2-3 (05 §4.3 paso 0, §4.4.3; Reglas §2.10): resolucion determinista de campania y
/// pregunta previa al orquestador. Con 0 campanias elegibles responde el rechazo neutral vigente
/// (silencio, como el orquestador cuando todo esta cerrado); con 1 continua el flujo actual; con
/// varias conserva el aporte en <see cref="EnrutamientoAporte"/> y ofrece listas numeradas de
/// campania y, si aplica, de pregunta (§5.4). Una afinidad vigente (§5.6) enruta las respuestas de
/// coaching sin menu; "otra campaña" la suspende sin cerrar la idea; y en campanias continuas un
/// aporte posterior abre un ciclo nuevo (§5.7). El LLM nunca participa en estas decisiones.
/// </summary>
public interface IServicioEnrutamientoParticipacion
{
    Task<ResultadoEnrutamiento> ResolverAsync(
        Usuario usuario,
        IReadOnlyList<CandidatoCampania> candidatos,
        MensajeEntrante mensaje,
        CancellationToken cancellationToken);

    /// <summary>
    /// Marca el enrutamiento <c>listo</c> como <c>enIdea</c> despues de que el orquestador persistio
    /// el aporte original (03 §3.6.1: solo esa transicion fija <c>procesadoEn</c>). Idempotente: un
    /// enrutamiento que ya no esta <c>listo</c> se conserva tal cual.
    /// </summary>
    Task ConfirmarProcesadoAsync(
        string usuarioId,
        string whatsappMessageIdOriginal,
        CancellationToken cancellationToken);

    /// <summary>P-30: cierra de forma auditable la seleccion historica despues de aplicar I-19.</summary>
    Task ConfirmarRetomadaAsync(
        string usuarioId,
        string whatsappMessageIdOriginal,
        bool completada,
        CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>P-33: deja la afinidad de una consulta cerrada lista para una corrección posterior.</summary>
    Task ConfirmarConsultaIdeaAsync(
        string usuarioId,
        string whatsappMessageIdOriginal,
        string ideaId,
        string conversacionId,
        CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>Desenlace de la resolucion P-26; jerarquia cerrada para forzar el manejo de todos los casos.</summary>
public abstract record ResultadoEnrutamiento
{
    private ResultadoEnrutamiento()
    {
    }

    /// <summary>
    /// Hay exactamente una campania resuelta: entregar <paramref name="Mensaje"/> al orquestador.
    /// <paramref name="EnrutamientoAporteId"/> viene poblado cuando el aporte procede de una seleccion
    /// conservada y debe confirmarse como procesado al terminar. <paramref name="Contexto"/> viene
    /// poblado cuando la pregunta tambien quedo resuelta (entrega dirigida, ciclos P-26); nulo
    /// conserva la resolucion secuencial actual del orquestador.
    /// </summary>
    public sealed record ContinuarConversacion(
        CandidatoCampania Candidato,
        MensajeEntrante Mensaje,
        string? EnrutamientoAporteId,
        ContextoAporteEnrutado? Contexto = null,
        ClasificacionIntencionPrevia? ClasificacionPrevia = null) : ResultadoEnrutamiento;

    /// <summary>El aporte quedo conservado y un menu (campania o pregunta) fue enviado u ofrecido de nuevo.</summary>
    public sealed record SeleccionPendiente(string EnrutamientoAporteId) : ResultadoEnrutamiento;

    /// <summary>
    /// P-26 §5.6: el participante cambio explicitamente de campania y la afinidad quedo apuntando a
    /// <paramref name="Candidato"/>. Si <paramref name="ConversacionAbierta"/> no es nula, el
    /// orquestador puede reenganchar el turno de coaching pendiente de esa conversacion.
    /// </summary>
    public sealed record CambioCampaniaAplicado(
        CandidatoCampania Candidato,
        DominioConversacion? ConversacionAbierta) : ResultadoEnrutamiento;

    /// <summary>
    /// P-28: saludo/inicio breve sin flujo. El alcance ya fue validado, pero no se crea una
    /// conversación ni se interpreta el saludo como aporte; el siguiente texto sustantivo vuelve a
    /// resolver P-26.
    /// </summary>
    public sealed record DespertarProactivo(CandidatoCampania Candidato) : ResultadoEnrutamiento;

    /// <summary>P-30: idea historica ya elegida y revalidada; el texto de seleccion no es un aporte.</summary>
    public sealed record RetomarIdea(
        CandidatoCampania Candidato,
        MensajeEntrante Mensaje,
        ContextoRetomarIdea Contexto) : ResultadoEnrutamiento;

    /// <summary>P-33: mostrar la versión oficial no es un aporte ni modifica el hilo.</summary>
    public sealed record ConsultarIdea(
        CandidatoCampania Candidato,
        MensajeEntrante Mensaje,
        ContextoConsultaIdea Contexto) : ResultadoEnrutamiento;

    /// <summary>Ninguna campania elegible: rechazo neutral vigente (silencio, comportamiento actual).</summary>
    public sealed record SinElegibles() : ResultadoEnrutamiento;
}

public sealed class ServicioEnrutamientoParticipacion : IServicioEnrutamientoParticipacion
{
    private static readonly TimeSpan VigenciaAfinidad = TimeSpan.FromHours(24);
    private const int MaxCaracteresParafrasisSeleccion = 160;

    private readonly IRepositorioEnrutamientosAporte _enrutamientos;
    private readonly IRepositorioConversaciones _conversaciones;
    private readonly IRepositorioRespuestas? _respuestas;
    private readonly IWhatsAppGateway _gateway;
    private readonly IRepositorioLogSeguridad _logSeguridad;
    private readonly IProveedorCorrelacion _correlacion;
    private readonly OpcionesMensajesConversacion _mensajes;
    private readonly DetectorIntencionContinuar _cambioCampania;
    private readonly DetectorEntradaProactiva _entradaProactiva;
    private readonly bool _despertarProactivoHabilitado;
    private readonly bool _retomarIdeasHabilitado;
    private readonly bool _visibilidadIdeaParticipanteHabilitada;
    private readonly int _maxCaracteresConsultaIdea;
    private readonly DetectorConsultaIdea _consultaIdea;
    private readonly int _maxCaracteresIntencionContinuar;
    private readonly int _maxCaracteresDespertarProactivo;
    private readonly DetectorIntencionContinuar _retomarIdea;
    private readonly IResolutorTextosConversacion? _resolutorTextos;
    private readonly IRepositorioConfiguracion? _configuracion;
    private readonly IClasificadorIntencionControl? _clasificadorIntencion;
    private readonly bool _clasificacionSemanticaConsultaIdeaHabilitada;
    private readonly bool _cuposHabilitados;
    private readonly bool _consolidacionProgresivaHabilitada;
    private readonly GuardaCuposLlm? _guardaCuposLlm;
    private readonly TimeProvider _tiempo;

    public ServicioEnrutamientoParticipacion(
        IRepositorioEnrutamientosAporte enrutamientos,
        IRepositorioConversaciones conversaciones,
        IWhatsAppGateway gateway,
        IRepositorioLogSeguridad logSeguridad,
        IProveedorCorrelacion correlacion,
        OpcionesConversacion opciones,
        TimeProvider tiempo,
        IRepositorioRespuestas? respuestas = null,
        IResolutorTextosConversacion? resolutorTextos = null,
        IRepositorioConfiguracion? configuracion = null,
        IClasificadorIntencionControl? clasificadorIntencion = null)
    {
        _enrutamientos = enrutamientos;
        _conversaciones = conversaciones;
        _respuestas = respuestas;
        _gateway = gateway;
        _logSeguridad = logSeguridad;
        _correlacion = correlacion;
        _mensajes = opciones.Mensajes;
        _cambioCampania = new DetectorIntencionContinuar(
            opciones.FrasesCambiarCampania is { Count: > 0 }
                ? opciones.FrasesCambiarCampania
                : DetectorIntencionContinuar.FrasesCambiarCampaniaPorDefecto,
            opciones.MaxCaracteresIntencionContinuar);
        _entradaProactiva = new DetectorEntradaProactiva(
            opciones.FrasesDespertarProactivo is { Count: > 0 }
                ? opciones.FrasesDespertarProactivo
                : DetectorEntradaProactiva.FrasesPorDefecto,
            opciones.MaxCaracteresDespertarProactivo);
        _despertarProactivoHabilitado = opciones.DespertarProactivoHabilitado;
        _retomarIdeasHabilitado = opciones.RetomarIdeasHabilitado;
        _visibilidadIdeaParticipanteHabilitada = opciones.VisibilidadIdeaParticipanteHabilitada;
        _maxCaracteresConsultaIdea = opciones.MaxCaracteresConsultaIdea;
        _consultaIdea = new DetectorConsultaIdea(
            opciones.FrasesConsultarIdea is { Count: > 0 }
                ? opciones.FrasesConsultarIdea
                : DetectorConsultaIdea.FrasesPorDefecto,
            opciones.MaxCaracteresConsultaIdea);
        _maxCaracteresIntencionContinuar = opciones.MaxCaracteresIntencionContinuar;
        _maxCaracteresDespertarProactivo = opciones.MaxCaracteresDespertarProactivo;
        _retomarIdea = new DetectorIntencionContinuar(
            opciones.FrasesRevisitarIdea is { Count: > 0 }
                ? opciones.FrasesRevisitarIdea
                : DetectorIntencionContinuar.FrasesRevisitarIdeaPorDefecto,
            opciones.MaxCaracteresIntencionContinuar);
        _resolutorTextos = resolutorTextos;
        _configuracion = configuracion;
        _clasificadorIntencion = clasificadorIntencion;
        _clasificacionSemanticaConsultaIdeaHabilitada = opciones.ClasificacionSemanticaConsultaIdeaHabilitada;
        _cuposHabilitados = opciones.CuposHabilitados;
        _consolidacionProgresivaHabilitada = opciones.ConsolidacionProgresivaHabilitada;
        _guardaCuposLlm = respuestas is null ? null : new GuardaCuposLlm(respuestas, logSeguridad);
        _tiempo = tiempo;
    }

    public async Task<ResultadoEnrutamiento> ResolverAsync(
        Usuario usuario,
        IReadOnlyList<CandidatoCampania> candidatos,
        MensajeEntrante mensaje,
        CancellationToken cancellationToken)
    {
        var ahora = _tiempo.GetUtcNow();

        var pendiente = await ObtenerSeleccionPendienteAsync(usuario.Id, cancellationToken);
        if (pendiente is not null && pendiente.SeleccionVencida(ahora))
        {
            // §5.5: el texto permanece auditable pero no se procesa en una seleccion posterior; el
            // mensaje actual arranca una resolucion nueva.
            await _enrutamientos.GuardarAsync(pendiente.Expirar(ahora), cancellationToken);
            await RegistrarAsync(usuario, "expirado", Detalle(pendiente), ahora, cancellationToken);
            pendiente = null;
        }

        // P-33: petición pura antes de cualquier menú o afinidad; no consume ni reinterpreta el aporte.
        if (_visibilidadIdeaParticipanteHabilitada
            && _respuestas is not null
            && await CoincideConsultaIdeaAsync(usuario.Idioma, mensaje.Texto, cancellationToken))
        {
            if (pendiente is not null)
            {
                await _enrutamientos.GuardarAsync(pendiente.Cancelar(ahora), cancellationToken);
                await RegistrarAsync(usuario, "consultaCancelaSeleccion", Detalle(pendiente), ahora, cancellationToken);
            }

            return await ResolverConsultaIdeaAsync(usuario, candidatos, mensaje, ahora, cancellationToken);
        }

        // P-33: una conformidad inequívoca inmediatamente posterior a mostrar la idea es un fast path
        // determinista. No necesita que el clasificador semántico entienda una variante lingüística ni
        // consume cupo/tokens. La autoridad sigue siendo server-side: solo se propone ConfirmarIdea si
        // existe la afinidad exacta creada por un envío P-33 exitoso; las ramas de abajo deciden según
        // el estado real de la conversación y de la idea.
        var afinidadConsultaDeterminista = _visibilidadIdeaParticipanteHabilitada
            ? await ObtenerAfinidadConsultaVigenteAsync(usuario.Id, candidatos, ahora, cancellationToken)
            : null;
        var clasificacionPrevia = afinidadConsultaDeterminista is not null
            && await CoincideConformidadConsultaIdeaAsync(
                afinidadConsultaDeterminista.Enrutamiento.Idioma, mensaje.Texto, cancellationToken)
                ? new ClasificacionIntencionPrevia(IntencionControl.ConfirmarIdea, false)
                : await ClasificarIntencionSemanticaAsync(
                    usuario, candidatos, mensaje, pendiente is not null, ahora, cancellationToken);
        if (clasificacionPrevia?.Intencion == IntencionControl.ConsultarIdea)
        {
            if (pendiente is not null)
            {
                await _enrutamientos.GuardarAsync(pendiente.Cancelar(ahora), cancellationToken);
                await RegistrarAsync(usuario, "consultaCancelaSeleccion", Detalle(pendiente), ahora, cancellationToken);
            }

            return await ResolverConsultaIdeaAsync(usuario, candidatos, mensaje, ahora, cancellationToken);
        }

        if (pendiente is not null)
        {
            return pendiente.Estado switch
            {
                EstadoEnrutamientoAporte.SeleccionCampania => await ResolverSeleccionCampaniaAsync(
                    usuario, candidatos, pendiente, mensaje, ahora, cancellationToken),
                EstadoEnrutamientoAporte.SeleccionPregunta => await ResolverSeleccionPreguntaAsync(
                    usuario, candidatos, pendiente, mensaje, ahora, cancellationToken),
                EstadoEnrutamientoAporte.SeleccionIdea => await ResolverSeleccionIdeaHistoricaAsync(
                    usuario, candidatos, pendiente, mensaje, ahora, cancellationToken),
                EstadoEnrutamientoAporte.Listo when pendiente.EsRetomarIdea => await EntregarRetomadaListaAsync(
                    usuario, candidatos, pendiente, mensaje, ahora, cancellationToken),
                _ => throw new InvalidOperationException($"Seleccion pendiente no soportada: {pendiente.Estado}."),
            };
        }

        // P-30: una peticion generica de retomar precede a la afinidad y a un aporte nuevo. El
        // servidor resuelve primero campania/pregunta y nunca entrega esta frase como contenido.
        if (_retomarIdeasHabilitado
            && _respuestas is not null
            && await CoincideRetomarIdeaAsync(usuario.Idioma, mensaje.Texto, cancellationToken))
        {
            return await IniciarRetomarIdeaAsync(usuario, candidatos, mensaje, ahora, cancellationToken);
        }

        // §5.6: una afinidad vigente enruta las respuestas de coaching sin volver a listar campanias,
        // salvo que el participante pida explicitamente cambiar de campania (§5.1 paso 3).
        var afinidad = clasificacionPrevia?.Intencion == IntencionControl.ConfirmarIdea
            ? await ObtenerAfinidadConsultaVigenteAsync(usuario.Id, candidatos, ahora, cancellationToken)
            : null;
        afinidad ??= await ObtenerAfinidadVigenteAsync(usuario.Id, candidatos, ahora, cancellationToken);
        if (afinidad is not null)
        {
            if (await CoincideCambioCampaniaAsync(afinidad.Enrutamiento.Idioma, mensaje.Texto, cancellationToken))
            {
                return await SuspenderAfinidadYReofrecerAsync(usuario, candidatos, afinidad, mensaje, ahora, cancellationToken);
            }

            if (afinidad.Enrutamiento.EsConsultarIdea
                && clasificacionPrevia?.Intencion == IntencionControl.ConfirmarIdea)
            {
                if (afinidad.Conversacion?.Estado == EstadoConversacion.Cerrada)
                {
                    await _enrutamientos.GuardarAsync(afinidad.Enrutamiento.Completar(ahora), cancellationToken);
                    return new ResultadoEnrutamiento.SinElegibles();
                }

                if (afinidad.Conversacion is not null && afinidad.Enrutamiento.IdeaSeleccionadaId is not null)
                {
                    return new ResultadoEnrutamiento.ContinuarConversacion(
                        afinidad.Candidato,
                        mensaje,
                        null,
                        new ContextoAporteEnrutado(
                            afinidad.Conversacion.PreguntaId,
                            null,
                            afinidad.Conversacion.Id,
                            ClasificacionPrevia: clasificacionPrevia,
                            IdeaIdConsultada: afinidad.Enrutamiento.IdeaSeleccionadaId),
                        clasificacionPrevia);
                }
            }

            if (afinidad.Enrutamiento.EsConsultarIdea && afinidad.Conversacion?.Estado == EstadoConversacion.Cerrada)
            {
                if (await EsAcuseConsultaIdeaAsync(afinidad.Enrutamiento.Idioma, mensaje.Texto, cancellationToken))
                {
                    await _enrutamientos.GuardarAsync(afinidad.Enrutamiento.Completar(ahora), cancellationToken);
                    return new ResultadoEnrutamiento.SinElegibles();
                }

                return new ResultadoEnrutamiento.ContinuarConversacion(
                    afinidad.Candidato, mensaje, null,
                    new ContextoAporteEnrutado(
                        afinidad.Conversacion.PreguntaId, null, afinidad.Conversacion.Id,
                        afinidad.Enrutamiento.IdeaSeleccionadaId,
                        ClasificacionPrevia: clasificacionPrevia),
                    clasificacionPrevia);
            }

            if (afinidad.Conversacion is not null)
            {
                return new ResultadoEnrutamiento.ContinuarConversacion(
                    afinidad.Candidato,
                    mensaje,
                    null,
                    new ContextoAporteEnrutado(
                        afinidad.Conversacion.PreguntaId,
                        null,
                        afinidad.Conversacion.Id,
                        ClasificacionPrevia: clasificacionPrevia),
                    clasificacionPrevia);
            }

            // Afinidad hacia una campania sin conversacion todavia (cambio de campania reciente): el
            // mensaje actual es el aporte y se resuelve dentro de esa campania.
            return await ResolverDentroDeCampaniaAsync(usuario, afinidad.Candidato, mensaje, ahora, cancellationToken);
        }

        var elegibles = await CalcularElegiblesAsync(candidatos, usuario.Id, cancellationToken);
        if (elegibles.Count == 0)
        {
            return new ResultadoEnrutamiento.SinElegibles();
        }

        // P-28: solo después de descartar selección pendiente/afinidad y trabajo pendiente. El saludo
        // no abre una conversación ni se guarda como aporte raíz; el siguiente texto real vuelve a P-26.
        if (_despertarProactivoHabilitado
            && !elegibles.Any(e => e.TrabajoPendiente)
            && await CoincideEntradaProactivaAsync(usuario.Idioma, mensaje.Texto, cancellationToken))
        {
            return elegibles.Count == 1
                ? new ResultadoEnrutamiento.DespertarProactivo(elegibles[0].Candidato)
                : await ConservarYOfrecerAsync(usuario, elegibles, mensaje, ahora, cancellationToken, esEntradaProactiva: true);
        }

        if (elegibles.Count == 1)
        {
            var unico = elegibles[0];
            if (unico.TrabajoPendiente)
            {
                // Comportamiento actual intacto: una sola opcion con recorrido en curso se entrega al
                // orquestador secuencial de siempre (criterio de compatibilidad 1 de P-26).
                return new ResultadoEnrutamiento.ContinuarConversacion(
                    unico.Candidato, mensaje, null, ClasificacionPrevia: clasificacionPrevia);
            }

            // Elegible solo por participacion continua: el aporte abre un ciclo nuevo (§5.7) y la
            // pregunta se resuelve automaticamente o por menu (§5.4).
            return await ResolverDentroDeCampaniaAsync(usuario, unico.Candidato, mensaje, ahora, cancellationToken);
        }

        return await ConservarYOfrecerAsync(usuario, elegibles, mensaje, ahora, cancellationToken);
    }

    public async Task ConfirmarProcesadoAsync(
        string usuarioId,
        string whatsappMessageIdOriginal,
        CancellationToken cancellationToken)
    {
        var enrutamiento = await _enrutamientos.ObtenerPorMensajeAsync(
            usuarioId, whatsappMessageIdOriginal, cancellationToken);
        if (enrutamiento is null || enrutamiento.Estado != EstadoEnrutamientoAporte.Listo)
        {
            return;
        }

        var ahora = _tiempo.GetUtcNow();
        var conversacionId = await ResolverConversacionRecienteAsync(
            enrutamiento.CampaniaSeleccionadaId, usuarioId, enrutamiento.PreguntaSeleccionadaId, cancellationToken);
        await _enrutamientos.GuardarAsync(enrutamiento.MarcarEnIdea(conversacionId, ahora), cancellationToken);

        // §10: latencia desde que se conservo el aporte hasta que quedo procesado en su conversacion.
        var latenciaMs = (long)(ahora - enrutamiento.CreadoEn).TotalMilliseconds;
        await RegistrarUsuarioAsync(
            usuarioId,
            null,
            "procesado",
            $"{Detalle(enrutamiento)};latenciaMs={latenciaMs}",
            ahora,
            cancellationToken);
    }

    private async Task<ClasificacionIntencionPrevia?> ClasificarIntencionSemanticaAsync(
        Usuario usuario,
        IReadOnlyList<CandidatoCampania> candidatos,
        MensajeEntrante mensaje,
        bool haySeleccionPendiente,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        if (!_clasificacionSemanticaConsultaIdeaHabilitada
            || !_visibilidadIdeaParticipanteHabilitada
            || _respuestas is null
            || _configuracion is null
            || _clasificadorIntencion is null
            || _maxCaracteresConsultaIdea <= 0
            || string.IsNullOrWhiteSpace(mensaje.Texto)
            || mensaje.Texto.Trim().Length > _maxCaracteresConsultaIdea)
        {
            return null;
        }

        var contexto = await ResolverContextoClasificacionSemanticaAsync(
            usuario.Id, candidatos, ahora, cancellationToken);
        if (contexto is null
            || !contexto.Candidato.Campania.ConfigConversacional.ConsultaIdea
            || string.IsNullOrWhiteSpace(contexto.Candidato.Campania.ConfigLlmRef))
        {
            return null;
        }

        var config = await _configuracion.ObtenerConfigLlmAsync(
            contexto.Candidato.Campania.ConfigLlmRef, cancellationToken);
        if (config is null || config.Estado != EstadoRegistro.Activo)
        {
            return new ClasificacionIntencionPrevia(null, false);
        }

        var motivoCupo = _cuposHabilitados && _guardaCuposLlm is not null
            ? await _guardaCuposLlm.MotivoAsync(
                contexto.Candidato.Campania,
                usuario.Id,
                ahora,
                _consolidacionProgresivaHabilitada,
                cancellationToken)
            : null;
        if (motivoCupo is not null)
        {
            await RegistrarClasificacionSemanticaAsync(
                usuario, contexto.Candidato.Campania.Id, contexto.Estado, "omitida", null, null,
                motivoCupo, false, ahora, cancellationToken);
            return new ClasificacionIntencionPrevia(null, false);
        }

        var actoPrevio = contexto.Estado == EstadoMaquinaConversacion.EsperandoConfirmacionSalida
            ? ActoPrevioIntencionControl.Confirmar
            : ActoPrevioIntencionControl.Mejorar;
        var resultado = await _clasificadorIntencion.ClasificarAsync(
            new ContextoClasificacionIntencionControl(
                contexto.Estado,
                actoPrevio,
                contexto.HayIdeaDisponible,
                QuedanUnidadesPendientes: false,
                usuario.Idioma,
                mensaje.Texto,
                config,
                contexto.HayIdeaDisponible,
                haySeleccionPendiente,
                contexto.HayAfinidadConsultaIdea,
                _maxCaracteresConsultaIdea),
            cancellationToken);

        if (resultado is ResultadoClasificacionIntencionControl.Exito exito)
        {
            await RegistrarClasificacionSemanticaAsync(
                usuario, contexto.Candidato.Campania.Id, contexto.Estado,
                exito.Intencion == IntencionControl.Ambigua ? "ambigua" : "clasificada",
                exito.Intencion, exito.Uso, "ninguno", true, ahora, cancellationToken);
            return new ClasificacionIntencionPrevia(exito.Intencion, true);
        }

        var fallback = (ResultadoClasificacionIntencionControl.Fallback)resultado;
        await RegistrarClasificacionSemanticaAsync(
            usuario, contexto.Candidato.Campania.Id, contexto.Estado, "fallback", null, fallback.Uso,
            fallback.Motivo, true, ahora, cancellationToken);
        return new ClasificacionIntencionPrevia(null, true);
    }

    private async Task<ContextoSemantico?> ResolverContextoClasificacionSemanticaAsync(
        string usuarioId,
        IReadOnlyList<CandidatoCampania> candidatos,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        var afinidadConsulta = await ObtenerAfinidadConsultaVigenteAsync(
            usuarioId, candidatos, ahora, cancellationToken);
        if (afinidadConsulta is not null)
        {
            return new ContextoSemantico(
                afinidadConsulta.Candidato,
                afinidadConsulta.Conversacion!.EstadoMaquina,
                HayIdeaDisponible: afinidadConsulta.Enrutamiento.IdeaSeleccionadaId is not null,
                HayAfinidadConsultaIdea: true,
                IdeaAbierta: afinidadConsulta.Conversacion.Estado == EstadoConversacion.Abierta,
                IdeaActualizadaEn: afinidadConsulta.Enrutamiento.ActualizadoEn,
                IdeaIndice: 0,
                IdeaId: afinidadConsulta.Enrutamiento.IdeaSeleccionadaId);
        }

        var contextos = new List<ContextoSemantico>();
        foreach (var candidato in candidatos.Where(c => c.Campania.ConfigConversacional.ConsultaIdea))
        {
            var idea = (await _respuestas!.ListarIdeasConsolidadasAsync(candidato.Campania.Id, cancellationToken))
                .Where(x => x.UsuarioId == usuarioId
                    && x.EstadoResultado != ElTejido.Domain.Respuestas.EstadoResultadoIdeaConsolidada.Rechazada
                    && (!string.IsNullOrWhiteSpace(x.VersionConfirmadaRef) || !string.IsNullOrWhiteSpace(x.VersionPropuestaRef)))
                .OrderBy(x => x.EstadoFlujo == ElTejido.Domain.Respuestas.EstadoFlujoIdeaConsolidada.Cerrada)
                .ThenByDescending(x => x.ActualizadaEn)
                .FirstOrDefault();
            if (idea is null)
            {
                continue;
            }

            var conversacion = await _conversaciones.ObtenerConversacionAsync(
                candidato.Campania.Id, idea.ConversacionId, cancellationToken);
            contextos.Add(new ContextoSemantico(
                candidato,
                conversacion?.EstadoMaquina ?? EstadoMaquinaConversacion.EsperandoRespuestaInicial,
                HayIdeaDisponible: true,
                HayAfinidadConsultaIdea: false,
                IdeaAbierta: idea.EstadoFlujo != ElTejido.Domain.Respuestas.EstadoFlujoIdeaConsolidada.Cerrada,
                IdeaActualizadaEn: idea.ActualizadaEn,
                IdeaIndice: idea.IdeaIndice,
                IdeaId: idea.Id));
        }

        if (contextos.Count > 0)
        {
            return contextos
                .OrderByDescending(contexto => contexto.IdeaAbierta)
                .ThenByDescending(contexto => contexto.IdeaActualizadaEn)
                .ThenByDescending(contexto => contexto.IdeaIndice)
                .ThenBy(contexto => contexto.IdeaId, StringComparer.Ordinal)
                .First();
        }

        var sinIdea = candidatos.Where(c => c.Campania.ConfigConversacional.ConsultaIdea).ToArray();
        return sinIdea.Length == 1
            ? new ContextoSemantico(
                sinIdea[0], EstadoMaquinaConversacion.EsperandoRespuestaInicial,
                HayIdeaDisponible: false, HayAfinidadConsultaIdea: false,
                IdeaAbierta: false, IdeaActualizadaEn: null, IdeaIndice: 0, IdeaId: null)
            : null;
    }

    private Task RegistrarClasificacionSemanticaAsync(
        Usuario usuario,
        string campaniaId,
        EstadoMaquinaConversacion estado,
        string resultado,
        IntencionControl? intencion,
        ElTejido.Domain.Evaluacion.UsoTokensLlm? uso,
        string motivo,
        bool esLlamadaLlm,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        var valorIntencion = intencion switch
        {
            IntencionControl.Aportar => "aportar",
            IntencionControl.ConsultarIdea => "consultarIdea",
            IntencionControl.ConfirmarIdea => "confirmarIdea",
            IntencionControl.FinalizarIdea => "finalizarIdea",
            IntencionControl.FinalizarParticipacion => "finalizarParticipacion",
            _ => "ninguna",
        };
        var detalle = FormattableString.Invariant(
            $"componente:consultaIdea;origen:llm;resultado:{resultado};intencion:{valorIntencion};estado:{estado};promptTokens:{uso?.PromptTokens ?? 0};completionTokens:{uso?.CompletionTokens ?? 0};motivo:{motivo}");
        return _logSeguridad.RegistrarAsync(
            LogSeguridad.Crear(
                "log_" + Guid.NewGuid().ToString("N"),
                TipoEventoSeguridad.ClasificacionIntencionControl,
                usuario.Id,
                usuario.WhatsappNormalizado.Valor,
                resultado,
                detalle,
                _correlacion.CorrelationIdActual,
                ahora,
                campaniaId,
                uso?.PromptTokens ?? 0,
                uso?.CompletionTokens ?? 0,
                esLlamadaLlm),
            cancellationToken);
    }

    public async Task ConfirmarConsultaIdeaAsync(
        string usuarioId,
        string whatsappMessageIdOriginal,
        string ideaId,
        string conversacionId,
        CancellationToken cancellationToken)
    {
        var ruta = await _enrutamientos.ObtenerPorMensajeAsync(usuarioId, whatsappMessageIdOriginal, cancellationToken);
        if (ruta is null || ruta.Estado != EstadoEnrutamientoAporte.Listo || !ruta.EsConsultarIdea)
        {
            return;
        }

        await _enrutamientos.GuardarAsync(
            ruta.CompletarConsultaIdea(ideaId, conversacionId, _tiempo.GetUtcNow()), cancellationToken);
    }

    public async Task ConfirmarRetomadaAsync(
        string usuarioId,
        string whatsappMessageIdOriginal,
        bool completada,
        CancellationToken cancellationToken)
    {
        var enrutamiento = await _enrutamientos.ObtenerPorMensajeAsync(
            usuarioId, whatsappMessageIdOriginal, cancellationToken);
        if (enrutamiento is null || enrutamiento.Estado != EstadoEnrutamientoAporte.Listo || !enrutamiento.EsRetomarIdea)
        {
            return;
        }

        var ahora = _tiempo.GetUtcNow();
        await _enrutamientos.GuardarAsync(
            completada ? enrutamiento.CompletarRetomarIdea(ahora) : enrutamiento.Cancelar(ahora),
            cancellationToken);
    }

    private async Task<ResultadoEnrutamiento> ResolverSeleccionCampaniaAsync(
        Usuario usuario,
        IReadOnlyList<CandidatoCampania> candidatos,
        EnrutamientoAporte pendiente,
        MensajeEntrante mensaje,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        var opcion = InterpretarSeleccion(
            mensaje.Texto, pendiente.CampaniasOfrecidas, o => o.NombreSnapshot, o => o.Orden);
        if (opcion is null)
        {
            // §5.5: una opcion invalida conserva el aporte, queda auditada sin texto libre y vuelve a
            // pedir la seleccion.
            var invalido = pendiente.RegistrarIntento(
                new IntentoSeleccion(mensaje.WhatsappMessageId, TipoIntentoSeleccion.Campania, ResultadoIntentoSeleccion.Invalido, ahora),
                ahora);
            await _enrutamientos.GuardarAsync(invalido, cancellationToken);
            await RegistrarAsync(usuario, "invalido", Detalle(pendiente), ahora, cancellationToken);
            await EnviarMenuCampaniasAsync(usuario, invalido.Idioma, invalido.CampaniasOfrecidas, mensaje.PhoneNumberIdDestino, conAyuda: true, cancellationToken);
            return new ResultadoEnrutamiento.SeleccionPendiente(pendiente.Id);
        }

        // Revalidacion (§5.3/§10): el estado pudo cambiar desde que se ofrecio la lista.
        var elegibles = pendiente.EsRetomarIdea
            ? await CalcularElegiblesRetomarAsync(candidatos, usuario.Id, cancellationToken)
            : await CalcularElegiblesAsync(candidatos, usuario.Id, cancellationToken);
        var elegido = elegibles.FirstOrDefault(c => c.Candidato.Campania.Id == opcion.CampaniaId);
        if (elegido is null)
        {
            return await RecalcularTrasRevalidacionAsync(usuario, elegibles, pendiente, mensaje, ahora, cancellationToken);
        }

        var seleccionado = pendiente
            .RegistrarIntento(
                new IntentoSeleccion(mensaje.WhatsappMessageId, TipoIntentoSeleccion.Campania, ResultadoIntentoSeleccion.Valido, ahora),
                ahora)
            .SeleccionarCampania(elegido.Candidato.Campania.Id, ahora);
        await RegistrarAsync(usuario, "seleccionado", Detalle(seleccionado), ahora, cancellationToken);

        return await ResolverPreguntaTrasCampaniaAsync(usuario, elegido.Candidato, seleccionado, mensaje, ahora, cancellationToken);
    }

    /// <summary>
    /// §5.4: dentro de la campania ya elegida, una pregunta elegible se selecciona sola; con varias se
    /// ofrece la lista numerada. El enrutamiento llega en estado <c>seleccionPregunta</c>.
    /// </summary>
    private async Task<ResultadoEnrutamiento> ResolverPreguntaTrasCampaniaAsync(
        Usuario usuario,
        CandidatoCampania candidato,
        EnrutamientoAporte enrutamiento,
        MensajeEntrante mensaje,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        var preguntas = enrutamiento.EsRetomarIdea
            ? await PreguntasConIdeasHistoricasAsync(candidato.Campania, usuario.Id, cancellationToken)
            : await PreguntasElegiblesAsync(candidato.Campania, usuario.Id, cancellationToken);
        if (preguntas.Count == 0)
        {
            await _enrutamientos.GuardarAsync(enrutamiento.Cancelar(ahora), cancellationToken);
            return new ResultadoEnrutamiento.SinElegibles();
        }

        if (preguntas.Count == 1)
        {
            var listo = enrutamiento.SeleccionarPregunta(preguntas[0].Id, ahora);
            await _enrutamientos.GuardarAsync(listo, cancellationToken);
            return await EntregarResueltoAsync(usuario, candidato, listo, preguntas[0], mensaje, ahora, cancellationToken);
        }

        var ofrecido = enrutamiento.OfrecerPreguntas(OpcionesPregunta(preguntas), ahora);
        await _enrutamientos.GuardarAsync(ofrecido, cancellationToken);
        await RegistrarAsync(usuario, "ofrecido", Detalle(ofrecido), ahora, cancellationToken);
        await EnviarMenuPreguntasAsync(usuario, ofrecido.Idioma, ofrecido.PreguntasOfrecidas, mensaje.PhoneNumberIdDestino, conAyuda: false, cancellationToken);
        return new ResultadoEnrutamiento.SeleccionPendiente(ofrecido.Id);
    }

    private async Task<ResultadoEnrutamiento> ResolverSeleccionPreguntaAsync(
        Usuario usuario,
        IReadOnlyList<CandidatoCampania> candidatos,
        EnrutamientoAporte pendiente,
        MensajeEntrante mensaje,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        // §5.1 paso 3: el cambio explicito de campania tambien aplica durante la seleccion de pregunta.
        if (await CoincideCambioCampaniaAsync(pendiente.Idioma, mensaje.Texto, cancellationToken))
        {
            var elegiblesCambio = pendiente.EsRetomarIdea
                ? await CalcularElegiblesRetomarAsync(candidatos, usuario.Id, cancellationToken)
                : await CalcularElegiblesAsync(candidatos, usuario.Id, cancellationToken);
            await RegistrarAsync(usuario, "cambioCampania", Detalle(pendiente), ahora, cancellationToken);
            return await OfrecerCampaniasDeNuevoAsync(usuario, elegiblesCambio, pendiente, mensaje, ahora, cancellationToken);
        }

        var candidato = candidatos.FirstOrDefault(c => c.Campania.Id == pendiente.CampaniaSeleccionadaId);
        if (candidato is null)
        {
            // La campania elegida dejo de estar autorizada entre la oferta y la seleccion (§11).
            var elegiblesActuales = pendiente.EsRetomarIdea
                ? await CalcularElegiblesRetomarAsync(candidatos, usuario.Id, cancellationToken)
                : await CalcularElegiblesAsync(candidatos, usuario.Id, cancellationToken);
            await RegistrarAsync(usuario, "invalido", Detalle(pendiente) + ";revalidacion", ahora, cancellationToken);
            return await OfrecerCampaniasDeNuevoAsync(usuario, elegiblesActuales, pendiente, mensaje, ahora, cancellationToken);
        }

        var opcion = InterpretarSeleccion(
            mensaje.Texto, pendiente.PreguntasOfrecidas, o => o.TextoSnapshot, o => o.Orden);
        if (opcion is null)
        {
            var invalido = pendiente.RegistrarIntento(
                new IntentoSeleccion(mensaje.WhatsappMessageId, TipoIntentoSeleccion.Pregunta, ResultadoIntentoSeleccion.Invalido, ahora),
                ahora);
            await _enrutamientos.GuardarAsync(invalido, cancellationToken);
            await RegistrarAsync(usuario, "invalido", Detalle(pendiente), ahora, cancellationToken);
            await EnviarMenuPreguntasAsync(usuario, invalido.Idioma, invalido.PreguntasOfrecidas, mensaje.PhoneNumberIdDestino, conAyuda: true, cancellationToken);
            return new ResultadoEnrutamiento.SeleccionPendiente(pendiente.Id);
        }

        // Revalidacion: la pregunta pudo desactivarse o completarse desde que se ofrecio (§11).
        var vigentes = pendiente.EsRetomarIdea
            ? await PreguntasConIdeasHistoricasAsync(candidato.Campania, usuario.Id, cancellationToken)
            : await PreguntasElegiblesAsync(candidato.Campania, usuario.Id, cancellationToken);
        var pregunta = vigentes.FirstOrDefault(p => p.Id == opcion.PreguntaId);
        if (pregunta is null)
        {
            var invalidado = pendiente.RegistrarIntento(
                new IntentoSeleccion(mensaje.WhatsappMessageId, TipoIntentoSeleccion.Pregunta, ResultadoIntentoSeleccion.Invalido, ahora),
                ahora);
            await RegistrarAsync(usuario, "invalido", Detalle(pendiente) + ";revalidacion", ahora, cancellationToken);
            if (vigentes.Count == 0)
            {
                await _enrutamientos.GuardarAsync(invalidado.Cancelar(ahora), cancellationToken);
                return new ResultadoEnrutamiento.SinElegibles();
            }

            if (vigentes.Count == 1)
            {
                var listoUnico = invalidado.SeleccionarPregunta(vigentes[0].Id, ahora);
                await _enrutamientos.GuardarAsync(listoUnico, cancellationToken);
                return await EntregarResueltoAsync(usuario, candidato, listoUnico, vigentes[0], mensaje, ahora, cancellationToken);
            }

            var reofrecido = invalidado.OfrecerPreguntas(OpcionesPregunta(vigentes), ahora);
            await _enrutamientos.GuardarAsync(reofrecido, cancellationToken);
            await RegistrarAsync(usuario, "ofrecido", Detalle(reofrecido), ahora, cancellationToken);
            await EnviarMenuPreguntasAsync(usuario, reofrecido.Idioma, reofrecido.PreguntasOfrecidas, mensaje.PhoneNumberIdDestino, conAyuda: true, cancellationToken);
            return new ResultadoEnrutamiento.SeleccionPendiente(reofrecido.Id);
        }

        var listo = pendiente
            .RegistrarIntento(
                new IntentoSeleccion(mensaje.WhatsappMessageId, TipoIntentoSeleccion.Pregunta, ResultadoIntentoSeleccion.Valido, ahora),
                ahora)
            .SeleccionarPregunta(pregunta.Id, ahora);
        await _enrutamientos.GuardarAsync(listo, cancellationToken);
        await RegistrarAsync(usuario, "seleccionado", Detalle(listo), ahora, cancellationToken);
        return await EntregarResueltoAsync(usuario, candidato, listo, pregunta, mensaje, ahora, cancellationToken);
    }

    /// <summary>
    /// Entrega final con campania y pregunta resueltas. Un enrutamiento con <c>procesadoEn</c> ya
    /// fijado proviene de un cambio explicito de campania: su aporte ya se proceso, asi que solo se
    /// establece la afinidad (§5.6) sin volver a entregar texto.
    /// </summary>
    private async Task<ResultadoEnrutamiento> EntregarResueltoAsync(
        Usuario usuario,
        CandidatoCampania candidato,
        EnrutamientoAporte enrutamiento,
        Pregunta pregunta,
        MensajeEntrante mensajeSeleccion,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        if (enrutamiento.ProcesadoEn is not null)
        {
            var abierta = await ResolverConversacionAbiertaAsync(
                candidato.Campania.Id, usuario.Id, pregunta.Id, cancellationToken);
            var afinidad = enrutamiento.EstablecerAfinidad(abierta?.Id, ahora);
            await _enrutamientos.GuardarAsync(afinidad, cancellationToken);
            return new ResultadoEnrutamiento.CambioCampaniaAplicado(candidato, abierta);
        }

        if (enrutamiento.EsEntradaProactiva)
        {
            await _enrutamientos.GuardarAsync(enrutamiento.CompletarEntradaProactiva(ahora), cancellationToken);
            return new ResultadoEnrutamiento.DespertarProactivo(candidato);
        }

        if (enrutamiento.EsRetomarIdea)
        {
            return await ResolverIdeasTrasPreguntaAsync(
                usuario, candidato, pregunta, enrutamiento, mensajeSeleccion, ahora, cancellationToken);
        }

        return new ResultadoEnrutamiento.ContinuarConversacion(
            candidato,
            MensajeOriginal(enrutamiento, mensajeSeleccion, ahora),
            enrutamiento.Id,
            new ContextoAporteEnrutado(pregunta.Id, enrutamiento.Id));
    }

    private async Task<ResultadoEnrutamiento> IniciarRetomarIdeaAsync(
        Usuario usuario,
        IReadOnlyList<CandidatoCampania> candidatos,
        MensajeEntrante mensaje,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        var elegibles = await CalcularElegiblesRetomarAsync(candidatos, usuario.Id, cancellationToken);
        if (elegibles.Count == 0)
        {
            await EnviarSinIdeasHistoricasAsync(usuario, usuario.Idioma, mensaje.PhoneNumberIdDestino, cancellationToken);
            return new ResultadoEnrutamiento.SinElegibles();
        }

        var existente = await _enrutamientos.ObtenerPorMensajeAsync(
            usuario.Id, mensaje.WhatsappMessageId, cancellationToken);
        if (existente is not null)
        {
            return new ResultadoEnrutamiento.SeleccionPendiente(existente.Id);
        }

        if (elegibles.Count > 1)
        {
            var seleccionarCampania = EnrutamientoAporte.Crear(
                usuario.Id,
                mensaje.WhatsappMessageId,
                mensaje.Texto,
                EstadoEnrutamientoAporte.SeleccionCampania,
                ahora,
                phoneNumberIdDestino: mensaje.PhoneNumberIdDestino,
                campaniasOfrecidas: Opciones(elegibles),
                modo: ModoEnrutamientoAporte.RetomarIdea,
                idioma: usuario.Idioma);
            await _enrutamientos.GuardarAsync(seleccionarCampania, cancellationToken);
            await RegistrarRetomarAsync(usuario, "ofrecido", seleccionarCampania, ahora, cancellationToken);
            await EnviarMenuCampaniasAsync(
                usuario, seleccionarCampania.Idioma, seleccionarCampania.CampaniasOfrecidas, mensaje.PhoneNumberIdDestino, false, cancellationToken);
            return new ResultadoEnrutamiento.SeleccionPendiente(seleccionarCampania.Id);
        }

        var unico = elegibles[0].Candidato;
        var preguntas = await PreguntasConIdeasHistoricasAsync(unico.Campania, usuario.Id, cancellationToken);
        var enrutamiento = EnrutamientoAporte.Crear(
            usuario.Id,
            mensaje.WhatsappMessageId,
            mensaje.Texto,
            EstadoEnrutamientoAporte.SeleccionPregunta,
            ahora,
            phoneNumberIdDestino: mensaje.PhoneNumberIdDestino,
            campaniaSeleccionadaId: unico.Campania.Id,
            preguntasOfrecidas: OpcionesPregunta(preguntas),
            modo: ModoEnrutamientoAporte.RetomarIdea,
            idioma: usuario.Idioma);

        if (preguntas.Count == 1)
        {
            var listo = enrutamiento.SeleccionarPregunta(preguntas[0].Id, ahora);
            await _enrutamientos.GuardarAsync(listo, cancellationToken);
            return await ResolverIdeasTrasPreguntaAsync(
                usuario, unico, preguntas[0], listo, mensaje, ahora, cancellationToken);
        }

        await _enrutamientos.GuardarAsync(enrutamiento, cancellationToken);
        await RegistrarRetomarAsync(usuario, "ofrecido", enrutamiento, ahora, cancellationToken);
        await EnviarMenuPreguntasAsync(
            usuario, enrutamiento.Idioma, enrutamiento.PreguntasOfrecidas, mensaje.PhoneNumberIdDestino, false, cancellationToken);
        return new ResultadoEnrutamiento.SeleccionPendiente(enrutamiento.Id);
    }

    private async Task<ResultadoEnrutamiento> ResolverConsultaIdeaAsync(
        Usuario usuario,
        IReadOnlyList<CandidatoCampania> candidatos,
        MensajeEntrante mensaje,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        var candidatas = new List<(CandidatoCampania Candidato, ElTejido.Domain.Respuestas.IdeaConsolidada Idea)>();
        foreach (var candidato in candidatos.Where(c => c.Campania.ConfigConversacional.ConsultaIdea))
        {
            var idea = (await _respuestas!.ListarIdeasConsolidadasAsync(candidato.Campania.Id, cancellationToken))
                .Where(x => x.UsuarioId == usuario.Id
                    && x.EstadoResultado != ElTejido.Domain.Respuestas.EstadoResultadoIdeaConsolidada.Rechazada
                    && (!string.IsNullOrWhiteSpace(x.VersionConfirmadaRef) || !string.IsNullOrWhiteSpace(x.VersionPropuestaRef)))
                .OrderByDescending(x => x.ActualizadaEn)
                .FirstOrDefault();
            if (idea is not null)
            {
                candidatas.Add((candidato, idea));
            }
        }

        // Un coach conserva primero el hilo todavía abierto; solo sin uno elige la última idea propia.
        var activas = candidatas
            .Where(x => x.Idea.EstadoFlujo != ElTejido.Domain.Respuestas.EstadoFlujoIdeaConsolidada.Cerrada)
            .ToArray();
        var elegida = (activas.Length > 0 ? activas : candidatas.ToArray())
            .OrderByDescending(x => x.Idea.ActualizadaEn)
            .ThenByDescending(x => x.Idea.IdeaIndice)
            .ThenBy(x => x.Idea.Id, StringComparer.Ordinal)
            .FirstOrDefault();
        if (elegida.Candidato is null)
        {
            var respaldo = candidatos.FirstOrDefault(c => c.Campania.ConfigConversacional.ConsultaIdea);
            return respaldo is null
                ? new ResultadoEnrutamiento.SinElegibles()
                : new ResultadoEnrutamiento.ConsultarIdea(
                    respaldo, mensaje, new ContextoConsultaIdea(null, null, null, null, false));
        }

        var conversacion = await _conversaciones.ObtenerConversacionAsync(
            elegida.Candidato.Campania.Id, elegida.Idea.ConversacionId, cancellationToken);
        string? rutaId = null;
        var cerrada = conversacion?.Estado == EstadoConversacion.Cerrada;
        if (conversacion is not null)
        {
            var ruta = EnrutamientoAporte.Crear(
                usuario.Id, mensaje.WhatsappMessageId, mensaje.Texto, EstadoEnrutamientoAporte.Listo, ahora,
                phoneNumberIdDestino: mensaje.PhoneNumberIdDestino,
                campaniaSeleccionadaId: elegida.Candidato.Campania.Id,
                preguntaSeleccionadaId: elegida.Idea.PreguntaId,
                conversacionId: elegida.Idea.ConversacionId,
                modo: ModoEnrutamientoAporte.ConsultarIdea,
                ideaSeleccionadaId: elegida.Idea.Id,
                idioma: usuario.Idioma);
            await _enrutamientos.GuardarAsync(ruta, cancellationToken);
            rutaId = ruta.Id;
        }

        await RegistrarAsync(usuario, "consulta", $"idea:{elegida.Idea.Id};cerrada:{cerrada}", ahora, cancellationToken);
        return new ResultadoEnrutamiento.ConsultarIdea(
            elegida.Candidato,
            mensaje,
            new ContextoConsultaIdea(elegida.Idea.PreguntaId, elegida.Idea.Id, elegida.Idea.ConversacionId, rutaId, cerrada));
    }

    private async Task<ResultadoEnrutamiento> ResolverIdeasTrasPreguntaAsync(
        Usuario usuario,
        CandidatoCampania candidato,
        Pregunta pregunta,
        EnrutamientoAporte enrutamiento,
        MensajeEntrante mensaje,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        var opciones = await OpcionesIdeasHistoricasAsync(
            candidato.Campania.Id, usuario.Id, pregunta.Id, cancellationToken);
        if (opciones.Count == 0)
        {
            await _enrutamientos.GuardarAsync(enrutamiento.Cancelar(ahora), cancellationToken);
            await EnviarSinIdeasHistoricasAsync(usuario, enrutamiento.Idioma, mensaje.PhoneNumberIdDestino, cancellationToken);
            return new ResultadoEnrutamiento.SinElegibles();
        }

        var ofrecido = enrutamiento.OfrecerIdeas(opciones, ahora);
        await RegistrarRetomarAsync(usuario, "ofrecido", ofrecido, ahora, cancellationToken);
        if (opciones.Count == 1)
        {
            var listo = ofrecido.SeleccionarIdea(opciones[0], ahora);
            await _enrutamientos.GuardarAsync(listo, cancellationToken);
            await RegistrarRetomarAsync(usuario, "seleccionado", listo, ahora, cancellationToken);
            return CrearResultadoRetomar(candidato, mensaje, listo);
        }

        await _enrutamientos.GuardarAsync(ofrecido, cancellationToken);
        await EnviarMenuIdeasAsync(usuario, ofrecido.Idioma, opciones, mensaje.PhoneNumberIdDestino, false, cancellationToken);
        return new ResultadoEnrutamiento.SeleccionPendiente(ofrecido.Id);
    }

    private async Task<ResultadoEnrutamiento> ResolverSeleccionIdeaHistoricaAsync(
        Usuario usuario,
        IReadOnlyList<CandidatoCampania> candidatos,
        EnrutamientoAporte pendiente,
        MensajeEntrante mensaje,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        var opcion = InterpretarSeleccion(
            mensaje.Texto, pendiente.IdeasOfrecidas, idea => idea.ResumenSnapshot, idea => idea.Orden);
        if (opcion is null)
        {
            var invalido = pendiente.RegistrarIntento(
                new IntentoSeleccion(
                    mensaje.WhatsappMessageId, TipoIntentoSeleccion.Idea, ResultadoIntentoSeleccion.Invalido, ahora),
                ahora);
            await _enrutamientos.GuardarAsync(invalido, cancellationToken);
            await RegistrarRetomarAsync(usuario, "invalido", invalido, ahora, cancellationToken);
            await EnviarMenuIdeasAsync(usuario, invalido.Idioma, invalido.IdeasOfrecidas, mensaje.PhoneNumberIdDestino, true, cancellationToken);
            return new ResultadoEnrutamiento.SeleccionPendiente(invalido.Id);
        }

        var candidato = candidatos.FirstOrDefault(c => c.Campania.Id == pendiente.CampaniaSeleccionadaId);
        var preguntaActiva = candidato?.Campania.Preguntas.Any(
            pregunta => pregunta.Id == pendiente.PreguntaSeleccionadaId && pregunta.Estado == EstadoRegistro.Activo) == true;
        var idea = candidato is null || !preguntaActiva || _respuestas is null
            ? null
            : (await _respuestas.ListarIdeasHistoricasAsync(
                    candidato.Campania.Id, usuario.Id, pendiente.PreguntaSeleccionadaId!, cancellationToken))
                .FirstOrDefault(candidata => candidata.Id == opcion.IdeaId
                    && candidata.ConversacionId == opcion.ConversacionId
                    && candidata.CampaniaId == candidato.Campania.Id
                    && candidata.UsuarioId == usuario.Id
                    && candidata.PreguntaId == pendiente.PreguntaSeleccionadaId);
        if (idea is null)
        {
            await _enrutamientos.GuardarAsync(pendiente.Cancelar(ahora), cancellationToken);
            await RegistrarRetomarAsync(usuario, "invalido", pendiente, ahora, cancellationToken);
            await EnviarSinIdeasHistoricasAsync(usuario, pendiente.Idioma, mensaje.PhoneNumberIdDestino, cancellationToken);
            return new ResultadoEnrutamiento.SinElegibles();
        }

        var listo = pendiente
            .RegistrarIntento(
                new IntentoSeleccion(
                    mensaje.WhatsappMessageId, TipoIntentoSeleccion.Idea, ResultadoIntentoSeleccion.Valido, ahora),
                ahora)
            .SeleccionarIdea(opcion, ahora);
        await _enrutamientos.GuardarAsync(listo, cancellationToken);
        await RegistrarRetomarAsync(usuario, "seleccionado", listo, ahora, cancellationToken);
        return CrearResultadoRetomar(candidato!, mensaje, listo);
    }

    private async Task<ResultadoEnrutamiento> EntregarRetomadaListaAsync(
        Usuario usuario,
        IReadOnlyList<CandidatoCampania> candidatos,
        EnrutamientoAporte enrutamiento,
        MensajeEntrante mensaje,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        var candidato = candidatos.FirstOrDefault(c => c.Campania.Id == enrutamiento.CampaniaSeleccionadaId);
        if (candidato is null
            || candidato.Campania.Estado != EstadoCampania.Activa
            || string.IsNullOrWhiteSpace(enrutamiento.PreguntaSeleccionadaId)
            || !candidato.Campania.Preguntas.Any(p => p.Id == enrutamiento.PreguntaSeleccionadaId && p.Estado == EstadoRegistro.Activo))
        {
            await _enrutamientos.GuardarAsync(enrutamiento.Cancelar(ahora), cancellationToken);
            return new ResultadoEnrutamiento.SinElegibles();
        }

        return CrearResultadoRetomar(candidato, mensaje, enrutamiento);
    }

    private static ResultadoEnrutamiento.RetomarIdea CrearResultadoRetomar(
        CandidatoCampania candidato,
        MensajeEntrante mensaje,
        EnrutamientoAporte enrutamiento)
        => new(
            candidato,
            mensaje,
            new ContextoRetomarIdea(
                enrutamiento.PreguntaSeleccionadaId!,
                enrutamiento.IdeaSeleccionadaId!,
                enrutamiento.ConversacionId!,
                enrutamiento.Id,
                enrutamiento.WhatsappMessageId));

    private async Task<ResultadoEnrutamiento> RecalcularTrasRevalidacionAsync(
        Usuario usuario,
        IReadOnlyList<CampaniaElegible> elegibles,
        EnrutamientoAporte pendiente,
        MensajeEntrante mensaje,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        // §11: la campania dejo de ser elegible entre la oferta y la seleccion; no se procesa alli.
        var invalidado = pendiente.RegistrarIntento(
            new IntentoSeleccion(mensaje.WhatsappMessageId, TipoIntentoSeleccion.Campania, ResultadoIntentoSeleccion.Invalido, ahora),
            ahora);
        await RegistrarAsync(usuario, "invalido", Detalle(pendiente) + ";revalidacion", ahora, cancellationToken);
        return await OfrecerCampaniasDeNuevoAsync(usuario, elegibles, invalidado, mensaje, ahora, cancellationToken);
    }

    /// <summary>Recalculo de opciones de campania: 0 cancela auditable, 1 se selecciona sin menu, N reoferta.</summary>
    private async Task<ResultadoEnrutamiento> OfrecerCampaniasDeNuevoAsync(
        Usuario usuario,
        IReadOnlyList<CampaniaElegible> elegibles,
        EnrutamientoAporte enrutamiento,
        MensajeEntrante mensaje,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        if (elegibles.Count == 0)
        {
            await _enrutamientos.GuardarAsync(enrutamiento.Cancelar(ahora), cancellationToken);
            return new ResultadoEnrutamiento.SinElegibles();
        }

        if (elegibles.Count == 1)
        {
            var unico = enrutamiento
                .OfrecerCampanias(Opciones(elegibles), ahora)
                .SeleccionarCampania(elegibles[0].Candidato.Campania.Id, ahora);
            await RegistrarAsync(usuario, "seleccionado", Detalle(unico), ahora, cancellationToken);
            return await ResolverPreguntaTrasCampaniaAsync(usuario, elegibles[0].Candidato, unico, mensaje, ahora, cancellationToken);
        }

        var reofrecido = enrutamiento.OfrecerCampanias(Opciones(elegibles), ahora);
        await _enrutamientos.GuardarAsync(reofrecido, cancellationToken);
        await RegistrarAsync(usuario, "ofrecido", Detalle(reofrecido), ahora, cancellationToken);
        await EnviarMenuCampaniasAsync(usuario, reofrecido.Idioma, reofrecido.CampaniasOfrecidas, mensaje.PhoneNumberIdDestino, conAyuda: true, cancellationToken);
        return new ResultadoEnrutamiento.SeleccionPendiente(reofrecido.Id);
    }

    private async Task<ResultadoEnrutamiento> ConservarYOfrecerAsync(
        Usuario usuario,
        IReadOnlyList<CampaniaElegible> elegibles,
        MensajeEntrante mensaje,
        DateTimeOffset ahora,
        CancellationToken cancellationToken,
        bool esEntradaProactiva = false)
    {
        // Reintento interno con el mismo mensaje raiz: el id determinista reutiliza el documento y no
        // se ofrece un segundo menu ni se pierde la auditoria previa (§5.5).
        var existente = await _enrutamientos.ObtenerPorMensajeAsync(
            usuario.Id, mensaje.WhatsappMessageId, cancellationToken);
        if (existente is not null)
        {
            return new ResultadoEnrutamiento.SeleccionPendiente(existente.Id);
        }

        var enrutamiento = EnrutamientoAporte.Crear(
            usuario.Id,
            mensaje.WhatsappMessageId,
            mensaje.Texto,
            EstadoEnrutamientoAporte.SeleccionCampania,
            ahora,
            phoneNumberIdDestino: mensaje.PhoneNumberIdDestino,
            campaniasOfrecidas: Opciones(elegibles),
            esEntradaProactiva: esEntradaProactiva,
            idioma: usuario.Idioma);

        // §11: primero se conserva el aporte; si Cosmos falla no se muestra un menu que pueda perderlo.
        await _enrutamientos.GuardarAsync(enrutamiento, cancellationToken);
        await RegistrarAsync(usuario, "ofrecido", Detalle(enrutamiento), ahora, cancellationToken);
        await EnviarMenuCampaniasAsync(usuario, enrutamiento.Idioma, enrutamiento.CampaniasOfrecidas, mensaje.PhoneNumberIdDestino, conAyuda: false, cancellationToken);
        return new ResultadoEnrutamiento.SeleccionPendiente(enrutamiento.Id);
    }

    /// <summary>
    /// §5.4/§5.7: resuelve la pregunta dentro de una campania ya determinada (unica elegible continua
    /// sin trabajo pendiente, o afinidad a campania tras un cambio explicito). El mensaje actual ES el
    /// aporte: con una pregunta elegible se entrega dirigido; con varias se conserva y se pide la
    /// pregunta.
    /// </summary>
    private async Task<ResultadoEnrutamiento> ResolverDentroDeCampaniaAsync(
        Usuario usuario,
        CandidatoCampania candidato,
        MensajeEntrante mensaje,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        var preguntas = await PreguntasElegiblesAsync(candidato.Campania, usuario.Id, cancellationToken);
        if (preguntas.Count == 0)
        {
            return new ResultadoEnrutamiento.SinElegibles();
        }

        if (preguntas.Count == 1)
        {
            return new ResultadoEnrutamiento.ContinuarConversacion(
                candidato,
                mensaje,
                null,
                new ContextoAporteEnrutado(preguntas[0].Id, null));
        }

        var existente = await _enrutamientos.ObtenerPorMensajeAsync(
            usuario.Id, mensaje.WhatsappMessageId, cancellationToken);
        if (existente is not null)
        {
            return new ResultadoEnrutamiento.SeleccionPendiente(existente.Id);
        }

        var enrutamiento = EnrutamientoAporte.Crear(
            usuario.Id,
            mensaje.WhatsappMessageId,
            mensaje.Texto,
            EstadoEnrutamientoAporte.SeleccionPregunta,
            ahora,
            phoneNumberIdDestino: mensaje.PhoneNumberIdDestino,
            campaniaSeleccionadaId: candidato.Campania.Id,
            preguntasOfrecidas: OpcionesPregunta(preguntas),
            idioma: usuario.Idioma);
        await _enrutamientos.GuardarAsync(enrutamiento, cancellationToken);
        await RegistrarAsync(usuario, "ofrecido", Detalle(enrutamiento), ahora, cancellationToken);
        await EnviarMenuPreguntasAsync(usuario, enrutamiento.Idioma, enrutamiento.PreguntasOfrecidas, mensaje.PhoneNumberIdDestino, conAyuda: false, cancellationToken);
        return new ResultadoEnrutamiento.SeleccionPendiente(enrutamiento.Id);
    }

    /// <summary>
    /// §5.1 paso 3: "otra campaña" suspende la afinidad sin cerrar ni rechazar la idea y recalcula las
    /// opciones sobre el mismo enrutamiento (su aporte original conserva la auditoria y, por tener
    /// <c>procesadoEn</c>, nunca vuelve a entregarse).
    /// </summary>
    private async Task<ResultadoEnrutamiento> SuspenderAfinidadYReofrecerAsync(
        Usuario usuario,
        IReadOnlyList<CandidatoCampania> candidatos,
        AfinidadVigente afinidad,
        MensajeEntrante mensaje,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        var elegibles = await CalcularElegiblesAsync(candidatos, usuario.Id, cancellationToken);
        await RegistrarAsync(usuario, "cambioCampania", Detalle(afinidad.Enrutamiento), ahora, cancellationToken);

        if (elegibles.Count == 1 && elegibles[0].Candidato.Campania.Id == afinidad.Candidato.Campania.Id)
        {
            // No hay otra campania a la cual cambiar: la afinidad actual permanece y se reengancha el
            // turno pendiente de esa conversacion.
            return new ResultadoEnrutamiento.CambioCampaniaAplicado(afinidad.Candidato, afinidad.Conversacion);
        }

        return await OfrecerCampaniasDeNuevoAsync(usuario, elegibles, afinidad.Enrutamiento, mensaje, ahora, cancellationToken);
    }

    /// <summary>
    /// §5.6: afinidad vigente = enrutamiento <c>enIdea</c> mas reciente cuya campania sigue autorizada
    /// y cuya conversacion sigue abierta con la ventana de servicio (24 h desde el ultimo mensaje)
    /// abierta. Una conversacion ya cerrada marca el enrutamiento <c>completado</c>. Un enrutamiento
    /// sin conversacion (cambio de campania) mantiene la afinidad a la campania por 24 h.
    /// </summary>
    private async Task<AfinidadVigente?> ObtenerAfinidadConsultaVigenteAsync(
        string usuarioId,
        IReadOnlyList<CandidatoCampania> candidatos,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        var rutas = await _enrutamientos.ListarPorUsuarioAsync(usuarioId, cancellationToken);
        foreach (var ruta in rutas
                     .Where(candidata => candidata.Estado == EstadoEnrutamientoAporte.EnIdea
                         && candidata.EsConsultarIdea
                         && ahora < candidata.ActualizadoEn + VigenciaAfinidad)
                     .OrderByDescending(candidata => candidata.ActualizadoEn))
        {
            if (ruta.CampaniaSeleccionadaId is null || ruta.ConversacionId is null)
            {
                continue;
            }

            var candidato = candidatos.FirstOrDefault(
                actual => actual.Campania.Id == ruta.CampaniaSeleccionadaId);
            if (candidato is null)
            {
                continue;
            }

            var conversacion = await _conversaciones.ObtenerConversacionAsync(
                candidato.Campania.Id, ruta.ConversacionId, cancellationToken);
            if (conversacion is not null && conversacion.UsuarioId == usuarioId)
            {
                return new AfinidadVigente(ruta, candidato, conversacion);
            }
        }

        return null;
    }

    private async Task<AfinidadVigente?> ObtenerAfinidadVigenteAsync(
        string usuarioId,
        IReadOnlyList<CandidatoCampania> candidatos,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        var enrutamientos = await _enrutamientos.ListarPorUsuarioAsync(usuarioId, cancellationToken);
        var enIdea = enrutamientos
            .Where(e => e.Estado == EstadoEnrutamientoAporte.EnIdea)
            .OrderByDescending(e => e.ActualizadoEn)
            .FirstOrDefault();
        if (enIdea?.CampaniaSeleccionadaId is null)
        {
            return null;
        }

        var candidato = candidatos.FirstOrDefault(c => c.Campania.Id == enIdea.CampaniaSeleccionadaId);
        if (candidato is null)
        {
            return null;
        }

        if (enIdea.ConversacionId is null)
        {
            return ahora < enIdea.ActualizadoEn + VigenciaAfinidad
                ? new AfinidadVigente(enIdea, candidato, null)
                : null;
        }

        var conversacion = await _conversaciones.ObtenerConversacionAsync(
            candidato.Campania.Id, enIdea.ConversacionId, cancellationToken);
        if (conversacion is null)
        {
            return null;
        }

        if (conversacion.Estado == EstadoConversacion.Cerrada)
        {
            if (enIdea.EsConsultarIdea && ahora < enIdea.ActualizadoEn + VigenciaAfinidad)
            {
                return new AfinidadVigente(enIdea, candidato, conversacion);
            }

            // §5.6: cuando la idea termina, el enrutamiento se marca completado y el siguiente aporte
            // vuelve a resolver campania/pregunta.
            await _enrutamientos.GuardarAsync(enIdea.Completar(ahora), cancellationToken);
            return null;
        }

        return conversacion.VentanaAbierta(ahora)
            ? new AfinidadVigente(enIdea, candidato, conversacion)
            : null;
    }

    /// <summary>
    /// Campanias elegibles (§5.2): activa + asociacion/usuario activos + pregunta activa (todo eso ya
    /// garantizado por los candidatos) y ademas trabajo pendiente o participacion continua.
    /// </summary>
    private async Task<IReadOnlyList<CampaniaElegible>> CalcularElegiblesAsync(
        IReadOnlyList<CandidatoCampania> candidatos,
        string usuarioId,
        CancellationToken cancellationToken)
    {
        var elegibles = new List<CampaniaElegible>();
        foreach (var candidato in candidatos)
        {
            if (EsCampaniaBilingueIncompleta(candidato.Campania))
            {
                continue;
            }

            var pendiente = await TieneTrabajoPendienteAsync(candidato.Campania, usuarioId, cancellationToken);
            if (pendiente || candidato.Campania.ConfigConversacional.ParticipacionContinua)
            {
                elegibles.Add(new CampaniaElegible(candidato, pendiente));
            }
        }

        return elegibles
            .OrderBy(c => c.Candidato.Campania.Nombre, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.Candidato.Campania.Id, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>P-30: campañas autorizadas y activas que realmente contienen ideas del participante.</summary>
    private async Task<IReadOnlyList<CampaniaElegible>> CalcularElegiblesRetomarAsync(
        IReadOnlyList<CandidatoCampania> candidatos,
        string usuarioId,
        CancellationToken cancellationToken)
    {
        if (_respuestas is null)
        {
            return Array.Empty<CampaniaElegible>();
        }

        var elegibles = new List<CampaniaElegible>();
        foreach (var candidato in candidatos.Where(c => c.Campania.Estado == EstadoCampania.Activa && !EsCampaniaBilingueIncompleta(c.Campania)))
        {
            if ((await PreguntasConIdeasHistoricasAsync(candidato.Campania, usuarioId, cancellationToken)).Count > 0)
            {
                elegibles.Add(new CampaniaElegible(candidato, TrabajoPendiente: false));
            }
        }

        return elegibles
            .OrderBy(c => c.Candidato.Campania.Nombre, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.Candidato.Campania.Id, StringComparer.Ordinal)
            .ToArray();
    }

    private async Task<IReadOnlyList<Pregunta>> PreguntasConIdeasHistoricasAsync(
        Campania campania,
        string usuarioId,
        CancellationToken cancellationToken)
    {
        if (_respuestas is null)
        {
            return Array.Empty<Pregunta>();
        }

        var resultado = new List<Pregunta>();
        foreach (var pregunta in PreguntasActivas(campania))
        {
            if ((await _respuestas.ListarIdeasHistoricasAsync(
                    campania.Id, usuarioId, pregunta.Id, cancellationToken)).Count > 0)
            {
                resultado.Add(pregunta);
            }
        }

        return resultado;
    }

    private static bool EsCampaniaBilingueIncompleta(Campania campania)
        => campania.IdiomasHabilitados.Any(idioma => !string.Equals(idioma, "es", StringComparison.OrdinalIgnoreCase))
            && ValidadorLocalizacionesCampania.Validar(campania).Count > 0;

    private async Task<IReadOnlyList<OpcionIdeaOfrecida>> OpcionesIdeasHistoricasAsync(
        string campaniaId,
        string usuarioId,
        string preguntaId,
        CancellationToken cancellationToken)
    {
        if (_respuestas is null)
        {
            return Array.Empty<OpcionIdeaOfrecida>();
        }

        var ideas = (await _respuestas.ListarIdeasHistoricasAsync(
                campaniaId, usuarioId, preguntaId, cancellationToken))
            .Where(idea => idea.CampaniaId == campaniaId
                && idea.UsuarioId == usuarioId
                && idea.PreguntaId == preguntaId)
            .OrderByDescending(idea => idea.ActualizadaEn)
            .ThenBy(idea => idea.Id, StringComparer.Ordinal)
            .ToArray();
        var opciones = new List<OpcionIdeaOfrecida>(ideas.Length);
        for (var indice = 0; indice < ideas.Length; indice++)
        {
            var idea = ideas[indice];
            var versionId = idea.VersionConfirmadaRef ?? idea.VersionPropuestaRef;
            var version = string.IsNullOrWhiteSpace(versionId)
                ? null
                : await _respuestas.ObtenerVersionIdeaAsync(campaniaId, versionId, cancellationToken);
            opciones.Add(new OpcionIdeaOfrecida(
                idea.Id,
                idea.ConversacionId,
                Acotar(version?.Texto ?? "Idea sin resumen disponible", MaxCaracteresParafrasisSeleccion),
                EstadoNeutral(idea),
                indice + 1));
        }

        return opciones;
    }

    /// <summary>
    /// Trabajo pendiente = alguna pregunta activa sin conversacion o con su conversacion mas reciente
    /// aun abierta (mismo criterio que el hilo de trabajo del orquestador).
    /// </summary>
    private async Task<bool> TieneTrabajoPendienteAsync(
        Campania campania,
        string usuarioId,
        CancellationToken cancellationToken)
        => (await PreguntasPendientesAsync(campania, usuarioId, cancellationToken)).Count > 0;

    /// <summary>
    /// §5.4: preguntas elegibles de una campania — las pendientes del recorrido; si no queda ninguna y
    /// la campania es continua, todas las activas vuelven a estar disponibles (ciclo nuevo §5.7).
    /// </summary>
    private async Task<IReadOnlyList<Pregunta>> PreguntasElegiblesAsync(
        Campania campania,
        string usuarioId,
        CancellationToken cancellationToken)
    {
        var pendientes = await PreguntasPendientesAsync(campania, usuarioId, cancellationToken);
        if (pendientes.Count > 0)
        {
            return pendientes;
        }

        return campania.ConfigConversacional.ParticipacionContinua
            ? PreguntasActivas(campania)
            : Array.Empty<Pregunta>();
    }

    private async Task<IReadOnlyList<Pregunta>> PreguntasPendientesAsync(
        Campania campania,
        string usuarioId,
        CancellationToken cancellationToken)
    {
        var activas = PreguntasActivas(campania);
        if (activas.Count == 0)
        {
            return Array.Empty<Pregunta>();
        }

        var conversaciones = await _conversaciones.ListarConversacionesAsync(campania.Id, cancellationToken);
        var porPregunta = conversaciones
            .Where(c => c.UsuarioId == usuarioId)
            .GroupBy(c => c.PreguntaId, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(c => c.FechaInicio).First(),
                StringComparer.Ordinal);

        return activas
            .Where(pregunta =>
                !porPregunta.TryGetValue(pregunta.Id, out var conversacion)
                || conversacion.Estado != EstadoConversacion.Cerrada)
            .ToArray();
    }

    private static IReadOnlyList<Pregunta> PreguntasActivas(Campania campania)
        => campania.Preguntas
            .Where(p => p.Estado == EstadoRegistro.Activo)
            .OrderBy(p => p.Orden)
            .ThenBy(p => p.Id, StringComparer.Ordinal)
            .ToArray();

    /// <summary>Numero de la lista vigente, o texto completo normalizado y no ambiguo (§5.3/§5.4).</summary>
    private static T? InterpretarSeleccion<T>(
        string texto,
        IReadOnlyList<T> opciones,
        Func<T, string> textoOpcion,
        Func<T, int> orden)
        where T : class
    {
        var normalizado = Normalizar(texto);
        if (normalizado.Length == 0 || opciones.Count == 0)
        {
            return null;
        }

        if (int.TryParse(normalizado, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numero))
        {
            return opciones.FirstOrDefault(o => orden(o) == numero);
        }

        var porTexto = opciones.Where(o => Normalizar(textoOpcion(o)) == normalizado).ToArray();
        return porTexto.Length == 1 ? porTexto[0] : null;
    }

    /// <summary>Minusculas, espacios colapsados y sin diacriticos: "Innovación  Comercial" == "innovacion comercial".</summary>
    private static string Normalizar(string texto)
    {
        var plano = new StringBuilder(texto.Length);
        foreach (var c in texto.Normalize(NormalizationForm.FormD))
        {
            var categoria = CharUnicodeInfo.GetUnicodeCategory(c);
            if (categoria == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            plano.Append(char.ToLowerInvariant(c));
        }

        return string.Join(' ', plano.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static IReadOnlyList<OpcionCampaniaOfrecida> Opciones(IReadOnlyList<CampaniaElegible> elegibles)
        => elegibles
            .Select((elegible, indice) => new OpcionCampaniaOfrecida(
                elegible.Candidato.Campania.Id, elegible.Candidato.Campania.Nombre, indice + 1))
            .ToArray();

    private static IReadOnlyList<OpcionPreguntaOfrecida> OpcionesPregunta(IReadOnlyList<Pregunta> preguntas)
        => preguntas
            .Select((pregunta, indice) => new OpcionPreguntaOfrecida(pregunta.Id, pregunta.Texto, indice + 1))
            .ToArray();

    /// <summary>
    /// El aporte original se entrega con su texto y wamid raiz (idempotencia de la respuesta), pero con
    /// el timestamp de la seleccion: la ventana de servicio de 24 h corre desde el ultimo mensaje real
    /// del participante.
    /// </summary>
    private static MensajeEntrante MensajeOriginal(
        EnrutamientoAporte enrutamiento,
        MensajeEntrante seleccion,
        DateTimeOffset ahora)
        => new(
            seleccion.NumeroE164,
            enrutamiento.TextoOriginal,
            enrutamiento.WhatsappMessageId,
            ahora,
            enrutamiento.PhoneNumberIdDestino ?? seleccion.PhoneNumberIdDestino);

    private async Task EnviarMenuCampaniasAsync(
        Usuario usuario,
        string idioma,
        IReadOnlyList<OpcionCampaniaOfrecida> opciones,
        string? emisor,
        bool conAyuda,
        CancellationToken cancellationToken)
        => await EnviarMenuAsync(
            usuario,
            await TextoCatalogoAsync(
                idioma,
                "encabezadoSeleccionCampania",
                Texto(_mensajes.EncabezadoSeleccionCampania, OpcionesMensajesConversacion.EncabezadoSeleccionCampaniaDefault),
                cancellationToken),
            opciones.OrderBy(o => o.Orden).Select(o => $"{o.Orden}. {o.NombreSnapshot}"),
            await TextoCatalogoAsync(
                idioma,
                "instruccionSeleccionCampania",
                Texto(_mensajes.InstruccionSeleccionCampania, OpcionesMensajesConversacion.InstruccionSeleccionCampaniaDefault),
                cancellationToken),
            emisor,
            conAyuda,
            idioma,
            cancellationToken);

    private async Task EnviarMenuPreguntasAsync(
        Usuario usuario,
        string idioma,
        IReadOnlyList<OpcionPreguntaOfrecida> opciones,
        string? emisor,
        bool conAyuda,
        CancellationToken cancellationToken)
        => await EnviarMenuAsync(
            usuario,
            await TextoCatalogoAsync(
                idioma,
                "encabezadoSeleccionPregunta",
                Texto(_mensajes.EncabezadoSeleccionPregunta, OpcionesMensajesConversacion.EncabezadoSeleccionPreguntaDefault),
                cancellationToken),
            opciones.OrderBy(o => o.Orden).Select(o => $"{o.Orden}. {o.TextoSnapshot}"),
            await TextoCatalogoAsync(
                idioma,
                "instruccionSeleccionPregunta",
                Texto(_mensajes.InstruccionSeleccionPregunta, OpcionesMensajesConversacion.InstruccionSeleccionPreguntaDefault),
                cancellationToken),
            emisor,
            conAyuda,
            idioma,
            cancellationToken);

    private async Task EnviarMenuIdeasAsync(
        Usuario usuario,
        string idioma,
        IReadOnlyList<OpcionIdeaOfrecida> opciones,
        string? emisor,
        bool conAyuda,
        CancellationToken cancellationToken)
        => await EnviarMenuAsync(
            usuario,
            await TextoCatalogoAsync(
                idioma,
                "preguntaSeleccionIdea",
                Texto(_mensajes.PreguntaSeleccionIdea, OpcionesMensajesConversacion.PreguntaSeleccionIdeaDefault),
                cancellationToken),
            opciones.OrderBy(o => o.Orden).Select(
                o => $"{o.Orden}. {o.ResumenSnapshot} ({o.EstadoSnapshot})"),
            await TextoCatalogoAsync(
                idioma,
                "instruccionSeleccionIdea",
                Texto(_mensajes.InstruccionSeleccionIdea, OpcionesMensajesConversacion.InstruccionSeleccionIdeaDefault),
                cancellationToken),
            emisor,
            conAyuda,
            idioma,
            cancellationToken);

    private async Task EnviarSinIdeasHistoricasAsync(
        Usuario usuario,
        string idioma,
        string? emisor,
        CancellationToken cancellationToken)
        => await _gateway.EnviarTextoAsync(
            usuario.WhatsappNormalizado.Valor,
            await TextoCatalogoAsync(
                idioma,
                "sinIdeasHistoricas",
                Texto(_mensajes.SinIdeasHistoricas, OpcionesMensajesConversacion.SinIdeasHistoricasDefault),
                cancellationToken),
            TipoEnvioMensaje.Repregunta,
            cancellationToken,
            emisor);

    private async Task EnviarMenuAsync(
        Usuario usuario,
        string encabezado,
        IEnumerable<string> filas,
        string instruccion,
        string? emisor,
        bool conAyuda,
        string idioma,
        CancellationToken cancellationToken)
    {
        var texto = new StringBuilder();
        if (conAyuda)
        {
            texto.AppendLine(await TextoCatalogoAsync(
                idioma,
                "ayudaSeleccionCampaniaInvalida",
                Texto(_mensajes.AyudaSeleccionCampaniaInvalida, OpcionesMensajesConversacion.AyudaSeleccionCampaniaInvalidaDefault),
                cancellationToken));
        }

        texto.AppendLine(encabezado);
        foreach (var fila in filas)
        {
            texto.AppendLine(fila);
        }

        texto.AppendLine();
        texto.Append(instruccion);

        // El participante acaba de escribir: la ventana de 24 h esta abierta y el texto libre es valido.
        await _gateway.EnviarTextoAsync(
            usuario.WhatsappNormalizado.Valor,
            texto.ToString(),
            TipoEnvioMensaje.Repregunta,
            cancellationToken,
            emisor);
    }

    private async Task<bool> CoincideCambioCampaniaAsync(
        string idioma,
        string texto,
        CancellationToken cancellationToken)
        => (await FrasesCatalogoAsync(
                idioma,
                "cambiarCampania",
                _cambioCampania,
                cancellationToken))
            .Coincide(texto);

    private async Task<bool> CoincideConsultaIdeaAsync(
        string idioma,
        string texto,
        CancellationToken cancellationToken)
    {
        if (_resolutorTextos is null)
        {
            return _consultaIdea.Coincide(texto);
        }

        var textos = await _resolutorTextos.ResolverParaIdiomaAsync(idioma, cancellationToken);
        return new DetectorConsultaIdea(textos.Frases["consultarIdea"], _maxCaracteresConsultaIdea).Coincide(texto);
    }

    private async Task<bool> EsAcuseConsultaIdeaAsync(
        string idioma,
        string texto,
        CancellationToken cancellationToken)
    {
        if (_resolutorTextos is null)
        {
            return DetectorConsultaIdea.EsAcuse(texto, DetectorConsultaIdea.FrasesAcusePorDefecto);
        }

        var textos = await _resolutorTextos.ResolverParaIdiomaAsync(idioma, cancellationToken);
        return DetectorConsultaIdea.EsAcuse(texto, textos.Frases["acuseConsultaIdea"]);
    }

    private async Task<bool> CoincideConformidadConsultaIdeaAsync(
        string idioma,
        string texto,
        CancellationToken cancellationToken)
    {
        if (_resolutorTextos is null)
        {
            return new DetectorIntencionContinuar(
                    DetectorIntencionContinuar.FrasesPorDefecto,
                    _maxCaracteresIntencionContinuar)
                .Coincide(texto);
        }

        var textos = await _resolutorTextos.ResolverParaIdiomaAsync(idioma, cancellationToken);
        return new DetectorIntencionContinuar(
                textos.Frases["confirmar"],
                _maxCaracteresIntencionContinuar)
            .Coincide(texto);
    }

    private async Task<bool> CoincideRetomarIdeaAsync(
        string idioma,
        string texto,
        CancellationToken cancellationToken)
        => (await FrasesCatalogoAsync(
                idioma,
                "revisitarIdea",
                _retomarIdea,
                cancellationToken))
            .Coincide(texto);

    private async Task<bool> CoincideEntradaProactivaAsync(
        string idioma,
        string texto,
        CancellationToken cancellationToken)
    {
        if (_resolutorTextos is null)
        {
            return _entradaProactiva.Coincide(texto);
        }

        var textos = await _resolutorTextos.ResolverParaIdiomaAsync(idioma, cancellationToken);
        return new DetectorEntradaProactiva(
                textos.Frases["despertarProactivo"],
                _maxCaracteresDespertarProactivo)
            .Coincide(texto);
    }

    private async Task<DetectorIntencionContinuar> FrasesCatalogoAsync(
        string idioma,
        string clave,
        DetectorIntencionContinuar respaldo,
        CancellationToken cancellationToken)
    {
        if (_resolutorTextos is null)
        {
            return respaldo;
        }

        var textos = await _resolutorTextos.ResolverParaIdiomaAsync(idioma, cancellationToken);
        return new DetectorIntencionContinuar(textos.Frases[clave], _maxCaracteresIntencionContinuar);
    }

    private async Task<string> TextoCatalogoAsync(
        string idioma,
        string clave,
        string respaldo,
        CancellationToken cancellationToken)
    {
        if (_resolutorTextos is null)
        {
            return respaldo;
        }

        var textos = await _resolutorTextos.ResolverParaIdiomaAsync(idioma, cancellationToken);
        return textos.Mensajes[clave];
    }

    private async Task<EnrutamientoAporte?> ObtenerSeleccionPendienteAsync(
        string usuarioId,
        CancellationToken cancellationToken)
    {
        var enrutamientos = await _enrutamientos.ListarPorUsuarioAsync(usuarioId, cancellationToken);
        return enrutamientos
            .Where(e => e.Estado is EstadoEnrutamientoAporte.SeleccionCampania
                or EstadoEnrutamientoAporte.SeleccionPregunta
                or EstadoEnrutamientoAporte.SeleccionIdea
                || (e.Estado == EstadoEnrutamientoAporte.Listo && e.EsRetomarIdea))
            .OrderByDescending(e => e.ActualizadoEn)
            .FirstOrDefault();
    }

    private async Task<string?> ResolverConversacionRecienteAsync(
        string? campaniaId,
        string usuarioId,
        string? preguntaId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(campaniaId))
        {
            return null;
        }

        var conversaciones = await _conversaciones.ListarConversacionesAsync(campaniaId, cancellationToken);
        return conversaciones
            .Where(c => c.UsuarioId == usuarioId && (preguntaId is null || c.PreguntaId == preguntaId))
            .OrderByDescending(c => c.FechaInicio)
            .FirstOrDefault()?.Id;
    }

    private async Task<DominioConversacion?> ResolverConversacionAbiertaAsync(
        string campaniaId,
        string usuarioId,
        string preguntaId,
        CancellationToken cancellationToken)
    {
        var conversaciones = await _conversaciones.ListarConversacionesAsync(campaniaId, cancellationToken);
        return conversaciones
            .Where(c => c.UsuarioId == usuarioId
                && c.PreguntaId == preguntaId
                && c.Estado != EstadoConversacion.Cerrada)
            .OrderByDescending(c => c.FechaInicio)
            .FirstOrDefault();
    }

    private Task RegistrarAsync(
        Usuario usuario,
        string accion,
        string detalle,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
        => RegistrarUsuarioAsync(usuario.Id, usuario.WhatsappNormalizado.Valor, accion, detalle, ahora, cancellationToken);

    /// <summary>10 §6.2: solo accion, conteos e ids internos; nunca texto del participante ni nombres.</summary>
    private Task RegistrarUsuarioAsync(
        string usuarioId,
        string? numero,
        string accion,
        string detalle,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
        => _logSeguridad.RegistrarAsync(
            LogSeguridad.Crear(
                "log_" + Guid.NewGuid().ToString("N"),
                TipoEventoSeguridad.EnrutamientoParticipacion,
                usuarioId,
                numero,
                accion,
                detalle,
                _correlacion.CorrelationIdActual,
                ahora),
            cancellationToken);

    private Task RegistrarRetomarAsync(
        Usuario usuario,
        string accion,
        EnrutamientoAporte enrutamiento,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
        => _logSeguridad.RegistrarAsync(
            LogSeguridad.Crear(
                "log_" + Guid.NewGuid().ToString("N"),
                TipoEventoSeguridad.RetomarIdea,
                usuario.Id,
                usuario.WhatsappNormalizado.Valor,
                accion,
                $"enrutamiento={enrutamiento.Id};opciones={enrutamiento.IdeasOfrecidas.Count};campania={enrutamiento.CampaniaSeleccionadaId};pregunta={enrutamiento.PreguntaSeleccionadaId};idea={enrutamiento.IdeaSeleccionadaId}",
                _correlacion.CorrelationIdActual,
                ahora,
                campaniaId: enrutamiento.CampaniaSeleccionadaId),
            cancellationToken);

    private static string Detalle(EnrutamientoAporte enrutamiento)
        => $"enrutamiento={enrutamiento.Id};opciones={enrutamiento.CampaniasOfrecidas.Count};preguntas={enrutamiento.PreguntasOfrecidas.Count};ideas={enrutamiento.IdeasOfrecidas.Count}";

    private static string EstadoNeutral(ElTejido.Domain.Respuestas.IdeaConsolidada idea)
        => idea.EstadoResultado switch
        {
            ElTejido.Domain.Respuestas.EstadoResultadoIdeaConsolidada.Madura => "madura",
            ElTejido.Domain.Respuestas.EstadoResultadoIdeaConsolidada.Rechazada => "descartada",
            ElTejido.Domain.Respuestas.EstadoResultadoIdeaConsolidada.Pendiente => "pendiente",
            _ => "en proceso",
        };

    private static string Acotar(string texto, int maximo)
        => texto.Length <= maximo ? texto : texto[..maximo].TrimEnd() + "…";

    private static string Texto(string configurado, string porDefecto)
        => string.IsNullOrWhiteSpace(configurado) ? porDefecto : configurado;

    /// <summary>Candidato con su marca de trabajo pendiente (decide flujo actual vs. ciclo nuevo).</summary>
    private sealed record CampaniaElegible(CandidatoCampania Candidato, bool TrabajoPendiente);

    /// <summary>Afinidad vigente: enrutamiento enIdea, su campania autorizada y la conversacion abierta (si existe).</summary>
    private sealed record AfinidadVigente(
        EnrutamientoAporte Enrutamiento,
        CandidatoCampania Candidato,
        DominioConversacion? Conversacion);

    private sealed record ContextoSemantico(
        CandidatoCampania Candidato,
        EstadoMaquinaConversacion Estado,
        bool HayIdeaDisponible,
        bool HayAfinidadConsultaIdea,
        bool IdeaAbierta,
        DateTimeOffset? IdeaActualizadaEn,
        int IdeaIndice,
        string? IdeaId);
}

/// <summary>P-30: seleccion historica ya resuelta; solo contiene ids internos auditables.</summary>
public sealed record ContextoRetomarIdea(
    string PreguntaId,
    string IdeaId,
    string ConversacionId,
    string EnrutamientoAporteId,
    string WhatsappMessageIdOriginal);

/// <summary>P-33: alcance revalidable de una consulta de la idea propia; no contiene texto.</summary>
public sealed record ContextoConsultaIdea(
    string? PreguntaId,
    string? IdeaId,
    string? ConversacionId,
    string? EnrutamientoAporteId,
    bool IdeaCerrada);
