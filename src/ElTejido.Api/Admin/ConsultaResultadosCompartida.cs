using ElTejido.Application.Respuestas;
using ElTejido.Application.Usuarios;
using ElTejido.Domain.Respuestas;
using ElTejido.Domain.Usuarios;
using Microsoft.Extensions.Primitives;
using DominioEvaluacion = ElTejido.Domain.Evaluacion.Evaluacion;

namespace ElTejido.Api.Admin;

/// <summary>
/// P-34 §4.5: el listado de la pantalla y la exportación comparten **una sola** resolución de
/// alcance. Si cada uno filtrara por su cuenta, el archivo terminaría diciendo algo distinto de lo
/// que el administrador vio antes de pedirlo.
/// </summary>
internal static class ConsultaResultadosCompartida
{
    /// <summary>Ideas que sobreviven al filtro, ya ordenadas, con lo que hizo falta leer para lograrlo.</summary>
    internal sealed record AlcanceResultados(
        IReadOnlyList<IdeaConsolidada> Ideas,
        IReadOnlyDictionary<string, Usuario> Participantes,
        IReadOnlyDictionary<string, DominioEvaluacion> Evaluaciones);

    internal static async Task<AlcanceResultados> ResolverAsync(
        HttpContext contexto,
        string campaniaId,
        CriteriosIdeas criterios,
        IQueryCollection query,
        CancellationToken ct)
    {
        var repo = contexto.RequestServices.GetRequiredService<IRepositorioRespuestas>();
        var usuarios = contexto.RequestServices.GetRequiredService<IRepositorioUsuarios>();
        var ideas = await repo.ListarIdeasConsolidadasAsync(campaniaId, ct);

        var candidatas = ideas
            .Where(idea => CoincideOpcional(query["usuarioId"], idea.UsuarioId)
                && CoincideOpcional(query["preguntaId"], idea.PreguntaId)
                && CoincideEnum(query["estadoResultado"], idea.EstadoResultado?.ToString())
                && CoincideEnum(query["estadoFlujo"], idea.EstadoFlujo.ToString())
                && CoincideEnum(query["estadoCuraduria"], idea.EstadoCuraduria?.ToString()))
            .ToArray();

        // P-34 §4.1: la identidad la resuelve el servidor, en una consulta acotada a estos ids.
        var participantes = await ParticipantesDeAsync(usuarios, candidatas, ct);

        // El texto y la calificación solo se leen si algún criterio los necesita (H-10).
        var textos = criterios.NecesitaTexto
            ? TextosDe(candidatas, await VersionesVigentesAsync(repo, campaniaId, candidatas, ct))
            : new Dictionary<string, string>(StringComparer.Ordinal);
        var evaluaciones = criterios.NecesitaCalificacion
            ? await EvaluacionesDeAsync(repo, campaniaId, candidatas, ct)
            : new Dictionary<string, DominioEvaluacion>(StringComparer.Ordinal);

        var filtradas = ConsultaIdeasResultados.FiltrarYOrdenar(
            candidatas,
            criterios,
            participantes,
            textos,
            evaluaciones.ToDictionary(par => par.Key, par => par.Value.CalificacionTotal, StringComparer.Ordinal));

        return new AlcanceResultados(filtradas, participantes, evaluaciones);
    }

    /// <summary>P-34 §4.1: identidad de los participantes de un conjunto de ideas, por ids.</summary>
    internal static async Task<IReadOnlyDictionary<string, Usuario>> ParticipantesDeAsync(
        IRepositorioUsuarios usuarios, IReadOnlyCollection<IdeaConsolidada> ideas, CancellationToken ct)
    {
        var ids = ideas
            .Select(idea => idea.UsuarioId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<string, Usuario>(StringComparer.Ordinal);
        }

        var encontrados = await usuarios.ListarUsuariosPorIdsAsync(ids, ct);
        var indice = new Dictionary<string, Usuario>(encontrados.Count, StringComparer.Ordinal);
        foreach (var usuario in encontrados)
        {
            indice[usuario.Id] = usuario;
        }

        return indice;
    }

    /// <summary>P-34 §6 (H-10): una sola consulta con las versiones vigentes de estas ideas.</summary>
    internal static async Task<IReadOnlyDictionary<string, VersionIdeaConsolidada>> VersionesVigentesAsync(
        IRepositorioRespuestas repo, string campaniaId, IReadOnlyCollection<IdeaConsolidada> ideas, CancellationToken ct)
    {
        var referencias = ideas
            .SelectMany(idea => new[] { idea.VersionConfirmadaRef, idea.VersionPropuestaRef })
            .Where(referencia => !string.IsNullOrWhiteSpace(referencia))
            .Select(referencia => referencia!.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (referencias.Length == 0)
        {
            return new Dictionary<string, VersionIdeaConsolidada>(StringComparer.Ordinal);
        }

        var versiones = await repo.ListarVersionesDeCampaniaAsync(campaniaId, referencias, ct);
        var indice = new Dictionary<string, VersionIdeaConsolidada>(versiones.Count, StringComparer.Ordinal);
        foreach (var version in versiones)
        {
            indice[version.Id] = version;
        }

        return indice;
    }

    /// <summary>P-34 §5: evaluación vigente por idea, en una sola consulta por ids.</summary>
    internal static async Task<IReadOnlyDictionary<string, DominioEvaluacion>> EvaluacionesDeAsync(
        IRepositorioRespuestas repo, string campaniaId, IReadOnlyCollection<IdeaConsolidada> ideas, CancellationToken ct)
    {
        var referencias = ideas
            .Select(idea => idea.EvaluacionVigenteRef)
            .Where(referencia => !string.IsNullOrWhiteSpace(referencia))
            .Select(referencia => referencia!.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (referencias.Length == 0)
        {
            return new Dictionary<string, DominioEvaluacion>(StringComparer.Ordinal);
        }

        var evaluaciones = await repo.ListarEvaluacionesPorIdsAsync(campaniaId, referencias, ct);
        var porId = evaluaciones.ToDictionary(evaluacion => evaluacion.Id, StringComparer.Ordinal);
        var porIdea = new Dictionary<string, DominioEvaluacion>(ideas.Count, StringComparer.Ordinal);
        foreach (var idea in ideas)
        {
            if (!string.IsNullOrWhiteSpace(idea.EvaluacionVigenteRef)
                && porId.TryGetValue(idea.EvaluacionVigenteRef.Trim(), out var evaluacion))
            {
                porIdea[idea.Id] = evaluacion;
            }
        }

        return porIdea;
    }

    /// <summary>Texto vigente por idea, para la búsqueda libre.</summary>
    internal static IReadOnlyDictionary<string, string> TextosDe(
        IReadOnlyCollection<IdeaConsolidada> ideas, IReadOnlyDictionary<string, VersionIdeaConsolidada> versiones)
    {
        var textos = new Dictionary<string, string>(ideas.Count, StringComparer.Ordinal);
        foreach (var idea in ideas)
        {
            var texto = VersionVigente(idea, versiones)?.Texto;
            if (!string.IsNullOrWhiteSpace(texto))
            {
                textos[idea.Id] = texto;
            }
        }

        return textos;
    }

    /// <summary>Confirmada si existe; si no, la propuesta marcada (misma precedencia de I-19).</summary>
    internal static VersionIdeaConsolidada? VersionVigente(
        IdeaConsolidada idea, IReadOnlyDictionary<string, VersionIdeaConsolidada> versiones)
        => Version(idea.VersionConfirmadaRef, versiones) ?? Version(idea.VersionPropuestaRef, versiones);

    /// <summary>Filtros efectivamente aplicados, para la hoja de alcance del archivo (§4.5).</summary>
    internal static IReadOnlyList<(string Clave, string Valor)> FiltrosAplicados(IQueryCollection query)
    {
        string[] claves =
        [
            "q", "estadoResultado", "estadoFlujo", "estadoCuraduria", "usuarioId", "preguntaId",
            "area", "empresa", "sede", "desde", "hasta", "calificacionMin", "calificacionMax",
            "confirmada", "orden", "dir",
        ];

        return claves
            .Select(clave => (Clave: clave, Valor: query[clave].ToString()))
            .Where(filtro => !string.IsNullOrWhiteSpace(filtro.Valor))
            .ToArray();
    }

    private static VersionIdeaConsolidada? Version(
        string? versionId, IReadOnlyDictionary<string, VersionIdeaConsolidada> versiones)
        => string.IsNullOrWhiteSpace(versionId) ? null : versiones.GetValueOrDefault(versionId.Trim());

    private static bool CoincideOpcional(StringValues filtro, string valor)
    {
        var texto = filtro.ToString();
        return string.IsNullOrWhiteSpace(texto) || string.Equals(texto.Trim(), valor, StringComparison.Ordinal);
    }

    private static bool CoincideEnum(StringValues filtro, string? valor)
    {
        var texto = filtro.ToString();
        return string.IsNullOrWhiteSpace(texto)
            || (valor is not null
                && string.Equals(texto.Trim(), MinusculaInicial(valor), StringComparison.OrdinalIgnoreCase));
    }

    private static string MinusculaInicial(string valor)
        => string.IsNullOrEmpty(valor) ? valor : char.ToLowerInvariant(valor[0]) + valor[1..];
}
