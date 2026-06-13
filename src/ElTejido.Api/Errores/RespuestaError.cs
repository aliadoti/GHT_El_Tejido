namespace ElTejido.Api.Errores;

// DTOs del cuerpo de error uniforme (04 §3). Con JsonSerializerDefaults.Web las propiedades se
// serializan en camelCase, produciendo exactamente:
// { "error": { "code", "message", "details": [ { "field", "issue" } ], "correlationId" } }

/// <summary>Envoltura raiz del cuerpo de error.</summary>
internal sealed record RespuestaError(ErrorRespuesta Error);

/// <summary>Cuerpo del error con codigo estable, mensaje, detalles opcionales y correlationId.</summary>
internal sealed record ErrorRespuesta(
    string Code,
    string Message,
    IReadOnlyList<CampoErrorRespuesta>? Details,
    string CorrelationId);

/// <summary>Detalle por campo del error (omitido <c>field</c> cuando es nulo).</summary>
internal sealed record CampoErrorRespuesta(string? Field, string Issue);
