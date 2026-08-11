using ElTejido.Application.Conversacion;
using ElTejido.Domain.Configuracion;

namespace ElTejido.Application.Configuracion;

/// <summary>
/// Semillas revisables y respaldo minimo del mismo idioma. Crear una semilla no la persiste ni la
/// activa: esa mutacion solo ocurre mediante el caso de uso administrativo explicito.
/// </summary>
public static class CatalogosTextosSemilla
{
    public const string FamiliaId = "catalogo_conversacion";

    public static SolicitudGuardarCatalogoTextos CrearSolicitud(
        string idioma,
        OpcionesConversacion? opcionesEfectivas = null)
    {
        var normalizado = idioma?.Trim().ToLowerInvariant();
        return normalizado switch
        {
            "es" => new SolicitudGuardarCatalogoTextos(
                FamiliaId,
                "es",
                MensajesEs(opcionesEfectivas?.Mensajes),
                FrasesEs(opcionesEfectivas)),
            "en" => new SolicitudGuardarCatalogoTextos(FamiliaId, "en", MensajesEn(), FrasesEn()),
            _ => throw new ArgumentOutOfRangeException(nameof(idioma), "El idioma debe ser 'es' o 'en'."),
        };
    }

    public static VersionCatalogoTextos CrearVersionEmergencia(string idioma)
    {
        var solicitud = CrearSolicitud(idioma);
        var huella = ValidadorCatalogoTextosConversacion.ValidarYCalcularHuella(
            solicitud.Mensajes,
            solicitud.Frases);
        var catalogo = CatalogoTextosConversacion.Crear(
            "catalogo_conversacion_emergencia",
            solicitud.Idioma,
            1,
            EstadoCatalogoTextos.Activo,
            solicitud.Mensajes,
            solicitud.Frases,
            "compilado",
            "compilado",
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch,
            huella);
        return new VersionCatalogoTextos(catalogo, $"\"emergencia-{solicitud.Idioma}-v1\"");
    }

    private static IReadOnlyDictionary<string, string> MensajesEs(OpcionesMensajesConversacion? mensajes)
        => new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["encabezadoResumenAvance"] = Texto(mensajes?.EncabezadoResumenAvance, OpcionesMensajesConversacion.EncabezadoResumenAvanceDefault),
            ["preguntaContinuarMadurando"] = Texto(mensajes?.PreguntaContinuarMadurando, OpcionesMensajesConversacion.PreguntaContinuarMadurandoDefault),
            ["saludoPrimerContacto"] = Texto(mensajes?.SaludoPrimerContacto, OpcionesMensajesConversacion.SaludoPrimerContactoDefault),
            ["saludoSiguientePregunta"] = Texto(mensajes?.SaludoSiguientePregunta, OpcionesMensajesConversacion.SaludoSiguientePreguntaDefault),
            ["saludoReactivacion"] = Texto(mensajes?.SaludoReactivacion, OpcionesMensajesConversacion.SaludoReactivacionDefault),
            ["pausaPorInactividad"] = Texto(mensajes?.PausaPorInactividad, OpcionesMensajesConversacion.PausaPorInactividadDefault),
            ["invitacionMejora"] = Texto(mensajes?.InvitacionMejora, OpcionesMensajesConversacion.InvitacionMejoraDefault),
            ["mensajeConfiguracionNoDisponible"] = Texto(mensajes?.MensajeConfiguracionNoDisponible, OpcionesMensajesConversacion.MensajeConfiguracionNoDisponibleDefault),
            ["mensajeCalificacionAlta"] = Texto(mensajes?.MensajeCalificacionAlta, OpcionesMensajesConversacion.MensajeCalificacionAltaDefault),
            ["acuseContinuar"] = Texto(mensajes?.AcuseContinuar, OpcionesMensajesConversacion.AcuseContinuarDefault),
            ["acuseRechazoGuardado"] = Texto(mensajes?.AcuseRechazoGuardado, OpcionesMensajesConversacion.AcuseRechazoGuardadoDefault),
            ["acuseReaperturaIdea"] = Texto(mensajes?.AcuseReaperturaIdea, OpcionesMensajesConversacion.AcuseReaperturaIdeaDefault),
            ["invitacionReaperturaIdea"] = Texto(mensajes?.InvitacionReaperturaIdea, OpcionesMensajesConversacion.InvitacionReaperturaIdeaDefault),
            ["preguntaSeleccionIdea"] = Texto(mensajes?.PreguntaSeleccionIdea, OpcionesMensajesConversacion.PreguntaSeleccionIdeaDefault),
            ["instruccionSeleccionIdea"] = Texto(mensajes?.InstruccionSeleccionIdea, OpcionesMensajesConversacion.InstruccionSeleccionIdeaDefault),
            ["sinIdeasHistoricas"] = Texto(mensajes?.SinIdeasHistoricas, OpcionesMensajesConversacion.SinIdeasHistoricasDefault),
            ["encabezadoSeleccionCampania"] = Texto(mensajes?.EncabezadoSeleccionCampania, OpcionesMensajesConversacion.EncabezadoSeleccionCampaniaDefault),
            ["instruccionSeleccionCampania"] = Texto(mensajes?.InstruccionSeleccionCampania, OpcionesMensajesConversacion.InstruccionSeleccionCampaniaDefault),
            ["ayudaSeleccionCampaniaInvalida"] = Texto(mensajes?.AyudaSeleccionCampaniaInvalida, OpcionesMensajesConversacion.AyudaSeleccionCampaniaInvalidaDefault),
            ["encabezadoSeleccionPregunta"] = Texto(mensajes?.EncabezadoSeleccionPregunta, OpcionesMensajesConversacion.EncabezadoSeleccionPreguntaDefault),
            ["instruccionSeleccionPregunta"] = Texto(mensajes?.InstruccionSeleccionPregunta, OpcionesMensajesConversacion.InstruccionSeleccionPreguntaDefault),
            ["menuAclaracionSalida"] = "¿Qué prefieres? Responde 1 para seguir con esta idea, 2 para dejar esta idea y pasar a la siguiente, o 3 para terminar por ahora.",
            ["respaldoAclaracionSalida"] = "Puedes continuar con tu idea o indicar una salida cuando lo necesites.",
            ["acuseAclaracionContinuar"] = "Perfecto, continuemos con esta idea.",
        };

    private static IReadOnlyDictionary<string, IReadOnlyCollection<string>> FrasesEs(OpcionesConversacion? opciones)
    {
        var mensajes = opciones?.Mensajes;
        var finalizacion = ResolutorFrasesFinalizacion.Resolver(opciones ?? new OpcionesConversacion());
        var continuar = Lista(opciones?.FrasesContinuar, DetectorIntencionContinuar.FrasesPorDefecto);
        return new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal)
        {
            ["invitacionMejoraVariantes"] = Lista(
                mensajes?.InvitacionMejoraVariantes,
                new[] { Texto(mensajes?.InvitacionMejora, OpcionesMensajesConversacion.InvitacionMejoraDefault) }),
            ["invitacionContinuarVariantes"] = Lista(
                mensajes?.InvitacionContinuarVariantes,
                OpcionesMensajesConversacion.InvitacionContinuarVariantesDefault),
            ["acuseContinuarVariantes"] = Lista(
                mensajes?.AcuseContinuarVariantes,
                new[] { Texto(mensajes?.AcuseContinuar, OpcionesMensajesConversacion.AcuseContinuarDefault) }),
            ["continuar"] = continuar,
            ["confirmar"] = CombinarSinDuplicadas(
                continuar,
                ["si", "sí", "correcto", "eso es", "exacto", "confirmo"]),
            ["finalizarIdea"] = finalizacion.FinalizarIdea.Frases,
            ["finalizarParticipacion"] = finalizacion.FinalizarParticipacion.Frases,
            ["solicitarMejora"] = Lista(opciones?.FrasesSolicitarMejora, DetectorIntencionContinuar.FrasesSolicitarMejoraPorDefecto),
            ["rechazoGuardado"] = Lista(opciones?.FrasesRechazoGuardado, DetectorIntencionContinuar.FrasesRechazoGuardadoPorDefecto),
            ["revisitarAnterior"] = Lista(opciones?.FrasesRevisitarAnterior, DetectorIntencionContinuar.FrasesRevisitarAnteriorPorDefecto),
            ["revisitarIdea"] = Lista(opciones?.FrasesRevisitarIdea, DetectorIntencionContinuar.FrasesRevisitarIdeaPorDefecto),
            ["cambiarCampania"] = Lista(opciones?.FrasesCambiarCampania, DetectorIntencionContinuar.FrasesCambiarCampaniaPorDefecto),
            ["despertarProactivo"] = Lista(opciones?.FrasesDespertarProactivo, DetectorEntradaProactiva.FrasesPorDefecto),
        };
    }

    private static string Texto(string? configurado, string porDefecto)
        => string.IsNullOrWhiteSpace(configurado) ? porDefecto : configurado.Trim();

    private static IReadOnlyCollection<string> Lista(
        IEnumerable<string>? configurada,
        IReadOnlyCollection<string> porDefecto)
    {
        var valores = configurada?.ToArray() ?? Array.Empty<string>();
        return valores.Length == 0 ? porDefecto : valores;
    }

    private static IReadOnlyCollection<string> CombinarSinDuplicadas(
        IEnumerable<string> primeras,
        IEnumerable<string> adicionales)
        => primeras.Concat(adicionales)
            .Where(valor => !string.IsNullOrWhiteSpace(valor))
            .GroupBy(NormalizarFrase, StringComparer.Ordinal)
            .Select(grupo => grupo.First())
            .ToArray();

    private static string NormalizarFrase(string valor)
    {
        var descompuesto = valor.Trim().ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);
        var sinAcentos = new string(descompuesto
            .Where(caracter => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(caracter)
                != System.Globalization.UnicodeCategory.NonSpacingMark)
            .ToArray());
        var limpio = new System.Text.StringBuilder(sinAcentos.Length);
        foreach (var caracter in sinAcentos)
        {
            limpio.Append(char.IsLetterOrDigit(caracter) ? caracter : ' ');
        }

        return string.Join(' ', limpio.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static IReadOnlyDictionary<string, string> MensajesEn()
        => new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["encabezadoResumenAvance"] = "Here is how your idea is shaping up:",
            ["preguntaContinuarMadurando"] = "Would you like to keep refining it, or leave it as it is?",
            ["saludoPrimerContacto"] = "Hello! Thank you for reaching out. To participate, answer this question:",
            ["saludoSiguientePregunta"] = "Let's continue with the next question:",
            ["saludoReactivacion"] = "Hello! I'm here to help you develop a new idea. Tell me what you would like to propose.",
            ["pausaPorInactividad"] = "Let's pause for now. Whenever you're ready, message me and we'll pick up where we left off.",
            ["invitacionMejora"] = "If you want, send me an improved version of your answer based on this feedback and I will take it into account.",
            ["mensajeConfiguracionNoDisponible"] = "There is a problem with this campaign's configuration. Please contact the system administrator.",
            ["mensajeCalificacionAlta"] = "Excellent! Your answer is already very complete, so let's move on.",
            ["acuseContinuar"] = "Perfect, let's continue!",
            ["acuseRechazoGuardado"] = "Understood. I won't save it as final. Thank you for letting me know!",
            ["acuseReaperturaIdea"] = "Of course, let's return to that idea. This is how it was saved:",
            ["invitacionReaperturaIdea"] = "What would you like to change or add?",
            ["preguntaSeleccionIdea"] = "Which of these ideas would you like to revisit? Reply with its number.",
            ["instruccionSeleccionIdea"] = "Reply with the number or the exact summary of the idea.",
            ["sinIdeasHistoricas"] = "I couldn't find previous ideas on this topic. If you want, share a new idea.",
            ["encabezadoSeleccionCampania"] = "Which campaign does your contribution belong to?",
            ["instruccionSeleccionCampania"] = "Reply with the number or the campaign name.",
            ["ayudaSeleccionCampaniaInvalida"] = "I didn't recognize that option. Your contribution is still saved.",
            ["encabezadoSeleccionPregunta"] = "Which question would you like to contribute to?",
            ["instruccionSeleccionPregunta"] = "Reply with the number or the full question text.",
            ["menuAclaracionSalida"] = "What would you prefer? Reply 1 to continue with this idea, 2 to leave this idea and move to the next one, or 3 to finish for now.",
            ["respaldoAclaracionSalida"] = "You can continue with your idea or indicate that you want to leave when you need to.",
            ["acuseAclaracionContinuar"] = "Perfect, let's continue with this idea.",
        };

    private static IReadOnlyDictionary<string, IReadOnlyCollection<string>> FrasesEn()
        => new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal)
        {
            ["invitacionMejoraVariantes"] = new[]
            {
                "If you want, send me an improved version and I will take it into account.",
                "You can add more detail or make your proposal more specific.",
            },
            ["invitacionContinuarVariantes"] = new[]
            {
                "If you're happy with it, write something like \"it is fine as is\" and we'll continue.",
                "If you prefer to leave it as it is, just say \"done\" and we'll move on.",
                "When you want to close this point, reply \"let's continue\".",
            },
            ["acuseContinuarVariantes"] = new[] { "Perfect, let's continue!", "Great, let's move on!" },
            ["continuar"] = new[]
            {
                "done", "let's continue", "continue", "next question", "it is fine as is",
                "it is good as is", "leave it as is", "I am satisfied", "I don't want to improve it",
            },
            ["confirmar"] = new[]
            {
                "done", "let's continue", "continue", "next question", "it is fine as is",
                "it is good as is", "leave it as is", "I am satisfied", "I don't want to improve it",
                "yes", "correct", "that's right", "exactly", "I confirm",
            },
            ["finalizarIdea"] = new[]
            {
                "I want to stop here", "I want to move to another idea", "move to another idea",
                "let's leave this idea", "leave this idea",
            },
            ["finalizarParticipacion"] = new[]
            {
                "stop now", "I want to finish for now", "finish for now", "stop for today",
                "finish participation", "I don't want to continue", "no more",
            },
            ["solicitarMejora"] = new[]
            {
                "let's improve it", "I want to improve it", "help me improve it", "I would like to improve it",
            },
            ["rechazoGuardado"] = new[]
            {
                "no", "that is not it", "that is not what I meant", "don't save it", "delete it", "remove it",
            },
            ["revisitarAnterior"] = new[]
            {
                "the previous one", "I want to expand the previous one", "go back to the previous one",
                "revisit the previous one", "correct the previous one",
            },
            ["revisitarIdea"] = new[]
            {
                "I want to return to an idea", "return to a previous idea", "revisit an idea",
                "review an idea", "expand an idea", "correct an idea",
            },
            ["cambiarCampania"] = new[]
            {
                "another campaign", "change campaign", "I want to change campaign",
                "I want another campaign", "show other campaigns",
            },
            ["despertarProactivo"] = new[]
            {
                "hello", "hi", "good morning", "good afternoon", "good evening", "I want to participate",
                "I want to start", "I want to begin", "I want to continue", "how do I participate",
            },
        };
}
