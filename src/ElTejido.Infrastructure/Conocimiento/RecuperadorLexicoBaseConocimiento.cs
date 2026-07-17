using ElTejido.Application.Conversacion;
using ElTejido.Application.Respuestas;
using ElTejido.Domain.Respuestas;

namespace ElTejido.Infrastructure.Conocimiento;

/// <summary>
/// Implementación <b>A (léxica, determinista)</b> de la base de conocimiento colectiva (I-09, 05 §4.8).
/// Sobre la partición <c>responses</c> de la campaña: filtra por <c>estado=evaluada</c>, <b>excluye al
/// propio autor y la conversación en curso</b>, puntúa por <b>solapamiento léxico</b> de keywords
/// (normalizadas, sin stopwords) con el texto de la consulta, con <b>boost por tags compartidas</b> y
/// <b>recencia</b>, aplica un <b>umbral mínimo</b> y recorta a <c>topK</c>. El <c>Resumen</c> se deriva
/// de <c>Evaluacion.temas ∪ entidades</c> + un extracto <b>anonimizado</b> de <c>Respuesta.texto</c>
/// (nunca nombre/número del autor ni Markdown completo). Cero dependencia nueva, auditable.
/// </summary>
public sealed class RecuperadorLexicoBaseConocimiento : IBaseConocimientoCampania
{
    /// <summary>Longitud máxima del extracto anonimizado incluido en el resumen (I-09 §3, ≤ ~240 chars).</summary>
    private const int MaxCharsExtracto = 240;

    /// <summary>Peso aditivo por cada tag compartida entre el participante y el aporte candidato.</summary>
    private readonly double _boostPorTagCompartida;

    /// <summary>Fracción mínima de keywords de la consulta que un aporte debe cubrir para ser candidato.</summary>
    private readonly double _umbralSolapamiento;

    private readonly IRepositorioRespuestas _respuestas;

    public RecuperadorLexicoBaseConocimiento(
        IRepositorioRespuestas respuestas,
        double umbralSolapamiento = 0.1,
        double boostPorTagCompartida = 0.15)
    {
        _respuestas = respuestas;
        _umbralSolapamiento = umbralSolapamiento;
        _boostPorTagCompartida = boostPorTagCompartida;
    }

    public async Task<IReadOnlyList<AporteRelevante>> RecuperarAsync(
        string campaniaId,
        string textoConsulta,
        IReadOnlyCollection<string> tags,
        string usuarioIdAutorExcluir,
        string? conversacionIdExcluir,
        int topK,
        CancellationToken cancellationToken)
    {
        if (topK <= 0 || string.IsNullOrWhiteSpace(campaniaId) || string.IsNullOrWhiteSpace(textoConsulta))
        {
            return Array.Empty<AporteRelevante>();
        }

        var keywordsConsulta = ExtraerKeywords(textoConsulta);
        if (keywordsConsulta.Count == 0)
        {
            return Array.Empty<AporteRelevante>();
        }

        var tagsConsulta = new HashSet<string>(
            (tags ?? Array.Empty<string>()).Select(t => t.Trim()).Where(t => t.Length > 0),
            StringComparer.OrdinalIgnoreCase);

        var respuestas = await _respuestas.ListarRespuestasAsync(campaniaId, cancellationToken);

        var candidatos = new List<Candidato>();
        foreach (var respuesta in respuestas)
        {
            if (respuesta.Estado != EstadoRespuesta.Evaluada
                || string.Equals(respuesta.UsuarioId, usuarioIdAutorExcluir, StringComparison.Ordinal)
                || (conversacionIdExcluir is not null
                    && string.Equals(respuesta.ConversacionId, conversacionIdExcluir, StringComparison.Ordinal))
                || string.IsNullOrWhiteSpace(respuesta.Texto))
            {
                continue;
            }

            var keywords = ExtraerKeywords(respuesta.Texto);
            if (keywords.Count == 0)
            {
                continue;
            }

            var comunes = keywords.Count(keywordsConsulta.Contains);
            var fraccion = (double)comunes / keywordsConsulta.Count;
            if (fraccion < _umbralSolapamiento)
            {
                continue;
            }

            var tagsCompartidas = respuesta.TagsSnapshot.Count(tagsConsulta.Contains);
            var puntaje = fraccion + (_boostPorTagCompartida * tagsCompartidas);
            candidatos.Add(new Candidato(respuesta, puntaje));
        }

        // Ranking: puntaje (léxico + boost por tags) desc; a igual puntaje, el más reciente primero.
        var seleccionados = candidatos
            .OrderByDescending(c => c.Puntaje)
            .ThenByDescending(c => c.Respuesta.Fecha)
            .Take(topK)
            .ToList();

        var aportes = new List<AporteRelevante>(seleccionados.Count);
        foreach (var candidato in seleccionados)
        {
            var resumen = await ConstruirResumenAsync(campaniaId, candidato.Respuesta, cancellationToken);
            if (string.IsNullOrWhiteSpace(resumen))
            {
                continue;
            }

            aportes.Add(new AporteRelevante(
                resumen,
                candidato.Respuesta.TagsSnapshot.ToArray(),
                candidato.Respuesta.Fecha));
        }

        return aportes;
    }

    /// <summary>
    /// Resumen anonimizado = <c>temas ∪ entidades</c> (etiquetas de la evaluación) + extracto sanitizado
    /// del texto. Si no hay evaluación (dato legacy) usa solo el extracto. Nunca incluye PII del autor.
    /// </summary>
    private async Task<string> ConstruirResumenAsync(
        string campaniaId,
        Respuesta respuesta,
        CancellationToken cancellationToken)
    {
        var evaluacion = await _respuestas.ObtenerEvaluacionPorRespuestaAsync(
            campaniaId, respuesta.Id, cancellationToken);

        var etiquetas = evaluacion is null
            ? Array.Empty<string>()
            : evaluacion.Temas
                .Concat(evaluacion.Entidades)
                .Select(e => e.Trim())
                .Where(e => e.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

        var extracto = SanitizadorAportes.AnonimizarExtracto(respuesta.Texto, MaxCharsExtracto);

        if (etiquetas.Length == 0)
        {
            return extracto;
        }

        var temas = string.Join(", ", etiquetas);
        return extracto.Length == 0 ? temas : temas + " — " + extracto;
    }

    /// <summary>Keywords normalizadas (sin acentos, minúsculas, ≥3 letras, sin stopwords, únicas).</summary>
    private static HashSet<string> ExtraerKeywords(string texto)
    {
        var plano = SanitizadorAportes.QuitarAcentos(texto).ToLowerInvariant();
        var tokens = plano.Split(Separadores, StringSplitOptions.RemoveEmptyEntries);
        var keywords = new HashSet<string>(StringComparer.Ordinal);
        foreach (var token in tokens)
        {
            if (token.Length >= 3 && !Stopwords.Contains(token))
            {
                keywords.Add(token);
            }
        }

        return keywords;
    }

    private static readonly char[] Separadores = BuildSeparadores();

    private static char[] BuildSeparadores()
    {
        // Cualquier carácter que no sea letra o dígito separa tokens.
        var seps = new List<char>();
        for (var c = (char)0; c < 128; c++)
        {
            if (!char.IsLetterOrDigit(c))
            {
                seps.Add(c);
            }
        }

        return seps.ToArray();
    }

    // Stopwords españolas frecuentes (sin acentos, ya que el texto se normaliza antes de comparar).
    private static readonly HashSet<string> Stopwords = new(StringComparer.Ordinal)
    {
        "que", "los", "las", "una", "unos", "unas", "por", "con", "para", "del", "the", "and",
        "como", "mas", "pero", "sus", "les", "este", "esta", "esto", "estos", "estas", "son",
        "hay", "muy", "porque", "cuando", "donde", "quien", "tambien", "sobre", "entre",
        "todo", "todos", "toda", "todas", "nos", "sin", "ser", "hacer", "puede", "pueden",
        "desde", "hasta", "algo", "ademas", "asi", "aqui", "ahi", "eso", "esa", "ese",
    };

    private readonly record struct Candidato(Respuesta Respuesta, double Puntaje);
}
