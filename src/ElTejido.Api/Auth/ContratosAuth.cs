using ElTejido.Application.Auth;

namespace ElTejido.Api.Auth;

// Contratos HTTP de /api/auth/* (04 §4). Con JsonSerializerDefaults.Web se serializan en camelCase.

/// <summary>Cuerpo de <c>POST /api/auth/request-code</c>.</summary>
internal sealed record SolicitudCodigoRequest(string? Numero);

/// <summary>Cuerpo de <c>POST /api/auth/verify-code</c>.</summary>
internal sealed record VerificarCodigoRequest(string? Numero, string? Codigo);

/// <summary>Respuesta neutral de <c>request-code</c> (04 §4.1).</summary>
internal sealed record RespuestaNeutralCodigo(string Message);

/// <summary>Respuesta de <c>verify-code</c> (04 §4.2): usuario, token CSRF y expiracion.</summary>
internal sealed record RespuestaSesion(UsuarioSesion Usuario, string CsrfToken, DateTimeOffset ExpiraEn);

/// <summary>Respuesta de <c>GET /api/auth/me</c> (04 §4.4).</summary>
internal sealed record RespuestaMe(UsuarioSesion Usuario);
