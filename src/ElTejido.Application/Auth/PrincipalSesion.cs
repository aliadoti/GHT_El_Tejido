using ElTejido.Domain.Usuarios;

namespace ElTejido.Application.Auth;

/// <summary>
/// Identidad reconstruida al validar un token de sesion (06 §4.4). La usa el Edge para restaurar
/// el estado del SPA (<c>GET /api/auth/me</c>) y, en Fase 4, para autorizar <c>/api/admin/*</c>.
/// </summary>
public sealed record PrincipalSesion(
    string UsuarioId,
    string Nombre,
    RolUsuario Rol,
    string CsrfToken,
    DateTimeOffset ExpiraEn);
