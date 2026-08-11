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
    };

    private static readonly IReadOnlySet<string> PlaceholdersPermitidos = new HashSet<string>(StringComparer.Ordinal)
    {
        "nombre", "campaña", "campania", "empresa", "area",
    };

    public static string ValidarYCalcularHuella(
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

        if (errores.Count > 0)
        {
            throw new ErrorValidacion("El catalogo esta incompleto.", errores);
        }

        ValidarClaves(mensajes!, ClavesMensajes, "mensajes", errores);
        ValidarClaves(frases!, ClavesFrases, "frases", errores);

        foreach (var item in mensajes!)
        {
            if (string.IsNullOrWhiteSpace(item.Value))
            {
                errores.Add(new DetalleError($"mensajes.{item.Key}", "vacio"));
                continue;
            }

            if (item.Value.Length > 1000)
            {
                errores.Add(new DetalleError($"mensajes.{item.Key}", "excede_1000_caracteres"));
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

        foreach (var item in frases!)
        {
            if (item.Value is null || item.Value.Count is < 1 or > 30)
            {
                errores.Add(new DetalleError($"frases.{item.Key}", "debe_tener_entre_1_y_30_elementos"));
                continue;
            }

            var normalizadas = new HashSet<string>(StringComparer.Ordinal);
            foreach (var frase in item.Value)
            {
                if (string.IsNullOrWhiteSpace(frase) || frase.Length > 200)
                {
                    errores.Add(new DetalleError($"frases.{item.Key}", "frase_vacia_o_excede_200_caracteres"));
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

        if (errores.Count > 0)
        {
            throw new ErrorValidacion("El catalogo de textos no es valido.", errores);
        }

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
