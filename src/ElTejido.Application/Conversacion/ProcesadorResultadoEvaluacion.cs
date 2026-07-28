using ElTejido.Application.Common;
using ElTejido.Application.Evaluacion;
using ElTejido.Application.Markdown;
using ElTejido.Application.Respuestas;
using ElTejido.Application.Seguridad;
using ElTejido.Domain.Campanas;
using ElTejido.Domain.Configuracion;
using ElTejido.Domain.Evaluacion;
using ElTejido.Domain.Respuestas;
using ElTejido.Domain.Seguridad;
using ElTejido.Domain.Usuarios;
using RespuestaUsuario = ElTejido.Domain.Respuestas.Respuesta;

namespace ElTejido.Application.Conversacion;

/// <summary>
/// P-15 (CAL-001) — Corte 3: efectos posteriores a una evaluación. Concentra la <b>persistencia</b> de la
/// evaluación y la respuesta, la compilación de <b>Markdown</b> y los <b>registros de seguridad</b> de
/// calibración (madurez sellada I-17, cierre por umbral I-01, reclasificación por rechazo I-17 §5.4),
/// conservando el <b>orden observable</b> de los efectos (persistir → registrar → compilar). Es un
/// colaborador interno del orquestador; la fachada <see cref="IOrquestadorConversacion"/> no cambia. El
/// <b>envío</b> de mensajes al participante permanece como primitiva única de la fachada
/// (<c>EnviarAsync</c>) para no fragmentar la ruta de persistir/enviar (05 §4). Reutiliza
/// <see cref="PoliticaLimitesConversacion"/> para el umbral y la clasificación deterministas.
/// </summary>
public sealed class ProcesadorResultadoEvaluacion
{
    private const string Canal = "whatsapp";

    private readonly IRepositorioRespuestas _respuestas;
    private readonly ICompiladorMarkdown _compilador;
    private readonly IRepositorioLogSeguridad _logSeguridad;
    private readonly IProveedorCorrelacion _correlacion;
    private readonly PoliticaLimitesConversacion _limites;

    public ProcesadorResultadoEvaluacion(
        IRepositorioRespuestas respuestas,
        ICompiladorMarkdown compilador,
        IRepositorioLogSeguridad logSeguridad,
        IProveedorCorrelacion correlacion,
        PoliticaLimitesConversacion limites)
    {
        _respuestas = respuestas;
        _compilador = compilador;
        _logSeguridad = logSeguridad;
        _correlacion = correlacion;
        _limites = limites;
    }

    /// <summary>
    /// Efectos posteriores a una evaluación de una sola respuesta (normal o una idea segmentada), en el
    /// orden observable de siempre: persiste la evaluación, sella la madurez (I-17, 03 §3.8) y registra su
    /// telemetría, persiste la respuesta con ese nivel y —solo si la evaluación es válida (no fallback,
    /// 08 §6)— compila el Markdown. Devuelve el nivel de madurez sellado para que la fachada decida la
    /// paráfrasis mostrable. Es determinista respecto al umbral (pregunta → campaña → global vía la
    /// política); recomputarlo por idea rinde el mismo valor que campaña/pregunta constantes.
    /// </summary>
    public async Task<NivelMadurez> PersistirRespuestaEvaluadaAsync(
        ResultadoEvaluacion resultado,
        Campania campania,
        Pregunta pregunta,
        Usuario usuario,
        string conversacionId,
        string respuestaId,
        string texto,
        bool esRepregunta,
        EscalaRubrica escala,
        DateTimeOffset ahora,
        CancellationToken cancellationToken,
        int? ideaIndice = null,
        string? respuestaPadreId = null,
        string? ideaRaizId = null,
        string? respuestaAnteriorId = null,
        int? revisionIndice = null)
    {
        await _respuestas.GuardarEvaluacionAsync(resultado.Evaluacion, cancellationToken);

        var esFallback = resultado is ResultadoEvaluacion.Fallback;
        var umbralBase = _limites.ResolverUmbralBase(campania, pregunta);
        var nivelMadurez = _limites.ClasificarMadurez(esFallback, resultado.Evaluacion.CalificacionTotal, escala, umbralBase);
        await RegistrarClasificacionMadurezAsync(
            usuario, nivelMadurez, resultado.Evaluacion.CalificacionTotal, escala, umbralBase,
            _limites.OrigenUmbral(campania, pregunta), ahora, cancellationToken);

        await GuardarRespuestaAsync(
            respuestaId, campania.Id, usuario, pregunta, conversacionId, texto, esRepregunta,
            esFallback ? EstadoRespuesta.EvaluacionPendiente : EstadoRespuesta.Evaluada, ahora, cancellationToken,
            ideaIndice, respuestaPadreId, nivelMadurez, ideaRaizId, respuestaAnteriorId, revisionIndice);

        // El Markdown se compila por cada evaluacion valida (cada intento queda con su artefacto; el
        // ultimo es el definitivo). En fallback no se compila (08 §6).
        if (!esFallback)
        {
            await CompilarMarkdownAsync(campania.Id, pregunta, usuario.Id, respuestaId, cancellationToken);
        }

        return nivelMadurez;
    }

    public Task GuardarRespuestaAsync(
        string respuestaId,
        string campaniaId,
        Usuario usuario,
        Pregunta pregunta,
        string conversacionId,
        string texto,
        bool esRepregunta,
        EstadoRespuesta estado,
        DateTimeOffset ahora,
        CancellationToken cancellationToken,
        int? ideaIndice = null,
        string? respuestaPadreId = null,
        NivelMadurez nivelMadurez = NivelMadurez.Incubacion,
        string? ideaRaizId = null,
        string? respuestaAnteriorId = null,
        int? revisionIndice = null,
        string? ideaId = null,
        TipoAporteIdea? tipoAporte = null)
        => _respuestas.GuardarRespuestaAsync(
            RespuestaUsuario.Crear(
                respuestaId,
                campaniaId,
                usuario.Id,
                pregunta.Id,
                conversacionId,
                texto,
                Canal,
                esRepregunta,
                estado,
                ahora,
                usuario.Tags,
                ideaIndice,
                respuestaPadreId,
                nivelMadurez,
                ideaRaizId,
                respuestaAnteriorId,
                revisionIndice,
                ideaId,
                tipoAporte),
            cancellationToken);

    /// <summary>
    /// I-19 §10: (re)genera el artefacto canónico de una idea desde su versión vigente y su evaluación.
    /// Como el Markdown es caché regenerable (REQ §22.4.6), un fallo aquí no rompe el hilo.
    /// </summary>
    public async Task CompilarMarkdownIdeaAsync(
        string campaniaId,
        string ideaId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _compilador.CompilarAsync(
                new SolicitudCompilacion(campaniaId, TipoArtefactoMarkdown.Idea, null, null, null, ideaId),
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // El artefacto es regenerable desde datos (REQ §22.4.6); un fallo de compilacion no rompe el hilo.
        }
    }

    public async Task CompilarMarkdownAsync(
        string campaniaId,
        Pregunta pregunta,
        string usuarioId,
        string respuestaId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _compilador.CompilarAsync(
                new SolicitudCompilacion(campaniaId, pregunta.ConfigMarkdown.TipoArtefacto, respuestaId, usuarioId, pregunta.Id),
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // El artefacto es regenerable desde datos (REQ §22.4.6); un fallo de compilacion no rompe el hilo.
        }
    }

    // I-01: telemetria de calibracion del cierre anticipado. Se registra en LogSeguridad (consultable,
    // 10 §6.2/§6.4) cada vez que el umbral dispara, con el score y el valor de corte (sin PII de texto).
    // Permite dimensionar el umbral en staging: cuantos cierres tempranos y a que calificacion.
    public Task RegistrarCierreUmbralAsync(
        Usuario usuario,
        decimal calificacionTotal,
        decimal valorUmbral,
        EscalaRubrica escala,
        double umbralEfectivo,
        string origen,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
        => _logSeguridad.RegistrarAsync(
            LogSeguridad.Crear(
                "log_" + Guid.NewGuid().ToString("N"),
                TipoEventoSeguridad.CierreUmbralAnticipado,
                usuario.Id,
                usuario.WhatsappNormalizado.Valor,
                "cierre_anticipado",
                FormattableString.Invariant(
                    $"origen:{origen};umbral:{umbralEfectivo:0.###};score:{calificacionTotal};valor:{valorUmbral};escala:{escala.Min}-{escala.Max}"),
                _correlacion.CorrelationIdActual,
                ahora),
            cancellationToken);

    // I-17 (03 §3.4/§3.8): telemetria de calibracion del sellado de madurez. Se registra por cada
    // evaluacion (valida o fallback) para dimensionar la distribucion maduro/incubacion por campania y
    // calibrar el umbral. Sin PII de texto: solo nivel, score, valor de corte, escala y origen del umbral.
    private Task RegistrarClasificacionMadurezAsync(
        Usuario usuario,
        NivelMadurez nivelMadurez,
        decimal calificacionTotal,
        EscalaRubrica escala,
        double umbralBase,
        string origen,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
        => _logSeguridad.RegistrarAsync(
            LogSeguridad.Crear(
                "log_" + Guid.NewGuid().ToString("N"),
                TipoEventoSeguridad.ClasificacionMadurez,
                usuario.Id,
                usuario.WhatsappNormalizado.Valor,
                nivelMadurez == NivelMadurez.Maduro ? "maduro" : "incubacion",
                FormattableString.Invariant(
                    $"origen:{origen};umbral:{umbralBase:0.###};score:{calificacionTotal};valor:{_limites.ValorUmbral(escala, umbralBase)};escala:{escala.Min}-{escala.Max}"),
                _correlacion.CorrelationIdActual,
                ahora),
            cancellationToken);

    // I-17 §5.4: degrada a incubacion las respuestas maduras del hilo tras un rechazo explicito del
    // participante ("guardar salvo que diga no"), regenera su Markdown para que el metadato de madurez
    // (09) refleje la degradacion y deja telemetria. Nunca promueve; es idempotente.
    public async Task ReclasificarComoIncubacionAsync(
        Campania campania,
        Usuario usuario,
        Pregunta pregunta,
        IReadOnlyList<RespuestaUsuario> maduras,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        foreach (var respuesta in maduras)
        {
            respuesta.ReclasificarComoIncubacion();
            await _respuestas.GuardarRespuestaAsync(respuesta, cancellationToken);
            await CompilarMarkdownAsync(campania.Id, pregunta, usuario.Id, respuesta.Id, cancellationToken);
            await RegistrarReclasificacionMadurezAsync(usuario, respuesta.Id, ahora, cancellationToken);
        }
    }

    private Task RegistrarReclasificacionMadurezAsync(
        Usuario usuario,
        string respuestaId,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
        => _logSeguridad.RegistrarAsync(
            LogSeguridad.Crear(
                "log_" + Guid.NewGuid().ToString("N"),
                TipoEventoSeguridad.ClasificacionMadurez,
                usuario.Id,
                usuario.WhatsappNormalizado.Valor,
                "incubacion",
                FormattableString.Invariant($"motivo:rechazo_guardado;respuesta:{respuestaId}"),
                _correlacion.CorrelationIdActual,
                ahora),
            cancellationToken);
}
