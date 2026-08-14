using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ElTejido.Application.Common;

namespace ElTejido.Application.Configuracion;

public static partial class ValidadorCatalogoTextosConversacion
{
    public static readonly IReadOnlySet<string> ClavesMensajes = new HashSet<string>(StringComparer.Ordinal)
    {
        "encabezadoConsultaIdea",
        "invitacionConsultaIdea",
        "encabezadoCierreIdea",
        "otrasIdeasGuardadas",
        "sinIdeaDisponible",
        "encabezadoResumenAvance",
        "preguntaContinuarMadurando",
        "saludoPrimerContacto",
        "saludoSiguientePregunta",
        "saludoReactivacion",
        "pausaPorInactividad",
        "invitacionMejora",
        "mensajeConfiguracionNoDisponible",
        "mensajeCalificacionAlta",
        "acuseContinuar",
        "acuseRechazoGuardado",
        "acuseReaperturaIdea",
        "invitacionReaperturaIdea",
        "preguntaSeleccionIdea",
        "instruccionSeleccionIdea",
        "sinIdeasHistoricas",
        "encabezadoSeleccionCampania",
        "instruccionSeleccionCampania",
        "ayudaSeleccionCampaniaInvalida",
        "encabezadoSeleccionPregunta",
        "instruccionSeleccionPregunta",
        "menuAclaracionSalida",
        "respaldoAclaracionSalida",
        "acuseAclaracionContinuar",
    };

    public static readonly IReadOnlySet<string> ClavesFrases = new HashSet<string>(StringComparer.Ordinal)
    {
        "invitacionMejoraVariantes",
        "invitacionContinuarVariantes",
        "acuseContinuarVariantes",
        "continuar",
        "confirmar",
        "finalizarIdea",
        "finalizarParticipacion",
        "solicitarMejora",
        "rechazoGuardado",
        "revisitarAnterior",
        "revisitarIdea",
        "cambiarCampania",
        "despertarProactivo",
        "consultarIdea",
        "acuseConsultaIdea",
        "nuevaIdea",
    };

    private static readonly IReadOnlySet<string> PlaceholdersPermitidos = new HashSet<string>(StringComparer.Ordinal)
    {
        "nombre", "campaña", "campania", "empresa", "area",
    };

    /// <summary>
    /// Valida el catalogo completo y devuelve su huella. <paramref name="limites"/> ausente usa la
    /// politica compilada (DT-P32-02 §2.4); el runtime y la administracion pasan la configurada.
    /// </summary>
    public static string ValidarYCalcularHuella(
        IReadOnlyDictionary<string, string>? mensajes,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>>? frases,
        PoliticaLimitesCatalogoTextos? limites = null)
    {
        if (mensajes is null || frases is null)
        {
            throw new ErrorValidacion("El catalogo esta incompleto.", ErroresDeAusencia(mensajes, frases));
        }

        var errores = Revisar(mensajes, frases, limites ?? PoliticaLimitesCatalogoTextos.PorDefecto);
        if (errores.Count > 0)
        {
            throw new ErrorValidacion("El catalogo de textos no es valido.", errores);
        }

        return CalcularHuella(mensajes, frases);
    }

    /// <summary>
    /// DT-P32-02 §3.3: prevalidacion pura. No persiste, no audita y no invalida cache; ejecuta
    /// exactamente las mismas reglas que la escritura real y devuelve todos los errores detectables.
    /// </summary>
    public static ResultadoPrevalidacionCatalogoTextos Prevalidar(
        string familiaId,
        string idioma,
        IReadOnlyDictionary<string, string>? mensajes,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>>? frases,
        PoliticaLimitesCatalogoTextos? limites = null)
    {
        var conteos = new ConteosCatalogoTextos(
            mensajes?.Count ?? 0,
            frases?.Count ?? 0,
            frases?.Sum(grupo => grupo.Value?.Count ?? 0) ?? 0);
        var errores = mensajes is null || frases is null
            ? ErroresDeAusencia(mensajes, frases)
            : Revisar(mensajes, frases, limites ?? PoliticaLimitesCatalogoTextos.PorDefecto);
        return new ResultadoPrevalidacionCatalogoTextos(
            errores.Count == 0,
            familiaId,
            idioma,
            conteos,
            errores);
    }

    private static IReadOnlyList<DetalleError> ErroresDeAusencia(
        IReadOnlyDictionary<string, string>? mensajes,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>>? frases)
    {
        var errores = new List<DetalleError>();
        if (mensajes is null)
        {
            errores.Add(new DetalleError("mensajes", "obligatorio"));
        }

        if (frases is null)
        {
            errores.Add(new DetalleError("frases", "obligatorio"));
        }

        return errores;
    }

    private static IReadOnlyList<DetalleError> Revisar(
        IReadOnlyDictionary<string, string> mensajes,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> frases,
        PoliticaLimitesCatalogoTextos limites)
    {
        var errores = new List<DetalleError>();
        ValidarClaves(mensajes, ClavesMensajes, "mensajes", errores);
        ValidarClaves(frases, ClavesFrases, "frases", errores);

        foreach (var item in mensajes)
        {
            if (string.IsNullOrWhiteSpace(item.Value))
            {
                errores.Add(new DetalleError($"mensajes.{item.Key}", "vacio"));
                continue;
            }

            if (item.Value.Length > PoliticaLimitesCatalogoTextos.MaxCaracteresMensaje)
            {
                errores.Add(new DetalleError(
                    $"mensajes.{item.Key}",
                    $"excede_{PoliticaLimitesCatalogoTextos.MaxCaracteresMensaje}_caracteres"));
            }

            if (HtmlRegex().IsMatch(item.Value))
            {
                errores.Add(new DetalleError($"mensajes.{item.Key}", "html_no_permitido"));
            }

            foreach (Match match in PlaceholderRegex().Matches(item.Value))
            {
                var nombre = match.Groups[1].Value;
                if (!PlaceholdersPermitidos.Contains(nombre))
                {
                    errores.Add(new DetalleError($"mensajes.{item.Key}", $"placeholder_no_permitido:{nombre}"));
                }
            }
        }

        foreach (var item in frases)
        {
            // El exceso se rechaza completo: DT-P32-02 §2.4 prohibe truncar o mezclar con defaults.
            if (item.Value is null
                || item.Value.Count < PoliticaLimitesCatalogoTextos.MinFrasesPorGrupo
                || item.Value.Count > limites.MaxFrasesPorGrupo)
            {
                errores.Add(new DetalleError(
                    $"frases.{item.Key}",
                    $"debe_tener_entre_{PoliticaLimitesCatalogoTextos.MinFrasesPorGrupo}_y_{limites.MaxFrasesPorGrupo}_elementos"));
                continue;
            }

            var normalizadas = new HashSet<string>(StringComparer.Ordinal);
            foreach (var frase in item.Value)
            {
                if (string.IsNullOrWhiteSpace(frase)
                    || frase.Length > PoliticaLimitesCatalogoTextos.MaxCaracteresFrase)
                {
                    errores.Add(new DetalleError(
                        $"frases.{item.Key}",
                        $"frase_vacia_o_excede_{PoliticaLimitesCatalogoTextos.MaxCaracteresFrase}_caracteres"));
                    continue;
                }

                if (HtmlRegex().IsMatch(frase))
                {
                    errores.Add(new DetalleError($"frases.{item.Key}", "html_no_permitido"));
                }

                if (!normalizadas.Add(NormalizarFrase(frase)))
                {
                    errores.Add(new DetalleError($"frases.{item.Key}", "frase_duplicada"));
                }
            }
        }

        return errores;
    }

    private static string CalcularHuella(
        IReadOnlyDictionary<string, string> mensajes,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> frases)
    {
        var canonico = new StringBuilder();
        foreach (var item in mensajes.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            canonico.Append("m|").Append(item.Key).Append('|').Append(item.Value).Append('\n');
        }

        foreach (var item in frases.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            canonico.Append("f|").Append(item.Key).Append('|').AppendJoin('\u001f', item.Value).Append('\n');
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonico.ToString()))).ToLowerInvariant();
    }

    private static void ValidarClaves<T>(
        IReadOnlyDictionary<string, T> valores,
        IReadOnlySet<string> esperadas,
        string campo,
        ICollection<DetalleError> errores)
    {
        foreach (var faltante in esperadas.Except(valores.Keys, StringComparer.Ordinal))
        {
            errores.Add(new DetalleError($"{campo}.{faltante}", "obligatorio"));
        }

        foreach (var desconocida in valores.Keys.Except(esperadas, StringComparer.Ordinal))
        {
            errores.Add(new DetalleError($"{campo}.{desconocida}", "clave_desconocida"));
        }
    }

    private static string NormalizarFrase(string valor)
    {
        var sinAcentos = new string(valor.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .ToArray());
        return EspaciosRegex().Replace(sinAcentos.Normalize(NormalizationForm.FormC), " ");
    }

    [GeneratedRegex("\\{\\{([^{}]+)\\}\\}", RegexOptions.CultureInvariant)]
    private static partial Regex PlaceholderRegex();

    [GeneratedRegex("<[^>]+>", RegexOptions.CultureInvariant)]
    private static partial Regex HtmlRegex();

    [GeneratedRegex("\\s+", RegexOptions.CultureInvariant)]
    private static partial Regex EspaciosRegex();
}
