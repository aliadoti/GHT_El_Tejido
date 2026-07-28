using ElTejido.Domain.Campanas;
using ElTejido.Domain.Respuestas;
using Newtonsoft.Json;

namespace ElTejido.Infrastructure.Respuestas;

internal sealed class ArtefactoMarkdownCosmosDocument
{
    public const string DocumentType = "ArtefactoMarkdown";

    [JsonProperty("id")]
    public string Id { get; init; } = string.Empty;

    [JsonProperty("type")]
    public string Type { get; init; } = DocumentType;

    [JsonProperty("campaniaId")]
    public string CampaniaId { get; init; } = string.Empty;

    [JsonProperty("tipoArtefacto")]
    public string TipoArtefacto { get; init; } = "respuesta";

    [JsonProperty("usuarioId")]
    public string UsuarioId { get; init; } = string.Empty;

    [JsonProperty("preguntaId")]
    public string PreguntaId { get; init; } = string.Empty;

    [JsonProperty("respuestaRef", NullValueHandling = NullValueHandling.Ignore)]
    public string? RespuestaRef { get; init; }

    [JsonProperty("evaluacionRef", NullValueHandling = NullValueHandling.Ignore)]
    public string? EvaluacionRef { get; init; }

    // I-19 (03 §3.10): referencias canonicas del artefacto por idea; ausentes en artefactos historicos.
    [JsonProperty("ideaRef", NullValueHandling = NullValueHandling.Ignore)]
    public string? IdeaRef { get; init; }

    [JsonProperty("versionIdeaRef", NullValueHandling = NullValueHandling.Ignore)]
    public string? VersionIdeaRef { get; init; }

    [JsonProperty("contenidoMarkdown")]
    public string ContenidoMarkdown { get; init; } = string.Empty;

    [JsonProperty("blobPath")]
    public string BlobPath { get; init; } = string.Empty;

    [JsonProperty("estado")]
    public string Estado { get; init; } = "generado";

    [JsonProperty("version")]
    public int Version { get; init; }

    [JsonProperty("creadoEn")]
    public DateTimeOffset CreadoEn { get; init; }

    [JsonProperty("actualizadoEn")]
    public DateTimeOffset ActualizadoEn { get; init; }

    public static ArtefactoMarkdownCosmosDocument FromDomain(ArtefactoMarkdown artefacto)
        => new()
        {
            Id = artefacto.Id,
            Type = DocumentType,
            CampaniaId = artefacto.CampaniaId,
            TipoArtefacto = artefacto.TipoArtefacto.ToString().ToLowerInvariant(),
            UsuarioId = artefacto.UsuarioId,
            PreguntaId = artefacto.PreguntaId,
            RespuestaRef = artefacto.RespuestaRef,
            EvaluacionRef = artefacto.EvaluacionRef,
            IdeaRef = artefacto.IdeaRef,
            VersionIdeaRef = artefacto.VersionIdeaRef,
            ContenidoMarkdown = artefacto.ContenidoMarkdown,
            BlobPath = artefacto.BlobPath,
            Estado = "generado",
            Version = artefacto.Version,
            CreadoEn = artefacto.CreadoEn,
            ActualizadoEn = artefacto.ActualizadoEn,
        };

    public ArtefactoMarkdown ToDomain()
        => ArtefactoMarkdown.Crear(
            Id,
            CampaniaId,
            MapearTipo(TipoArtefacto),
            UsuarioId,
            PreguntaId,
            RespuestaRef,
            EvaluacionRef,
            ContenidoMarkdown,
            BlobPath,
            EstadoArtefacto.Generado,
            Version,
            CreadoEn,
            ActualizadoEn,
            IdeaRef,
            VersionIdeaRef);

    private static TipoArtefactoMarkdown MapearTipo(string tipo)
        => tipo switch
        {
            "respuesta" => TipoArtefactoMarkdown.Respuesta,
            "idea" => TipoArtefactoMarkdown.Idea,
            "participante" => TipoArtefactoMarkdown.Participante,
            "campania" => TipoArtefactoMarkdown.Campania,
            "entidad" => TipoArtefactoMarkdown.Entidad,
            "capitulo" => TipoArtefactoMarkdown.Capitulo,
            _ => throw new InvalidOperationException($"Tipo de artefacto no soportado en Cosmos: {tipo}."),
        };
}
