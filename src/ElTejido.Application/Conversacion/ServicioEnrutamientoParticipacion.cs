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

namespace ElTejido.Application.Conversacion;

/// <summary>
/// P-26 corte 2 (05 §4.3 paso 0, §4.4.3; Reglas §2.10): resolucion determinista de campania previa al
/// orquestador. Con 0 campanias elegibles responde el rechazo neutral vigente (silencio, como el
/// orquestador cuando todo esta cerrado); con 1 continua el flujo actual; con varias conserva el
/// aporte en <see cref="EnrutamientoAporte"/> y ofrece una lista numerada. La seleccion acepta numero
/// o nombre exacto no ambiguo, se revalida al aceptar y vence logicamente a las 24 h. El LLM nunca
/// participa en esta decision.
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
    /// conservada y debe confirmarse como procesado al terminar.
    /// </summary>
    public sealed record ContinuarConversacion(
        CandidatoCampania Candidato,
        MensajeEntrante Mensaje,
        string? EnrutamientoAporteId) : ResultadoEnrutamiento;

    /// <summary>El aporte quedo conservado y el menu de campanias fue enviado (u ofrecido de nuevo).</summary>
    public sealed record SeleccionPendiente(string EnrutamientoAporteId) : ResultadoEnrutamiento;

    /// <summary>Ninguna campania elegible: rechazo neutral vigente (silencio, comportamiento actual).</summary>
    public sealed record SinElegibles() : ResultadoEnrutamiento;
}

public sealed class ServicioEnrutamientoParticipacion : IServicioEnrutamientoParticipacion
{
    private readonly IRepositorioEnrutamientosAporte _enrutamientos;
    private readonly IRepositorioConversaciones _conversaciones;
    private readonly IWhatsAppGateway _gateway;
    private readonly IRepositorioLogSeguridad _logSeguridad;
    private readonly IProveedorCorrelacion _correlacion;
    private readonly OpcionesMensajesConversacion _mensajes;
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
            return await ResolverSeleccionAsync(usuario, candidatos, pendiente, mensaje, ahora, cancellationToken);
        }

        var elegibles = await CalcularElegiblesAsync(candidatos, usuario.Id, cancellationToken);
        if (elegibles.Count == 0)
        {
            return new ResultadoEnrutamiento.SinElegibles();
        }

        if (elegibles.Count == 1)
        {
            // Comportamiento actual: una sola opcion se selecciona sin menu (§5.1 paso 6).
            return new ResultadoEnrutamiento.ContinuarConversacion(elegibles[0], mensaje, null);
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
            enrutamiento.CampaniaSeleccionadaId, usuarioId, cancellationToken);
        await _enrutamientos.GuardarAsync(enrutamiento.MarcarEnIdea(conversacionId, ahora), cancellationToken);
        await RegistrarUsuarioAsync(
            usuarioId, null, "procesado", Detalle(enrutamiento), ahora, cancellationToken);
    }

    private async Task<ResultadoEnrutamiento> ResolverSeleccionAsync(
        Usuario usuario,
        IReadOnlyList<CandidatoCampania> candidatos,
        EnrutamientoAporte pendiente,
        MensajeEntrante mensaje,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        var opcion = InterpretarSeleccion(mensaje.Texto, pendiente.CampaniasOfrecidas);
        if (opcion is null)
        {
            // §5.5: una opcion invalida conserva el aporte, queda auditada sin texto libre y vuelve a
            // pedir la seleccion.
            var invalido = pendiente.RegistrarIntento(
                new IntentoSeleccion(mensaje.WhatsappMessageId, TipoIntentoSeleccion.Campania, ResultadoIntentoSeleccion.Invalido, ahora),
                ahora);
            await _enrutamientos.GuardarAsync(invalido, cancellationToken);
            await RegistrarAsync(usuario, "invalido", Detalle(pendiente), ahora, cancellationToken);
            await EnviarMenuAsync(usuario, invalido.CampaniasOfrecidas, mensaje.PhoneNumberIdDestino, conAyuda: true, cancellationToken);
            return new ResultadoEnrutamiento.SeleccionPendiente(pendiente.Id);
        }

        // Revalidacion (§5.3/§10): el estado pudo cambiar desde que se ofrecio la lista.
        var elegibles = await CalcularElegiblesAsync(candidatos, usuario.Id, cancellationToken);
        var elegido = elegibles.FirstOrDefault(c => c.Campania.Id == opcion.CampaniaId);
        if (elegido is null)
        {
            return await RecalcularTrasRevalidacionAsync(usuario, elegibles, pendiente, mensaje, ahora, cancellationToken);
        }

        var seleccionado = pendiente
            .RegistrarIntento(
                new IntentoSeleccion(mensaje.WhatsappMessageId, TipoIntentoSeleccion.Campania, ResultadoIntentoSeleccion.Valido, ahora),
                ahora)
            .SeleccionarCampania(elegido.Campania.Id, ahora);
        await _enrutamientos.GuardarAsync(seleccionado, cancellationToken);
        await RegistrarAsync(usuario, "seleccionado", Detalle(seleccionado), ahora, cancellationToken);

        return new ResultadoEnrutamiento.ContinuarConversacion(
            elegido,
            MensajeOriginal(seleccionado, mensaje, ahora),
            seleccionado.Id);
    }

    private async Task<ResultadoEnrutamiento> RecalcularTrasRevalidacionAsync(
        Usuario usuario,
        IReadOnlyList<CandidatoCampania> elegibles,
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

        if (elegibles.Count == 0)
        {
            await _enrutamientos.GuardarAsync(invalidado.Cancelar(ahora), cancellationToken);
            return new ResultadoEnrutamiento.SinElegibles();
        }

        if (elegibles.Count == 1)
        {
            var unico = invalidado.SeleccionarCampania(elegibles[0].Campania.Id, ahora);
            await _enrutamientos.GuardarAsync(unico, cancellationToken);
            await RegistrarAsync(usuario, "seleccionado", Detalle(unico), ahora, cancellationToken);
            return new ResultadoEnrutamiento.ContinuarConversacion(
                elegibles[0],
                MensajeOriginal(unico, mensaje, ahora),
                unico.Id);
        }

        var reofrecido = invalidado.OfrecerCampanias(Opciones(elegibles), ahora);
        await _enrutamientos.GuardarAsync(reofrecido, cancellationToken);
        await RegistrarAsync(usuario, "ofrecido", Detalle(reofrecido), ahora, cancellationToken);
        await EnviarMenuAsync(usuario, reofrecido.CampaniasOfrecidas, mensaje.PhoneNumberIdDestino, conAyuda: true, cancellationToken);
        return new ResultadoEnrutamiento.SeleccionPendiente(reofrecido.Id);
    }

    private async Task<ResultadoEnrutamiento> ConservarYOfrecerAsync(
        Usuario usuario,
        IReadOnlyList<CandidatoCampania> elegibles,
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
        await EnviarMenuAsync(usuario, enrutamiento.CampaniasOfrecidas, mensaje.PhoneNumberIdDestino, conAyuda: false, cancellationToken);
        return new ResultadoEnrutamiento.SeleccionPendiente(enrutamiento.Id);
    }

    /// <summary>
    /// Campanias elegibles (§5.2): activa + asociacion/usuario activos + pregunta activa (todo eso ya
    /// garantizado por los candidatos) y ademas trabajo pendiente o participacion continua.
    /// </summary>
    private async Task<IReadOnlyList<CandidatoCampania>> CalcularElegiblesAsync(
        IReadOnlyList<CandidatoCampania> candidatos,
        string usuarioId,
        CancellationToken cancellationToken)
    {
        var elegibles = new List<CandidatoCampania>();
        foreach (var candidato in candidatos)
        {
            if (candidato.Campania.ConfigConversacional.ParticipacionContinua
                || await TieneTrabajoPendienteAsync(candidato.Campania, usuarioId, cancellationToken))
            {
                elegibles.Add(candidato);
            }
        }

        return elegibles
            .OrderBy(c => c.Campania.Nombre, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.Campania.Id, StringComparer.Ordinal)
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
    {
        var preguntas = campania.Preguntas
            .Where(p => p.Estado == EstadoRegistro.Activo)
            .ToArray();
        if (preguntas.Length == 0)
        {
            return false;
        }

        var conversaciones = await _conversaciones.ListarConversacionesAsync(campania.Id, cancellationToken);
        var porPregunta = conversaciones
            .Where(c => c.UsuarioId == usuarioId)
            .GroupBy(c => c.PreguntaId, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(c => c.FechaInicio).First(),
                StringComparer.Ordinal);

        return preguntas.Any(pregunta =>
            !porPregunta.TryGetValue(pregunta.Id, out var conversacion)
            || conversacion.Estado != EstadoConversacion.Cerrada);
    }

    /// <summary>Numero de la lista vigente, o nombre completo normalizado y no ambiguo (§5.3).</summary>
    private static OpcionCampaniaOfrecida? InterpretarSeleccion(
        string texto,
        IReadOnlyList<OpcionCampaniaOfrecida> opciones)
    {
        var normalizado = Normalizar(texto);
        if (normalizado.Length == 0 || opciones.Count == 0)
        {
            return null;
        }

        if (int.TryParse(normalizado, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numero))
        {
            return opciones.FirstOrDefault(o => o.Orden == numero);
        }

        var porNombre = opciones.Where(o => Normalizar(o.NombreSnapshot) == normalizado).ToArray();
        return porNombre.Length == 1 ? porNombre[0] : null;
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

    private static IReadOnlyList<OpcionCampaniaOfrecida> Opciones(IReadOnlyList<CandidatoCampania> elegibles)
        => elegibles
            .Select((candidato, indice) => new OpcionCampaniaOfrecida(
                candidato.Campania.Id, candidato.Campania.Nombre, indice + 1))
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

    private async Task EnviarMenuAsync(
        Usuario usuario,
        IReadOnlyList<OpcionCampaniaOfrecida> opciones,
        string? emisor,
        bool conAyuda,
        CancellationToken cancellationToken)
    {
        var texto = new StringBuilder();
        if (conAyuda)
        {
            texto.AppendLine(Texto(_mensajes.AyudaSeleccionCampaniaInvalida, OpcionesMensajesConversacion.AyudaSeleccionCampaniaInvalidaDefault));
        }

        texto.AppendLine(Texto(_mensajes.EncabezadoSeleccionCampania, OpcionesMensajesConversacion.EncabezadoSeleccionCampaniaDefault));
        foreach (var opcion in opciones.OrderBy(o => o.Orden))
        {
            texto.AppendLine($"{opcion.Orden}. {opcion.NombreSnapshot}");
        }

        texto.AppendLine();
        texto.Append(Texto(_mensajes.InstruccionSeleccionCampania, OpcionesMensajesConversacion.InstruccionSeleccionCampaniaDefault));

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
            .Where(e => e.Estado == EstadoEnrutamientoAporte.SeleccionCampania)
            .OrderByDescending(e => e.CreadoEn)
            .FirstOrDefault();
    }

    private async Task<string?> ResolverConversacionRecienteAsync(
        string? campaniaId,
        string usuarioId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(campaniaId))
        {
            return null;
        }

        var conversaciones = await _conversaciones.ListarConversacionesAsync(campaniaId, cancellationToken);
        return conversaciones
            .Where(c => c.UsuarioId == usuarioId)
            .OrderByDescending(c => c.FechaInicio)
            .FirstOrDefault()?.Id;
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
        => $"enrutamiento={enrutamiento.Id};opciones={enrutamiento.CampaniasOfrecidas.Count}";

    private static string Texto(string configurado, string porDefecto)
        => string.IsNullOrWhiteSpace(configurado) ? porDefecto : configurado;
}
