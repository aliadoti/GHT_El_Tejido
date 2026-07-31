using System.Globalization;
using System.Text;
using ElTejido.Application.Common;
using ElTejido.Application.Identidad;
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
        ContextoAporteEnrutado? Contexto = null) : ResultadoEnrutamiento;

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

    /// <summary>Ninguna campania elegible: rechazo neutral vigente (silencio, comportamiento actual).</summary>
    public sealed record SinElegibles() : ResultadoEnrutamiento;
}

public sealed class ServicioEnrutamientoParticipacion : IServicioEnrutamientoParticipacion
{
    private static readonly TimeSpan VigenciaAfinidad = TimeSpan.FromHours(24);

    private readonly IRepositorioEnrutamientosAporte _enrutamientos;
    private readonly IRepositorioConversaciones _conversaciones;
    private readonly IWhatsAppGateway _gateway;
    private readonly IRepositorioLogSeguridad _logSeguridad;
    private readonly IProveedorCorrelacion _correlacion;
    private readonly OpcionesMensajesConversacion _mensajes;
    private readonly DetectorIntencionContinuar _cambioCampania;
    private readonly TimeProvider _tiempo;

    public ServicioEnrutamientoParticipacion(
        IRepositorioEnrutamientosAporte enrutamientos,
        IRepositorioConversaciones conversaciones,
        IWhatsAppGateway gateway,
        IRepositorioLogSeguridad logSeguridad,
        IProveedorCorrelacion correlacion,
        OpcionesConversacion opciones,
        TimeProvider tiempo)
    {
        _enrutamientos = enrutamientos;
        _conversaciones = conversaciones;
        _gateway = gateway;
        _logSeguridad = logSeguridad;
        _correlacion = correlacion;
        _mensajes = opciones.Mensajes;
        _cambioCampania = new DetectorIntencionContinuar(
            opciones.FrasesCambiarCampania is { Count: > 0 }
                ? opciones.FrasesCambiarCampania
                : DetectorIntencionContinuar.FrasesCambiarCampaniaPorDefecto,
            opciones.MaxCaracteresIntencionContinuar);
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

        if (pendiente is not null)
        {
            return pendiente.Estado == EstadoEnrutamientoAporte.SeleccionCampania
                ? await ResolverSeleccionCampaniaAsync(usuario, candidatos, pendiente, mensaje, ahora, cancellationToken)
                : await ResolverSeleccionPreguntaAsync(usuario, candidatos, pendiente, mensaje, ahora, cancellationToken);
        }

        // §5.6: una afinidad vigente enruta las respuestas de coaching sin volver a listar campanias,
        // salvo que el participante pida explicitamente cambiar de campania (§5.1 paso 3).
        var afinidad = await ObtenerAfinidadVigenteAsync(usuario.Id, candidatos, ahora, cancellationToken);
        if (afinidad is not null)
        {
            if (_cambioCampania.Coincide(mensaje.Texto))
            {
                return await SuspenderAfinidadYReofrecerAsync(usuario, candidatos, afinidad, mensaje, ahora, cancellationToken);
            }

            if (afinidad.Conversacion is not null)
            {
                return new ResultadoEnrutamiento.ContinuarConversacion(
                    afinidad.Candidato,
                    mensaje,
                    null,
                    new ContextoAporteEnrutado(afinidad.Conversacion.PreguntaId, null));
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

        if (elegibles.Count == 1)
        {
            var unico = elegibles[0];
            if (unico.TrabajoPendiente)
            {
                // Comportamiento actual intacto: una sola opcion con recorrido en curso se entrega al
                // orquestador secuencial de siempre (criterio de compatibilidad 1 de P-26).
                return new ResultadoEnrutamiento.ContinuarConversacion(unico.Candidato, mensaje, null);
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
            await EnviarMenuCampaniasAsync(usuario, invalido.CampaniasOfrecidas, mensaje.PhoneNumberIdDestino, conAyuda: true, cancellationToken);
            return new ResultadoEnrutamiento.SeleccionPendiente(pendiente.Id);
        }

        // Revalidacion (§5.3/§10): el estado pudo cambiar desde que se ofrecio la lista.
        var elegibles = await CalcularElegiblesAsync(candidatos, usuario.Id, cancellationToken);
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
        var preguntas = await PreguntasElegiblesAsync(candidato.Campania, usuario.Id, cancellationToken);
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
        await EnviarMenuPreguntasAsync(usuario, ofrecido.PreguntasOfrecidas, mensaje.PhoneNumberIdDestino, conAyuda: false, cancellationToken);
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
        if (_cambioCampania.Coincide(mensaje.Texto))
        {
            var elegiblesCambio = await CalcularElegiblesAsync(candidatos, usuario.Id, cancellationToken);
            await RegistrarAsync(usuario, "cambioCampania", Detalle(pendiente), ahora, cancellationToken);
            return await OfrecerCampaniasDeNuevoAsync(usuario, elegiblesCambio, pendiente, mensaje, ahora, cancellationToken);
        }

        var candidato = candidatos.FirstOrDefault(c => c.Campania.Id == pendiente.CampaniaSeleccionadaId);
        if (candidato is null)
        {
            // La campania elegida dejo de estar autorizada entre la oferta y la seleccion (§11).
            var elegiblesActuales = await CalcularElegiblesAsync(candidatos, usuario.Id, cancellationToken);
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
            await EnviarMenuPreguntasAsync(usuario, invalido.PreguntasOfrecidas, mensaje.PhoneNumberIdDestino, conAyuda: true, cancellationToken);
            return new ResultadoEnrutamiento.SeleccionPendiente(pendiente.Id);
        }

        // Revalidacion: la pregunta pudo desactivarse o completarse desde que se ofrecio (§11).
        var vigentes = await PreguntasElegiblesAsync(candidato.Campania, usuario.Id, cancellationToken);
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
            await EnviarMenuPreguntasAsync(usuario, reofrecido.PreguntasOfrecidas, mensaje.PhoneNumberIdDestino, conAyuda: true, cancellationToken);
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

        return new ResultadoEnrutamiento.ContinuarConversacion(
            candidato,
            MensajeOriginal(enrutamiento, mensajeSeleccion, ahora),
            enrutamiento.Id,
            new ContextoAporteEnrutado(pregunta.Id, enrutamiento.Id));
    }

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
        await EnviarMenuCampaniasAsync(usuario, reofrecido.CampaniasOfrecidas, mensaje.PhoneNumberIdDestino, conAyuda: true, cancellationToken);
        return new ResultadoEnrutamiento.SeleccionPendiente(reofrecido.Id);
    }

    private async Task<ResultadoEnrutamiento> ConservarYOfrecerAsync(
        Usuario usuario,
        IReadOnlyList<CampaniaElegible> elegibles,
        MensajeEntrante mensaje,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
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
            campaniasOfrecidas: Opciones(elegibles));

        // §11: primero se conserva el aporte; si Cosmos falla no se muestra un menu que pueda perderlo.
        await _enrutamientos.GuardarAsync(enrutamiento, cancellationToken);
        await RegistrarAsync(usuario, "ofrecido", Detalle(enrutamiento), ahora, cancellationToken);
        await EnviarMenuCampaniasAsync(usuario, enrutamiento.CampaniasOfrecidas, mensaje.PhoneNumberIdDestino, conAyuda: false, cancellationToken);
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
            preguntasOfrecidas: OpcionesPregunta(preguntas));
        await _enrutamientos.GuardarAsync(enrutamiento, cancellationToken);
        await RegistrarAsync(usuario, "ofrecido", Detalle(enrutamiento), ahora, cancellationToken);
        await EnviarMenuPreguntasAsync(usuario, enrutamiento.PreguntasOfrecidas, mensaje.PhoneNumberIdDestino, conAyuda: false, cancellationToken);
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

    private Task EnviarMenuCampaniasAsync(
        Usuario usuario,
        IReadOnlyList<OpcionCampaniaOfrecida> opciones,
        string? emisor,
        bool conAyuda,
        CancellationToken cancellationToken)
        => EnviarMenuAsync(
            usuario,
            Texto(_mensajes.EncabezadoSeleccionCampania, OpcionesMensajesConversacion.EncabezadoSeleccionCampaniaDefault),
            opciones.OrderBy(o => o.Orden).Select(o => $"{o.Orden}. {o.NombreSnapshot}"),
            Texto(_mensajes.InstruccionSeleccionCampania, OpcionesMensajesConversacion.InstruccionSeleccionCampaniaDefault),
            emisor,
            conAyuda,
            cancellationToken);

    private Task EnviarMenuPreguntasAsync(
        Usuario usuario,
        IReadOnlyList<OpcionPreguntaOfrecida> opciones,
        string? emisor,
        bool conAyuda,
        CancellationToken cancellationToken)
        => EnviarMenuAsync(
            usuario,
            Texto(_mensajes.EncabezadoSeleccionPregunta, OpcionesMensajesConversacion.EncabezadoSeleccionPreguntaDefault),
            opciones.OrderBy(o => o.Orden).Select(o => $"{o.Orden}. {o.TextoSnapshot}"),
            Texto(_mensajes.InstruccionSeleccionPregunta, OpcionesMensajesConversacion.InstruccionSeleccionPreguntaDefault),
            emisor,
            conAyuda,
            cancellationToken);

    private async Task EnviarMenuAsync(
        Usuario usuario,
        string encabezado,
        IEnumerable<string> filas,
        string instruccion,
        string? emisor,
        bool conAyuda,
        CancellationToken cancellationToken)
    {
        var texto = new StringBuilder();
        if (conAyuda)
        {
            texto.AppendLine(Texto(_mensajes.AyudaSeleccionCampaniaInvalida, OpcionesMensajesConversacion.AyudaSeleccionCampaniaInvalidaDefault));
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

    private async Task<EnrutamientoAporte?> ObtenerSeleccionPendienteAsync(
        string usuarioId,
        CancellationToken cancellationToken)
    {
        var enrutamientos = await _enrutamientos.ListarPorUsuarioAsync(usuarioId, cancellationToken);
        return enrutamientos
            .Where(e => e.Estado is EstadoEnrutamientoAporte.SeleccionCampania or EstadoEnrutamientoAporte.SeleccionPregunta)
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

    private static string Detalle(EnrutamientoAporte enrutamiento)
        => $"enrutamiento={enrutamiento.Id};opciones={enrutamiento.CampaniasOfrecidas.Count};preguntas={enrutamiento.PreguntasOfrecidas.Count}";

    private static string Texto(string configurado, string porDefecto)
        => string.IsNullOrWhiteSpace(configurado) ? porDefecto : configurado;

    /// <summary>Candidato con su marca de trabajo pendiente (decide flujo actual vs. ciclo nuevo).</summary>
    private sealed record CampaniaElegible(CandidatoCampania Candidato, bool TrabajoPendiente);

    /// <summary>Afinidad vigente: enrutamiento enIdea, su campania autorizada y la conversacion abierta (si existe).</summary>
    private sealed record AfinidadVigente(
        EnrutamientoAporte Enrutamiento,
        CandidatoCampania Candidato,
        DominioConversacion? Conversacion);
}
